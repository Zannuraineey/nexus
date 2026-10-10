namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Raw COM port discovery metadata from Windows kernel DEVICEMAP.
/// </summary>
public sealed record RawSerialPortInfo(
    string PortName,
    string DeviceMapKey
);

/// <summary>
/// Raw USB device/interface node metadata from Windows PnP enumeration.
/// </summary>
public sealed record RawUsbDeviceInfo(
    string DeviceInstanceId,
    string HardwareId,
    string? FriendlyName,
    string? DeviceDesc,
    string? ContainerId,
    string? Service,
    string? PortName,
    string? ParentInstanceId
);

/// <summary>
/// Pluggable abstraction for querying low-level Windows device information and serial mappings.
/// Allows injection of mock enumerators for robust testing across environments.
/// </summary>
public interface IWindowsDeviceEnumerator
{
    Task<IReadOnlyList<RawSerialPortInfo>> EnumerateActiveComPortsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawUsbDeviceInfo>> EnumerateUsbDevicesAsync(CancellationToken cancellationToken = default);
}
