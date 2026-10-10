namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Status of modem information query and availability.
/// </summary>
public enum ModemInformationState
{
    Unavailable,
    Available,
    PartiallyAvailable,
    Error
}

/// <summary>
/// Result of querying a device's documented information and management interfaces.
/// Truthfully reflects available identifiers without fabricating simulated data for physical devices.
/// </summary>
public sealed record DeviceInformationResult
{
    public required bool IsAvailable { get; init; }
    public required string StatusMessage { get; init; }
    public ModemInformationState InformationState { get; init; } = ModemInformationState.Unavailable;
    public string? Imei { get; init; }
    public string? MaskedImei { get; init; }
    public string? Model { get; init; }
    public string? Manufacturer { get; init; }
    public string? FirmwareVersion { get; init; }
    public string? HardwareRevision { get; init; }
    public string? Imsi { get; init; }
    public string? Iccid { get; init; }
    public SimNetworkLockStatus LockStatus { get; init; } = SimNetworkLockStatus.Unknown;
    public int RetriesRemaining { get; init; } = -1;
    public bool CanUnlock { get; init; } = false;
    public string? CarrierName { get; init; }
    public string? ManagementChannel { get; init; }
    public string? TechnicalDetails { get; init; }
    public IReadOnlyDictionary<string, string> ExtendedProperties { get; init; } = new Dictionary<string, string>();

    public static DeviceInformationResult Unavailable(string message, string? technicalDetails = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        InformationState = ModemInformationState.Unavailable,
        LockStatus = SimNetworkLockStatus.Unsupported,
        TechnicalDetails = technicalDetails
    };

    public static DeviceInformationResult AvailableResult(
        string message,
        string? imei,
        string? maskedImei,
        string? model,
        string? firmwareVersion,
        string? hardwareRevision,
        SimNetworkLockStatus lockStatus,
        int retriesRemaining,
        bool canUnlock,
        string? managementChannel,
        string? carrierName = null,
        IReadOnlyDictionary<string, string>? extendedProperties = null) => new()
    {
        IsAvailable = true,
        StatusMessage = message,
        InformationState = ModemInformationState.Available,
        Imei = imei,
        MaskedImei = maskedImei,
        Model = model,
        FirmwareVersion = firmwareVersion,
        HardwareRevision = hardwareRevision,
        LockStatus = lockStatus,
        RetriesRemaining = retriesRemaining,
        CanUnlock = canUnlock,
        ManagementChannel = managementChannel,
        CarrierName = carrierName,
        ExtendedProperties = extendedProperties ?? new Dictionary<string, string>()
    };
}
