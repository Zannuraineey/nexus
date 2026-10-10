using System.IO.Ports;
using System.Text;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;

namespace NexusDeviceService.Infrastructure.Modem;

/// <summary>
/// Hardware serial port transport communicating via Windows COM ports.
/// </summary>
public sealed class SerialPortModemTransport : IModemTransport
{
    private readonly ILogger<SerialPortModemTransport> _logger;
    private SerialPort? _serialPort;

    public bool IsOpen => _serialPort?.IsOpen == true;
    public string? PortName => _serialPort?.PortName;

    public SerialPortModemTransport(ILogger<SerialPortModemTransport> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task OpenAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default)
    {
        if (IsOpen)
            throw new InvalidOperationException($"Serial transport is already open on {PortName}.");

        _logger.LogInformation("Opening serial port {PortName} at {BaudRate} baud (8N1)...", portName, baudRate);

        try
        {
            _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                NewLine = "\r\n",
                Encoding = Encoding.ASCII,
                ReadTimeout = 3000,
                WriteTimeout = 3000,
                DtrEnable = true,
                RtsEnable = true
            };

            _serialPort.Open();
            _serialPort.DiscardInBuffer();
            _serialPort.DiscardOutBuffer();

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port {PortName}.", portName);
            _serialPort?.Dispose();
            _serialPort = null;
            throw;
        }
    }

    public Task CloseAsync()
    {
        if (_serialPort != null)
        {
            try
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error closing serial port {PortName}.", _serialPort.PortName);
            }
            finally
            {
                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        return Task.CompletedTask;
    }

    public async Task<string> SendCommandAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
            throw new InvalidOperationException("Serial port is not open.");

        cancellationToken.ThrowIfCancellationRequested();

        var formattedCommand = command.EndsWith("\r") ? command : command + "\r";
        var buffer = new StringBuilder();

        _serialPort.DiscardInBuffer();
        _serialPort.Write(formattedCommand);

        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_serialPort.BytesToRead > 0)
            {
                var chunk = _serialPort.ReadExisting();
                buffer.Append(chunk);

                var current = buffer.ToString();
                if (current.Contains("OK\r\n") || current.Contains("ERROR\r\n") || current.Contains("+CME ERROR:") || current.Contains("+CMS ERROR:"))
                {
                    return current;
                }
            }

            await Task.Delay(25, cancellationToken).ConfigureAwait(false);
        }

        throw new TimeoutException($"Modem did not respond to command within {timeout.TotalMilliseconds}ms. Accumulated output: {buffer}");
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
    }
}
