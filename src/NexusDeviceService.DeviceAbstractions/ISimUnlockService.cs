using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Service abstraction for legitimate, carrier/manufacturer-authorized SIM unlocking workflows.
/// Decouples eligibility checks, user consent, authorization requests, operation status,
/// baseband verification, and persistent audit history.
/// </summary>
public interface ISimUnlockService
{
    /// <summary>
    /// Phase 1: Checks device compatibility, hardware attempt counter, and 3GPP facility lock status.
    /// Returns honest unsupported guidance if device or facility is not supported.
    /// </summary>
    Task<SimUnlockEligibilityResult> CheckEligibilityAsync(
        DeviceDescriptor device,
        IModemCommunicationChannel channel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 3: Obtains an authorized unlock ticket following user consent (Phase 2).
    /// </summary>
    Task<SimUnlockAuthorizationTicket> RequestAuthorizationAsync(
        DeviceDescriptor device,
        SimUnlockConsent consent,
        string maskedImei,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 4 & 5: Applies the authorized Network Control Key and verifies baseband unlock status.
    /// Reports progress through Phase 4 and confirms physical status in Phase 5.
    /// </summary>
    Task<SimUnlockOutcome> ApplyAndVerifyUnlockAsync(
        DeviceDescriptor device,
        IModemCommunicationChannel channel,
        SimUnlockAuthorizationTicket ticket,
        IOperationProgressReporter? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 6: Retrieves the audit log history for authorized unlock operations.
    /// </summary>
    Task<IReadOnlyList<SimUnlockAuditRecord>> GetAuditHistoryAsync(
        string? deviceId = null,
        CancellationToken cancellationToken = default);
}
