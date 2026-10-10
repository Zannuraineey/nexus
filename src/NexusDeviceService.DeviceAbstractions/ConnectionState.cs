namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Lifecycle state of a physical or simulated device connection channel.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Busy,
    Faulted,
    Disconnecting
}
