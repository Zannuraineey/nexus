using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Application service orchestrating device information queries, provider selection,
/// truthful reporting of unavailable interfaces, and safe carrier unlock workflows.
/// </summary>
public interface IDeviceInformationService
{
    /// <summary>
    /// Retrieves device identifiers (IMEI, model, firmware, lock status) for a device.
    /// Never generates simulated identifiers for physical devices.
    /// </summary>
    Task<DeviceInformationResult> GetDeviceInformationAsync(
        DeviceDescriptor device,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and submits a carrier-issued unlock code to the device.
    /// Never guesses or brute-forces codes. Verifies attempts and safe interface first.
    /// </summary>
    Task<SimUnlockOutcome> ApplyCarrierUnlockCodeAsync(
        DeviceDescriptor device,
        string unlockCode,
        CancellationToken cancellationToken = default);
}
