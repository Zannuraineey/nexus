using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.Services;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Desktop.ViewModels.Pages;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IThemeService _themeService;
    private readonly IPreferenceStore _preferenceStore;

    [ObservableProperty]
    private string _selectedTheme = "Dark";

    [ObservableProperty]
    private string _selectedLanguage = "English (en-US)";

    [ObservableProperty]
    private int _logRetentionDays = 7;

    [ObservableProperty]
    private string _selectedLogLevel = "Information";

    [ObservableProperty]
    private string _defaultPage = "Dashboard";

    [ObservableProperty]
    private bool _requireConfirmation = true;

    [ObservableProperty]
    private bool _enableDetailedDiagnostics = true;

    [ObservableProperty]
    private string _selectedBaudRate = "115200";

    [ObservableProperty]
    private bool _simulationModeEnabled = true;

    [ObservableProperty]
    private string? _statusFeedback;

    public ObservableCollection<string> AvailableThemes { get; } = new() { "Dark", "Light" };
    public ObservableCollection<string> AvailableLanguages { get; } = new() { "English (en-US)", "Spanish (es-ES)", "French (fr-FR)", "German (de-DE)" };
    public ObservableCollection<int> AvailableRetentionDays { get; } = new() { 1, 3, 7, 14, 30 };
    public ObservableCollection<string> AvailableLogLevels { get; } = new() { "Trace", "Debug", "Information", "Warning", "Error" };
    public ObservableCollection<string> AvailableDefaultPages { get; } = new() { "Dashboard", "Devices", "Operations", "Console", "History" };
    public ObservableCollection<string> AvailableBaudRates { get; } = new() { "9600", "19200", "38400", "57600", "115200", "921600" };

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        IThemeService themeService,
        IPreferenceStore preferenceStore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _preferenceStore = preferenceStore ?? throw new ArgumentNullException(nameof(preferenceStore));

        // Load existing saved preferences asynchronously
        _ = LoadPreferencesAsync();
    }

    public async Task LoadPreferencesAsync()
    {
        try
        {
            var prefs = await _preferenceStore.LoadPreferencesAsync();
            SelectedTheme = prefs.Theme;
            SelectedLanguage = prefs.LanguageCode switch
            {
                "es-ES" => "Spanish (es-ES)",
                "fr-FR" => "French (fr-FR)",
                "de-DE" => "German (de-DE)",
                _ => "English (en-US)"
            };
            LogRetentionDays = prefs.LogRetentionDays;
            SelectedLogLevel = prefs.MinimumLogLevel;
            DefaultPage = prefs.DefaultPage;
            RequireConfirmation = prefs.RequireConfirmationForDestructiveOperations;
            EnableDetailedDiagnostics = prefs.EnableDetailedDiagnostics;
            SelectedBaudRate = prefs.PreferredBaudRate;
            SimulationModeEnabled = prefs.SimulationModeEnabled;
            StatusFeedback = $"Preferences loaded @ {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load preferences on Settings view initialization");
            StatusFeedback = "Failed to load preferences; using defaults.";
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        if (Enum.TryParse<AppTheme>(value, out var theme) && theme != _themeService.CurrentTheme)
        {
            _themeService.SetTheme(theme);
            _logger.LogInformation("Theme changed to {Theme} via Settings", theme);
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        try
        {
            var langCode = SelectedLanguage switch
            {
                "Spanish (es-ES)" => "es-ES",
                "French (fr-FR)" => "fr-FR",
                "German (de-DE)" => "de-DE",
                _ => "en-US"
            };

            var prefs = new ApplicationPreferences
            {
                Theme = SelectedTheme,
                LanguageCode = langCode,
                LogRetentionDays = LogRetentionDays,
                MinimumLogLevel = SelectedLogLevel,
                DefaultPage = DefaultPage,
                RequireConfirmationForDestructiveOperations = RequireConfirmation,
                EnableDetailedDiagnostics = EnableDetailedDiagnostics,
                PreferredBaudRate = SelectedBaudRate,
                SimulationModeEnabled = SimulationModeEnabled
            };

            await _preferenceStore.SavePreferencesAsync(prefs);
            StatusFeedback = $"Settings saved successfully @ {DateTime.Now:HH:mm:ss}.";
            _logger.LogInformation("User settings saved to local preference store.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist user preferences");
            StatusFeedback = $"Save failed: {ex.Message}";
        }
    }
}
