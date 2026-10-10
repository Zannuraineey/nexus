namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Describes a recognized device specification, vendor/model branding, and its allowed capabilities.
/// </summary>
public sealed class DeviceProfile
{
    public required string ProfileId { get; init; }
    public required string Manufacturer { get; init; }
    public required string ModelName { get; init; }
    public required string ChipsetFamily { get; init; }
    public DeviceCapability SupportedCapabilities { get; init; }
    public bool IsSimulationProfile { get; init; }

    public bool Supports(DeviceCapability capability) =>
        (SupportedCapabilities & capability) == capability;
}
