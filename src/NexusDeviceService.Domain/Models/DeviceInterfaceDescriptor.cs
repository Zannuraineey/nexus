namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Describes a logical communication interface exposed by a physical device 
/// (e.g., Diagnostic port, AT modem interface, NMEA GPS port, ADB endpoint).
/// </summary>
public sealed record DeviceInterfaceDescriptor
{
    public required string InterfaceId { get; init; }
    public string? FriendlyName { get; init; }
    public DeviceInterfaceType InterfaceType { get; init; } = DeviceInterfaceType.Unknown;
    public string? PortName { get; init; }
    public string? HardwareId { get; init; }
    public string? DriverService { get; init; }
    public bool IsOperational { get; init; } = true;

    public override string ToString() =>
        $"{InterfaceId} ({PortName ?? "No Port"}): {FriendlyName ?? HardwareId ?? "Unknown Interface"}";
}
