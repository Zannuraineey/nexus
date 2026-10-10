namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Safety and impact classification for device operations.
/// </summary>
public enum OperationRiskClassification
{
    /// <summary>
    /// Non-invasive query or environment inspection without side-effects.
    /// </summary>
    Safe = 0,

    /// <summary>
    /// Read-only device or modem interrogation (e.g. AT query, IMEI read).
    /// </summary>
    Diagnostic = 1,

    /// <summary>
    /// Configuration or partition modification with controlled scope.
    /// </summary>
    Service = 2,

    /// <summary>
    /// High-impact or persistent hardware alteration (e.g. firmware write, NVRAM modification).
    /// </summary>
    Critical = 3
}

/// <summary>
/// Operational readiness state of an installed device module.
/// </summary>
public enum ModuleAvailabilityStatus
{
    Available = 0,
    Disabled = 1,
    MissingDependency = 2,
    Incompatible = 3,
    Degraded = 4
}

/// <summary>
/// Compatibility matrix and target profile declaration for a device module.
/// </summary>
public sealed record ModuleCompatibilityInfo
{
    public IReadOnlyList<string> SupportedManufacturers { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SupportedModels { get; init; } = Array.Empty<string>();
    public string MinEngineVersion { get; init; } = "1.0.0";
    public bool IsSimulationOnly { get; init; } = false;
    public string? Notes { get; init; }
}

/// <summary>
/// Structural metadata and prerequisite contract declared by an operation.
/// </summary>
public sealed record OperationDescriptor
{
    public required string OperationId { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = string.Empty;
    public DeviceCapability RequiredCapabilities { get; init; } = DeviceCapability.None;
    public OperationRiskClassification Risk { get; init; } = OperationRiskClassification.Safe;
    public bool RequiresDeviceConnection { get; init; } = true;
    public bool RequiresUserConfirmation { get; init; } = false;
    public bool SupportsCancellation { get; init; } = true;
}

/// <summary>
/// Diagnostic report item produced during module or registry validation.
/// </summary>
public sealed record ModuleValidationIssue
{
    public required string ModuleId { get; init; }
    public required string Message { get; init; }
    public bool IsFatal { get; init; } = true;
    public string? MissingDependencyId { get; init; }
}
