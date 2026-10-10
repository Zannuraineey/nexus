using NexusDeviceService.DeviceAbstractions;

namespace NexusDeviceService.Infrastructure.Modem;

/// <summary>
/// Hardware-independent mock transport for unit testing and offline development.
/// </summary>
public sealed class MockModemTransport : IModemTransport
{
    private readonly Dictionary<string, string> _cannedResponses = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _commandHistory = new();

    public bool IsOpen { get; private set; }
    public string? PortName { get; private set; }
    public IReadOnlyList<string> CommandHistory => _commandHistory;

    public bool SimulateTimeout { get; set; } = false;
    public string? SimulateErrorForCommand { get; set; }

    public MockModemTransport()
    {
        // Default standard 3GPP Quectel EC25 profile
        _cannedResponses["AT"] = "\r\nOK\r\n";
        _cannedResponses["ATI"] = "\r\nQuectel\r\nEC25\r\nRevision: EC25EFAR06A03M4G\r\n\r\nOK\r\n";
        _cannedResponses["AT+GMR"] = "\r\nEC25EFAR06A03M4G\r\n\r\nOK\r\n";
        _cannedResponses["AT+GSN"] = "\r\n867890041234567\r\n\r\nOK\r\n";
        _cannedResponses["AT+CSQ"] = "\r\n+CSQ: 24,99\r\n\r\nOK\r\n";
        _cannedResponses["AT+CPIN?"] = "\r\n+CPIN: READY\r\n\r\nOK\r\n";
        _cannedResponses["AT+COPS?"] = "\r\n+COPS: 0,0,\"Vodafone UK\",7\r\n\r\nOK\r\n";
        _cannedResponses["AT+CLCK=\"PN\",2"] = "\r\n+CLCK: 1\r\n\r\nOK\r\n";
        _cannedResponses["AT+QPINC=\"PN\""] = "\r\n+QPINC: \"PN\",10,10\r\n\r\nOK\r\n";
    }

    public void SetResponse(string command, string response)
    {
        _cannedResponses[command.Trim()] = response;
    }

    public Task OpenAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IsOpen = true;
        PortName = portName;
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        IsOpen = false;
        PortName = null;
        return Task.CompletedTask;
    }

    public Task<string> SendCommandAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsOpen)
            throw new InvalidOperationException("Mock modem transport is not open.");

        if (SimulateTimeout)
            throw new TimeoutException($"Simulated transport timeout after {timeout.TotalMilliseconds}ms.");

        var cleanCmd = command.Trim();
        _commandHistory.Add(cleanCmd);

        if (SimulateErrorForCommand != null && cleanCmd.Equals(SimulateErrorForCommand, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult("\r\n+CME ERROR: 100\r\n");
        }

        if (_cannedResponses.TryGetValue(cleanCmd, out var response))
        {
            return Task.FromResult(response);
        }

        // Dynamically handle AT+CLCK="PN",0,"<NCK>" unlock requests
        if (cleanCmd.StartsWith("AT+CLCK=\"PN\",0,", StringComparison.OrdinalIgnoreCase))
        {
            // Transition facility state to unlocked (0) upon successful command
            _cannedResponses["AT+CLCK=\"PN\",2"] = "\r\n+CLCK: 0\r\n\r\nOK\r\n";
            return Task.FromResult("\r\nOK\r\n");
        }

        return Task.FromResult("\r\nCOMMAND NOT SUPPORTED\r\nERROR\r\n");
    }

    public ValueTask DisposeAsync()
    {
        IsOpen = false;
        return ValueTask.CompletedTask;
    }
}
