namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Status of SIM Network Personalization (facility lock "PN").
/// </summary>
public enum SimNetworkLockStatus
{
    Unknown,
    Unlocked,
    NetworkLocked,
    NetworkSubsetLocked,
    ServiceLocked,
    PermanentlyBlocked,
    Unsupported
}

/// <summary>
/// Result of Phase 1 eligibility check.
/// </summary>
public sealed record SimUnlockEligibilityResult
{
    public required bool IsEligible { get; init; }
    public required SimNetworkLockStatus LockStatus { get; init; }
    public int RetriesRemaining { get; init; } = -1;
    public string? CarrierName { get; init; }
    public required string GuidanceMessage { get; init; }
    public bool RequiresServerAuthorization { get; init; } = true;
}

/// <summary>
/// Phase 2 explicit user consent and legal acceptance.
/// </summary>
public sealed record SimUnlockConsent
{
    public required bool UserConfirmed { get; init; }
    public required string TermsAgreementVersion { get; init; }
    public DateTimeOffset AcceptedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required bool AcknowledgedRiskNotice { get; init; }
}

/// <summary>
/// Phase 3 authorized unlock ticket issued by carrier service or mock authority.
/// </summary>
public sealed record SimUnlockAuthorizationTicket
{
    public required string TicketId { get; init; }
    public required string DeviceId { get; init; }
    public required string MaskedImei { get; init; }
    public required bool IsMockAuthorization { get; init; }
    public required string NetworkControlKeyToken { get; init; }
    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; init; } = DateTimeOffset.UtcNow.AddMinutes(30);
    public required string ProviderName { get; init; }
}

/// <summary>
/// Phase 5 final operation outcome with post-write baseband verification.
/// </summary>
public sealed record SimUnlockOutcome
{
    public required bool IsSuccess { get; init; }
    public required bool IsMockSuccess { get; init; }
    public SimNetworkLockStatus StatusBefore { get; init; }
    public SimNetworkLockStatus StatusAfter { get; init; }
    public required string TicketId { get; init; }
    public DateTimeOffset CompletedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? ErrorMessage { get; init; }
    public required string GuidanceMessage { get; init; }

    public bool Success => IsSuccess;
    public string SummaryReport => GuidanceMessage;
    public SimNetworkLockStatus FinalLockStatus => StatusAfter;
    public bool PostVerificationPassed => IsSuccess && StatusAfter == SimNetworkLockStatus.Unlocked;
    public DateTimeOffset TimestampUtc => CompletedAtUtc;

    public static SimUnlockOutcome Failure(string guidance, string? error = null, SimNetworkLockStatus statusBefore = SimNetworkLockStatus.Unknown, SimNetworkLockStatus statusAfter = SimNetworkLockStatus.Unknown, string ticketId = "NONE") => new()
    {
        IsSuccess = false,
        IsMockSuccess = false,
        StatusBefore = statusBefore,
        StatusAfter = statusAfter,
        TicketId = ticketId,
        ErrorMessage = error ?? guidance,
        GuidanceMessage = guidance
    };

    public static SimUnlockOutcome SuccessOutcome(string guidance, bool isMock = false, SimNetworkLockStatus statusBefore = SimNetworkLockStatus.NetworkLocked, SimNetworkLockStatus statusAfter = SimNetworkLockStatus.Unlocked, string ticketId = "DIRECT-CARRIER-CODE") => new()
    {
        IsSuccess = true,
        IsMockSuccess = isMock,
        StatusBefore = statusBefore,
        StatusAfter = statusAfter,
        TicketId = ticketId,
        GuidanceMessage = guidance
    };
}

/// <summary>
/// Phase 6 persistent audit history record.
/// </summary>
public sealed record SimUnlockAuditRecord
{
    public required string AuditId { get; init; }
    public required string DeviceId { get; init; }
    public required string MaskedImei { get; init; }
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public required bool IsSuccess { get; init; }
    public required bool IsMockSuccess { get; init; }
    public required string TicketId { get; init; }
    public required string Summary { get; init; }
}
