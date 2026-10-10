namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Physical or virtual transport interface type through which a device communicates.
/// </summary>
public enum DeviceInterfaceType
{
    Unknown,
    Usb,
    SerialCom,
    Adb,
    Fastboot,
    VirtualSimulation
}

/// <summary>
/// Comprehensive domain-level descriptor of an identified mobile device or modem.
/// Properties are optional/nullable where hardware queries cannot retrieve the information.
/// </summary>
public sealed record DeviceDescriptor
{
    public required DeviceIdentifier Identifier { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? ChipsetFamily { get; init; }
    public string? HardwareRevision { get; init; }
    public string? FirmwareVersion { get; init; }
    public DeviceInterfaceType InterfaceType { get; init; } = DeviceInterfaceType.Unknown;
    public DateTime DetectionTimestamp { get; init; } = DateTime.UtcNow;
    public required string ProviderId { get; init; }
    public DeviceCapability SupportedCapabilities { get; init; } = DeviceCapability.None;
    public bool IsSimulation { get; init; } = false;
    public string? PhysicalDeviceId { get; init; }
    public bool IsModemVerified { get; init; } = false;
    public string? Description { get; init; }
    public string? PrimaryHardwareId { get; init; }
    public bool IsConnected { get; init; } = true;
    public DeviceInformationResult? Information { get; init; }
    public IReadOnlyList<DeviceInterfaceDescriptor> Interfaces { get; init; } = Array.Empty<DeviceInterfaceDescriptor>();

    public bool Supports(DeviceCapability capability) =>
        (SupportedCapabilities & capability) == capability;

    public string DisplayName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Model))
            {
                return Identifier.DisplayName;
            }

            if (string.IsNullOrWhiteSpace(Manufacturer) ||
                Manufacturer.Equals("Generic", StringComparison.OrdinalIgnoreCase) ||
                Manufacturer.Equals("Generic USB Device", StringComparison.OrdinalIgnoreCase) ||
                Manufacturer.Equals("USB", StringComparison.OrdinalIgnoreCase))
            {
                return Model;
            }

            if (Model.StartsWith(Manufacturer, StringComparison.OrdinalIgnoreCase) ||
                Manufacturer.Equals(Model, StringComparison.OrdinalIgnoreCase))
            {
                return Model;
            }

            return $"{Manufacturer} {Model}";
        }
    }

    public override string ToString() => $"{DisplayName} [{Identifier.PortName ?? Identifier.SerialNumber}]";
}
