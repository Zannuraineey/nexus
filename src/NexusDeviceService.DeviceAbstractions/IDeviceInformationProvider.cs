using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Provider abstraction for querying detailed device identifiers and modem parameters
/// over documented, supported device management or diagnostic channels.
/// </summary>
public interface IDeviceInformationProvider
{
    /// <summary>
    /// Unique provider identifier.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Provider priority during handler selection (higher priority evaluated first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Determines whether this provider can handle the given device.
    /// </summary>
    bool CanHandle(DeviceDescriptor device);

    /// <summary>
    /// Retrieves device identifiers (IMEI, model, firmware, lock status) using supported interfaces.
    /// Returns truthful result if interfaces are unavailable without generating simulated data.
    /// </summary>
    Task<DeviceInformationResult> GetDeviceInformationAsync(
        DeviceDescriptor device,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits a carrier-issued unlock code to the device if supported by the provider interface.
    /// </summary>
    Task<SimUnlockOutcome> ApplyCarrierUnlockCodeAsync(
        DeviceDescriptor device,
        string unlockCode,
        CancellationToken cancellationToken = default);
}
