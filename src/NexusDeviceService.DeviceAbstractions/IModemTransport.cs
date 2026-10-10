namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Low-level transport interface representing a physical or simulated communication channel (e.g. SerialPort, USB pipe).
/// </summary>
public interface IModemTransport : IAsyncDisposable
{
    bool IsOpen { get; }
    string? PortName { get; }

    Task OpenAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default);
    Task CloseAsync();
    Task<string> SendCommandAsync(string command, TimeSpan timeout, CancellationToken cancellationToken = default);
}
