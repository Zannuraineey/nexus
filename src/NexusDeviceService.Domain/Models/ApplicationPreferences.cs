namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Strongly-typed user and platform preferences persisted across application restarts.
/// </summary>
public sealed record ApplicationPreferences
{
    public string Theme { get; init; } = "Dark";
    public string LanguageCode { get; init; } = "en-US";
    public int LogRetentionDays { get; init; } = 7;
    public string MinimumLogLevel { get; init; } = "Information";
    public string DefaultPage { get; init; } = "Dashboard";
    public bool RequireConfirmationForDestructiveOperations { get; init; } = true;
    public bool EnableDetailedDiagnostics { get; init; } = true;
    public string PreferredBaudRate { get; init; } = "115200";
    public bool SimulationModeEnabled { get; init; } = true;
    public DateTime LastModified { get; init; } = DateTime.UtcNow;
}
