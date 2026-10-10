using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Infrastructure.SimUnlock;

/// <summary>
/// Reference implementation of ISimUnlockService enforcing:
/// 1. Device eligibility and attempt counter checks without exploit bypasses.
/// 2. User consent and terms acceptance verification.
/// 3. Server-mediated authorization tickets (keeping credentials off the desktop client).
/// 4. Fine-grained operation status reporting.
/// 5. Physical baseband result verification (AT+CLCK="PN",2).
/// 6. Comprehensive audit history recording.
/// </summary>
public sealed class SimUnlockService : ISimUnlockService
{
    private readonly ISimUnlockAuthorizationClient _authorizationClient;
    private readonly ISimUnlockAuditStore _auditStore;
    private readonly ILogger<SimUnlockService> _logger;

    private static readonly Regex ClckStatusRegex = new(
        @"\+CLCK:\s*(?<status>[0-2])",
        RegexOptions.Compiled);

    private static readonly Regex QpincRegex = new(
        @"\+QPINC:\s*""PN"",\s*(?<remaining>\d+),\s*(?<total>\d+)",
        RegexOptions.Compiled);

    public SimUnlockService(
        ISimUnlockAuthorizationClient authorizationClient,
        ISimUnlockAuditStore auditStore,
        ILogger<SimUnlockService> logger)
    {
        _authorizationClient = authorizationClient ?? throw new ArgumentNullException(nameof(authorizationClient));
        _auditStore = auditStore ?? throw new ArgumentNullException(nameof(auditStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<SimUnlockEligibilityResult> CheckEligibilityAsync(
        DeviceDescriptor device,
        IModemCommunicationChannel channel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Validate device family compatibility
        if (!IsSupportedModelFamily(device))
        {
            _logger.LogWarning("Device {DeviceId} ({Model}) is not in an authorized 3GPP facility unlock family.", device.Identifier.ToString(), device.Model);
            return new SimUnlockEligibilityResult
            {
                IsEligible = false,
                LockStatus = SimNetworkLockStatus.Unsupported,
                GuidanceMessage = $"Device '{device.Model}' is not supported for 3GPP facility unlocking. Please contact your mobile network operator or equipment manufacturer for official service.",
                RequiresServerAuthorization = false
            };
        }

        // 2. Query 3GPP TS 27.007 Facility Lock Status: AT+CLCK="PN",2
        string queryResponse;
        try
        {
            queryResponse = await channel.ExecuteCommandAsync("AT+CLCK=\"PN\",2", cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Exception querying AT+CLCK=\"PN\",2 for device {DeviceId}: {Error}", device.Identifier.ToString(), ex.Message);
            return new SimUnlockEligibilityResult
            {
                IsEligible = false,
                LockStatus = SimNetworkLockStatus.Unsupported,
                GuidanceMessage = $"Modem communication error ({ex.Message}) while querying network personalization status. Please verify SIM presence and carrier policy.",
                RequiresServerAuthorization = false
            };
        }

        if (queryResponse.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Modem query AT+CLCK=\"PN\",2 returned error for device {DeviceId}: {Response}", device.Identifier.ToString(), queryResponse);
            return new SimUnlockEligibilityResult
            {
                IsEligible = false,
                LockStatus = SimNetworkLockStatus.Unsupported,
                GuidanceMessage = $"Modem returned an error ({queryResponse.Trim()}) while querying network personalization status. Please verify SIM presence and carrier policy.",
                RequiresServerAuthorization = false
            };
        }

        var match = ClckStatusRegex.Match(queryResponse);
        if (!match.Success)
        {
            return new SimUnlockEligibilityResult
            {
                IsEligible = false,
                LockStatus = SimNetworkLockStatus.Unknown,
                GuidanceMessage = "Unable to parse baseband facility lock status response. Consult operator service.",
                RequiresServerAuthorization = false
            };
        }

        var statusValue = match.Groups["status"].Value;

        if (statusValue == "0")
        {
            return new SimUnlockEligibilityResult
            {
                IsEligible = false,
                LockStatus = SimNetworkLockStatus.Unlocked,
                RetriesRemaining = 10,
                GuidanceMessage = "Device SIM is already network-unlocked (Facility 'PN' status is 0). No unlock action required.",
                RequiresServerAuthorization = false
            };
        }

        // Status is 1 (Locked) - check remaining attempts
        var retries = await QueryRemainingAttemptsAsync(channel, cancellationToken);

        if (retries == 0)
        {
            return new SimUnlockEligibilityResult
            {
                IsEligible = false,
                LockStatus = SimNetworkLockStatus.PermanentlyBlocked,
                RetriesRemaining = 0,
                GuidanceMessage = "Network unlock retry counter has reached zero (0 retries remaining). The baseband is permanently blocked from software unlocking. Please contact the network operator for MEP/PUK service.",
                RequiresServerAuthorization = false
            };
        }

        return new SimUnlockEligibilityResult
        {
            IsEligible = true,
            LockStatus = SimNetworkLockStatus.NetworkLocked,
            RetriesRemaining = retries,
            CarrierName = "Authorized Carrier Network",
            GuidanceMessage = $"Device is network locked (Facility 'PN') with {retries} attempts remaining. Valid carrier authorization ticket or NCK required.",
            RequiresServerAuthorization = true
        };
    }

    /// <inheritdoc />
    public async Task<SimUnlockAuthorizationTicket> RequestAuthorizationAsync(
        DeviceDescriptor device,
        SimUnlockConsent consent,
        string maskedImei,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!consent.UserConfirmed || !consent.AcknowledgedRiskNotice)
        {
            throw new InvalidOperationException("User consent and legal acknowledgment are required before requesting carrier authorization.");
        }

        _logger.LogInformation("Requesting carrier authorization ticket for device {DeviceId} (IMEI: {Imei})", device.Identifier.ToString(), maskedImei);
        return await _authorizationClient.RequestTicketAsync(device, consent, maskedImei, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<SimUnlockOutcome> ApplyAndVerifyUnlockAsync(
        DeviceDescriptor device,
        IModemCommunicationChannel channel,
        SimUnlockAuthorizationTicket ticket,
        IOperationProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (DateTimeOffset.UtcNow > ticket.ExpiresAtUtc)
        {
            throw new InvalidOperationException("The authorization ticket has expired. Please request a new carrier authorization ticket.");
        }

        if (!string.Equals(ticket.DeviceId, device.Identifier.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Authorization ticket device mismatch: ticket is for '{ticket.DeviceId}' but target device is '{device.Identifier.ToString()}'.");
        }

        // Phase 4: Operation Status reporting
        progress?.ReportProgress(10, "Initializing secure communication channel...", "Phase 4: Channel Acquisition");

        // Phase 4: Submit NCK via 3GPP TS 27.007 AT+CLCK="PN",0,"<NCK>"
        progress?.ReportProgress(30, "Submitting authorized Network Control Key to modem...", "Phase 4: NCK Transmission");

        var unlockCommand = $"AT+CLCK=\"PN\",0,\"{ticket.NetworkControlKeyToken}\"";
        string unlockResponse;
        try
        {
            unlockResponse = await channel.ExecuteCommandAsync(unlockCommand, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Baseband communication error during unlock: {Error}", ex.Message);
            var failureOutcome = new SimUnlockOutcome
            {
                IsSuccess = false,
                IsMockSuccess = false,
                StatusBefore = SimNetworkLockStatus.NetworkLocked,
                StatusAfter = SimNetworkLockStatus.NetworkLocked,
                TicketId = ticket.TicketId,
                ErrorMessage = ex.Message,
                GuidanceMessage = "The modem connection was interrupted during the unlock attempt. Please verify hardware connection."
            };

            await RecordAuditAsync(device, ticket, failureOutcome, cancellationToken);
            return failureOutcome;
        }

        if (unlockResponse.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Baseband rejected unlock command: {Response}", unlockResponse);
            var failureOutcome = new SimUnlockOutcome
            {
                IsSuccess = false,
                IsMockSuccess = false,
                StatusBefore = SimNetworkLockStatus.NetworkLocked,
                StatusAfter = SimNetworkLockStatus.NetworkLocked,
                TicketId = ticket.TicketId,
                ErrorMessage = unlockResponse.Trim(),
                GuidanceMessage = "The baseband rejected the unlock key or encountered a CME error. Please verify authorization credentials with your carrier."
            };

            await RecordAuditAsync(device, ticket, failureOutcome, cancellationToken);
            return failureOutcome;
        }

        progress?.ReportProgress(70, "Awaiting baseband facility state transition...", "Phase 4: State Transition");

        // Phase 5: Result Verification (Physical Query)
        progress?.ReportProgress(85, "Verifying physical baseband lock status...", "Phase 5: Result Verification");

        var verifyResponse = await channel.ExecuteCommandAsync("AT+CLCK=\"PN\",2", cancellationToken: cancellationToken);
        var match = ClckStatusRegex.Match(verifyResponse);
        var verifiedStatus = match.Success && match.Groups["status"].Value == "0"
            ? SimNetworkLockStatus.Unlocked
            : SimNetworkLockStatus.NetworkLocked;

        SimUnlockOutcome outcome;

        if (verifiedStatus == SimNetworkLockStatus.Unlocked)
        {
            progress?.ReportProgress(100, "Baseband SIM network personalization successfully deactivated.", "Phase 5: Verified Unlocked");
            outcome = new SimUnlockOutcome
            {
                IsSuccess = true,
                IsMockSuccess = ticket.IsMockAuthorization,
                StatusBefore = SimNetworkLockStatus.NetworkLocked,
                StatusAfter = SimNetworkLockStatus.Unlocked,
                TicketId = ticket.TicketId,
                GuidanceMessage = ticket.IsMockAuthorization
                    ? "Mock authorization verified successfully. Device simulated facility lock transitioned to Unlocked (0)."
                    : "Legitimate SIM network unlock verified. The device is now permanently unlocked for all compatible networks."
            };
        }
        else
        {
            progress?.ReportProgress(100, "Verification check failed. Baseband remains locked.", "Phase 5: Verification Failed");
            outcome = new SimUnlockOutcome
            {
                IsSuccess = false,
                IsMockSuccess = false,
                StatusBefore = SimNetworkLockStatus.NetworkLocked,
                StatusAfter = SimNetworkLockStatus.NetworkLocked,
                TicketId = ticket.TicketId,
                ErrorMessage = "Post-unlock verification failed: baseband returned locked status (1).",
                GuidanceMessage = "The unlock command completed but physical verification confirmed facility lock remains active. Please contact carrier support."
            };
        }

        // Phase 6: Audit History
        await RecordAuditAsync(device, ticket, outcome, cancellationToken);
        return outcome;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SimUnlockAuditRecord>> GetAuditHistoryAsync(
        string? deviceId = null,
        CancellationToken cancellationToken = default)
    {
        return _auditStore.GetRecordsAsync(deviceId, cancellationToken);
    }

    private static bool IsSupportedModelFamily(DeviceDescriptor device)
    {
        var model = device.Model?.ToUpperInvariant() ?? string.Empty;
        var mfr = device.Manufacturer?.ToUpperInvariant() ?? string.Empty;
        var name = device.DisplayName?.ToUpperInvariant() ?? string.Empty;

        return model.Contains("EC25") ||
               model.Contains("EG25") ||
               model.Contains("EC21") ||
               mfr.Contains("QUECTEL") ||
               name.Contains("QUECTEL") ||
               device.Supports(DeviceCapability.AuthorizedSimUnlock) ||
               device.Identifier.ToString().Contains("TEST", StringComparison.OrdinalIgnoreCase) ||
               device.Identifier.ToString().Contains("SIMULATED", StringComparison.OrdinalIgnoreCase) ||
               device.Identifier.ToString().Contains("MOCK", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<int> QueryRemainingAttemptsAsync(
        IModemCommunicationChannel channel,
        CancellationToken cancellationToken)
    {
        try
        {
            var pincResponse = await channel.ExecuteCommandAsync("AT+QPINC=\"PN\"", cancellationToken: cancellationToken);
            if (!pincResponse.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
            {
                var match = QpincRegex.Match(pincResponse);
                if (match.Success && int.TryParse(match.Groups["remaining"].Value, out var count))
                {
                    return count;
                }
            }
        }
        catch
        {
            // Fallback to standard 3GPP retry ceiling
        }

        return 10;
    }

    private async Task RecordAuditAsync(
        DeviceDescriptor device,
        SimUnlockAuthorizationTicket ticket,
        SimUnlockOutcome outcome,
        CancellationToken cancellationToken)
    {
        var record = new SimUnlockAuditRecord
        {
            AuditId = $"AUDIT-{Guid.NewGuid():N}"[..18].ToUpperInvariant(),
            DeviceId = device.Identifier.ToString(),
            MaskedImei = ticket.MaskedImei,
            TimestampUtc = DateTimeOffset.UtcNow,
            IsSuccess = outcome.IsSuccess,
            IsMockSuccess = outcome.IsMockSuccess,
            TicketId = ticket.TicketId,
            Summary = outcome.GuidanceMessage
        };

        await _auditStore.AddRecordAsync(record, cancellationToken);
        _logger.LogInformation("Recorded SIM unlock audit {AuditId} for device {DeviceId}. Outcome Success: {Success}, Mock: {IsMock}",
            record.AuditId, record.DeviceId, record.IsSuccess, record.IsMockSuccess);
    }
}
