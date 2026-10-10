using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Abstraction representing an underlying physical or virtual communication channel (USB, COM, AT port, ADB, or simulation engine).
/// </summary>
public interface IDeviceConnection : IAsyncDisposable
{
    DeviceIdentifier Identifier { get; }
    ConnectionState State { get; }
    bool IsConnected => State == ConnectionState.Connected || State == ConnectionState.Busy;

    event EventHandler<ConnectionState>? StateChanged;

    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
