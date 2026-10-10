using NexusDeviceService.Application.Services;

namespace NexusDeviceService.Desktop.Services;

/// <summary>
/// Controls application-wide WPF dynamic theme switching at runtime.
/// </summary>
public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    event EventHandler<AppTheme>? ThemeChanged;

    void Initialize();
    void SetTheme(AppTheme theme);
    void ToggleTheme();
}
