namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Aggregated summary of connected physical and virtual communication channels.
/// </summary>
public sealed record DeviceConnectionSummary
{
    public int TotalConnectedCount { get; init; }
    public int SimulatedCount { get; init; }
    public int PhysicalCount { get; init; }
    public string? ActiveInterfaceOrPort { get; init; }
    public string? PrimaryDeviceDisplayName { get; init; }
    public bool HasActiveConnection => TotalConnectedCount > 0;
}

/// <summary>
/// Record representing an executed or simulated mobile device service operation.
/// </summary>
public sealed record RecentOperationRecord
{
    public required string Id { get; init; }
    public required string OperationName { get; init; }
    public required string TargetDevice { get; init; }
    public OperationStatus Status { get; init; }
    public DateTime Timestamp { get; init; }
    public required string DurationText { get; init; }
    public bool IsSimulated { get; init; } = true;
}

/// <summary>
/// Operational activity and telemetry event record for the activity feed.
/// </summary>
public sealed record RecentActivityRecord
{
    public required string Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public string Severity { get; init; } = "Info";
}

/// <summary>
/// Status and readiness descriptor for a device protocol engine or hardware driver module.
/// </summary>
public sealed record DeviceModuleInfo
{
    public required string ModuleName { get; init; }
    public required string Category { get; init; }
    public bool IsReady { get; init; }
    public required string Description { get; init; }
    public required string DriverVersion { get; init; }
    public bool IsSimulated { get; init; } = true;
}

/// <summary>
/// Application platform diagnostics and environment status.
/// </summary>
public sealed record ApplicationStatusInfo
{
    public required string EngineVersion { get; init; }
    public required string RuntimeFramework { get; init; }
    public required string DatabaseStatus { get; init; }
    public required string StationProfile { get; init; }
    public bool IsSimulationActive { get; init; } = true;
    public int ActiveLogSinkEntries { get; init; }
}

/// <summary>
/// Recent structured diagnostic event logged by the application runtime.
/// </summary>
public sealed record LatestApplicationEvent
{
    public DateTime Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Source { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Comprehensive dashboard telemetry snapshot aggregating all platform modules.
/// </summary>
public sealed record DashboardSnapshot
{
    public required DeviceConnectionSummary DeviceSummary { get; init; }
    public required IReadOnlyList<RecentOperationRecord> RecentOperations { get; init; }
    public required IReadOnlyList<RecentActivityRecord> RecentActivities { get; init; }
    public required IReadOnlyList<DeviceModuleInfo> AvailableModules { get; init; }
    public required ApplicationStatusInfo ApplicationStatus { get; init; }
    public required IReadOnlyList<LatestApplicationEvent> LatestEvents { get; init; }
    public bool IsDemonstrationData { get; init; } = true;
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}
