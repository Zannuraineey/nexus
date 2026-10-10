namespace NexusDeviceService.Application.Services;

public enum AppTheme
{
    Dark,
    Light
}

/// <summary>
/// Persistence contract for storing user theme preference.
/// </summary>
public interface IThemePreferenceStore
{
    AppTheme GetPreferredTheme();
    Task SetPreferredThemeAsync(AppTheme theme, CancellationToken cancellationToken = default);
}
