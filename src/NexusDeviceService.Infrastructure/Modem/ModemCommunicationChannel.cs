using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;

namespace NexusDeviceService.Infrastructure.Modem;

/// <summary>
/// Production and test implementation of <see cref="IModemCommunicationChannel"/> 
/// that enforces serialized mutex access, timeout policies, response validation, and sensitive data scrubbing.
/// </summary>
public sealed class ModemCommunicationChannel : IModemCommunicationChannel
{
    private static readonly Regex CsqRegex = new(@"\+CSQ:\s*(?<rssi>\d+),(?<ber>\d+)", RegexOptions.Compiled);
    private static readonly Regex CpinRegex = new(@"\+CPIN:\s*(?<status>[^\r\n]+)", RegexOptions.Compiled);
    private static readonly Regex CopsRegex = new(@"\+COPS:\s*(?<mode>\d+),(?<format>\d+),""(?<oper>[^""]+)""(?:,(?<act>\d+))?", RegexOptions.Compiled);

    private readonly IModemTransport _transport;
    private readonly ISensitiveDataRedactor _redactor;
    private readonly ILogger<ModemCommunicationChannel> _logger;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(3);

    public bool IsConnected => _transport.IsOpen;
    public string? CurrentPort => _transport.PortName;

    public ModemCommunicationChannel(
        IModemTransport transport,
        ISensitiveDataRedactor redactor,
        ILogger<ModemCommunicationChannel> logger)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ConnectAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
            throw new ArgumentException("Port name cannot be empty.", nameof(portName));

        await _channelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Connecting to modem transport on port {PortName} ({BaudRate} baud)...", portName, baudRate);
            await _transport.OpenAsync(portName, baudRate, cancellationToken).ConfigureAwait(false);

            // Send standard AT attention handshake to confirm responsiveness
            var response = await _transport.SendCommandAsync("AT", _defaultTimeout, cancellationToken).ConfigureAwait(false);
            if (!response.Contains("OK", StringComparison.OrdinalIgnoreCase))
            {
                await _transport.CloseAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Modem did not respond with OK to AT synchronization. Received: {response.Trim()}");
            }

            _logger.LogInformation("Modem communication channel successfully initialized on port {PortName}.", portName);
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _channelLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_transport.IsOpen)
            {
                _logger.LogInformation("Closing modem communication channel on port {PortName}...", CurrentPort);
                await _transport.CloseAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task<string> ExecuteCommandAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Modem communication channel is not connected.");

        var effectiveTimeout = timeout ?? _defaultTimeout;
        var sanitizedCommand = _redactor.Redact(command);

        await _channelLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogDebug("Transmitting AT command: {Command} (Timeout: {Timeout}ms)", sanitizedCommand, effectiveTimeout.TotalMilliseconds);
            var response = await _transport.SendCommandAsync(command, effectiveTimeout, cancellationToken).ConfigureAwait(false);
            var sanitizedResponse = _redactor.Redact(response);

            _logger.LogTrace("Modem response: {Response}", sanitizedResponse);

            if (response.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || 
                response.Contains("COMMAND NOT SUPPORTED", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException($"Modem returned error or unsupported response for command '{sanitizedCommand}': {response.Trim()}");
            }

            return response;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async Task<ModemIdentityInfo> QueryIdentityAsync(CancellationToken cancellationToken = default)
    {
        var rawId = await ExecuteCommandAsync("ATI", null, cancellationToken).ConfigureAwait(false);
        var gmr = await ExecuteCommandAsync("AT+GMR", null, cancellationToken).ConfigureAwait(false);
        var gsn = await ExecuteCommandAsync("AT+GSN", null, cancellationToken).ConfigureAwait(false);

        var manufacturer = ExtractCleanLine(rawId, 0) ?? "Unknown Manufacturer";
        var model = ExtractCleanLine(rawId, 1) ?? "Standard 3GPP Modem";
        var fw = ExtractCleanLine(gmr, 0) ?? "Unknown Firmware";
        var imei = ExtractCleanLine(gsn, 0) ?? "Unknown IMEI";

        return new ModemIdentityInfo(
            Manufacturer: manufacturer,
            Model: model,
            FirmwareVersion: fw,
            Imei: _redactor.Redact(imei),
            RawOutput: rawId
        );
    }

    public async Task<ModemSignalInfo> QuerySignalQualityAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteCommandAsync("AT+CSQ", null, cancellationToken).ConfigureAwait(false);
        var match = CsqRegex.Match(response);
        if (!match.Success)
            throw new FormatException($"Unable to parse +CSQ signal response: {response}");

        int rssi = int.Parse(match.Groups["rssi"].Value);
        int ber = int.Parse(match.Groups["ber"].Value);

        int? dbm = rssi switch
        {
            99 => null,
            0 => -113,
            1 => -111,
            31 => -51,
            _ => -113 + (rssi * 2)
        };

        string quality = rssi switch
        {
            99 => "Not Known or Detectable",
            < 10 => "Poor",
            < 15 => "Fair",
            < 20 => "Good",
            _ => "Excellent"
        };

        return new ModemSignalInfo(rssi, dbm, ber, quality);
    }

    public async Task<SimPinState> QuerySimStatusAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteCommandAsync("AT+CPIN?", null, cancellationToken).ConfigureAwait(false);
        var match = CpinRegex.Match(response);
        if (!match.Success)
            return SimPinState.Unknown;

        var status = match.Groups["status"].Value.Trim().ToUpperInvariant();
        return status switch
        {
            "READY" => SimPinState.Ready,
            "SIM PIN" => SimPinState.PinRequired,
            "SIM PUK" => SimPinState.PukRequired,
            _ when status.Contains("NOT INSERTED") => SimPinState.NotInserted,
            _ => SimPinState.Unknown
        };
    }

    public async Task<ModemOperatorInfo> QueryOperatorAsync(CancellationToken cancellationToken = default)
    {
        var response = await ExecuteCommandAsync("AT+COPS?", null, cancellationToken).ConfigureAwait(false);
        var match = CopsRegex.Match(response);
        if (!match.Success)
            throw new FormatException($"Unable to parse +COPS operator response: {response}");

        int mode = int.Parse(match.Groups["mode"].Value);
        int format = int.Parse(match.Groups["format"].Value);
        string oper = match.Groups["oper"].Value;
        int? act = match.Groups["act"].Success ? int.Parse(match.Groups["act"].Value) : null;

        return new ModemOperatorInfo(mode, format, oper, act);
    }

    private static string? ExtractCleanLine(string output, int index)
    {
        var lines = output
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrEmpty(l) && !l.Equals("OK", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return index < lines.Count ? lines[index] : lines.FirstOrDefault();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _channelLock.Dispose();
    }
}
