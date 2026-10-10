using System.Windows;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;

namespace NexusDeviceService.Desktop.Services;

/// <summary>
/// Manages live swapping of theme ResourceDictionaries at runtime without restarting the application.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private readonly IThemePreferenceStore _preferenceStore;

    private const string DarkThemeUri = "pack://application:,,,/NexusDeviceService.Desktop;component/Styles/Themes/Theme.Dark.xaml";
    private const string LightThemeUri = "pack://application:,,,/NexusDeviceService.Desktop;component/Styles/Themes/Theme.Light.xaml";

    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    public event EventHandler<AppTheme>? ThemeChanged;

    public ThemeService(ILogger<ThemeService> logger, IThemePreferenceStore preferenceStore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _preferenceStore = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));
    }

    public void Initialize()
    {
        var preferredTheme = _preferenceStore.GetPreferredTheme();
        _logger.LogInformation("Initializing theme service with preferred theme: {Theme}", preferredTheme);
        ApplyTheme(preferredTheme, notify: false);
    }

    public void SetTheme(AppTheme theme)
    {
        if (CurrentTheme == theme)
        {
            return;
        }

        _logger.LogInformation("Switching application theme to: {Theme}", theme);
        ApplyTheme(theme, notify: true);
        _ = _preferenceStore.SetPreferredThemeAsync(theme);
    }

    public void ToggleTheme()
    {
        var nextTheme = CurrentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        SetTheme(nextTheme);
    }

    private void ApplyTheme(AppTheme theme, bool notify)
    {
        CurrentTheme = theme;

        if (System.Windows.Application.Current == null || System.Windows.Application.Current.Dispatcher.HasShutdownStarted)
        {
            if (notify) ThemeChanged?.Invoke(this, CurrentTheme);
            return;
        }

        void UpdateDictionary()
        {
            var uriString = theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri;
            var newThemeDict = new ResourceDictionary { Source = new Uri(uriString, UriKind.Absolute) };

            var merged = System.Windows.Application.Current.Resources.MergedDictionaries;
            ResourceDictionary? existingThemeDict = null;

            for (int i = 0; i < merged.Count; i++)
            {
                var dict = merged[i];
                if (dict.Source != null && (dict.Source.OriginalString.Contains("Theme.Dark.xaml") || dict.Source.OriginalString.Contains("Theme.Light.xaml")))
                {
                    existingThemeDict = dict;
                    break;
                }
            }

            if (existingThemeDict != null)
            {
                var index = merged.IndexOf(existingThemeDict);
                if (index >= 0)
                {
                    merged[index] = newThemeDict;
                }
                else
                {
                    merged.Insert(0, newThemeDict);
                }
            }
            else
            {
                merged.Insert(0, newThemeDict);
            }
        }

        try
        {
            if (System.Windows.Application.Current.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(UpdateDictionary);
            }
            else
            {
                UpdateDictionary();
            }
        }
        catch (NullReferenceException)
        {
            // Handled for headless testing environments where WPF ThemeManager is uninitialized
        }

        if (notify)
        {
            ThemeChanged?.Invoke(this, CurrentTheme);
        }
    }
}
