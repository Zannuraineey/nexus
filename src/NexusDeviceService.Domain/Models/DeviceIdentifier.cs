namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Uniquely identifies a connected physical or simulated mobile device/modem.
/// </summary>
public sealed record DeviceIdentifier
{
    public required string VendorId { get; init; }
    public required string ProductId { get; init; }
    public required string SerialNumber { get; init; }
    public string? Revision { get; init; }
    public string? PortName { get; init; }

    public string DisplayName => $"{VendorId}:{ProductId} ({SerialNumber})";

    public override string ToString() => DisplayName;
}
