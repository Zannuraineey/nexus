using NexusDeviceService.Application.Services;

namespace NexusDeviceService.Infrastructure.Theme;

/// <summary>
/// In-memory implementation of IThemePreferenceStore with default Dark theme.
/// Can be replaced or extended with file/registry/SQLite persistence.
/// </summary>
public sealed class InMemoryThemePreferenceStore : IThemePreferenceStore
{
    private AppTheme _currentTheme = AppTheme.Dark;

    public AppTheme GetPreferredTheme() => _currentTheme;

    public Task SetPreferredThemeAsync(AppTheme theme, CancellationToken cancellationToken = default)
    {
        _currentTheme = theme;
        return Task.CompletedTask;
    }
}
