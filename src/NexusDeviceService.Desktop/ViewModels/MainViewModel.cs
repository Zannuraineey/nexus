using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.Services;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.System;

namespace NexusDeviceService.Desktop.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ILogger<MainViewModel> _logger;
    private readonly ISystemEnvironmentProvider _environmentProvider;
    private readonly IThemeService _themeService;
    private readonly INavigationService _navigationService;
    private readonly IAppLogSink _logSink;

    [ObservableProperty]
    private string _title = "Nexus Device Service — Professional Hardware & Diagnostic Suite";

    [ObservableProperty]
    private string _version = "v1.0.0 Professional Edition";

    [ObservableProperty]
    private string _statusMessage = "Application Shell Ready. Technician Workstation Active.";

    [ObservableProperty]
    private bool _isSimulationMode = true;

    [ObservableProperty]
    private string _modeBadgeText = "SIMULATION ACTIVE";

    [ObservableProperty]
    private string _connectionStatus = "SIMULATION (Ready)";

    [ObservableProperty]
    private string _technicianName = "Technician Lab Station (Offline)";

    [ObservableProperty]
    private string _accountRole = "Licensed Workstation";

    [ObservableProperty]
    private AppTheme _currentTheme;

    [ObservableProperty]
    private string _themeButtonLabel = "Light Mode";

    [ObservableProperty]
    private NavigationTarget _activeTarget;

    [ObservableProperty]
    private ObservableObject? _currentPageViewModel;

    [ObservableProperty]
    private bool _isConsoleExpanded = false;

    public ObservableCollection<AppLogEntry> BottomConsoleLogs { get; } = new();

    public string OsDescription => _environmentProvider.OsDescription;
    public string RuntimeDescription => _environmentProvider.RuntimeVersion;
    public string MachineName => _environmentProvider.MachineName;

    public MainViewModel(
        ILogger<MainViewModel> logger,
        ISystemEnvironmentProvider environmentProvider,
        IThemeService themeService,
        INavigationService navigationService,
        IAppLogSink logSink,
        IDeviceDiscoveryService discoveryService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environmentProvider = environmentProvider ?? throw new ArgumentNullException(nameof(environmentProvider));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));

        discoveryService.DevicesDiscovered += (s, devices) =>
        {
            var physicalCount = devices.Count(d => !d.IsSimulation);
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (physicalCount > 0)
                {
                    IsSimulationMode = false;
                    ModeBadgeText = $"HARDWARE ATTACHED ({physicalCount})";
                    ConnectionStatus = $"HARDWARE ONLINE ({physicalCount})";
                }
                else
                {
                    IsSimulationMode = true;
                    ModeBadgeText = "SIMULATION ACTIVE";
                    ConnectionStatus = "SIMULATION (Ready)";
                }
            });
        };

        _currentTheme = _themeService.CurrentTheme;
        UpdateThemeLabel();

        _activeTarget = _navigationService.CurrentTarget;
        _currentPageViewModel = _navigationService.CurrentViewModel;

        _themeService.ThemeChanged += (s, theme) =>
        {
            CurrentTheme = theme;
            UpdateThemeLabel();
            StatusMessage = $"Switched application theme to {theme}";
        };

        _navigationService.Navigated += (s, target) =>
        {
            ActiveTarget = target;
            CurrentPageViewModel = _navigationService.CurrentViewModel;
            StatusMessage = $"Active view: {target}";
        };

        // Populate initial bottom console logs
        foreach (var log in _logSink.GetRecentLogs().TakeLast(20))
        {
            BottomConsoleLogs.Add(log);
        }

        _logSink.LogReceived += OnLogReceived;

        _logger.LogInformation("Nexus Device Service Shell initialized on {OS}", _environmentProvider.OsDescription);
    }

    private void OnLogReceived(object? sender, AppLogEntry entry)
    {
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            BottomConsoleLogs.Add(entry);
            while (BottomConsoleLogs.Count > 30)
            {
                BottomConsoleLogs.RemoveAt(0);
            }
        });
    }

    private void UpdateThemeLabel()
    {
        ThemeButtonLabel = CurrentTheme == AppTheme.Dark ? "Light Mode" : "Dark Mode";
    }

    [RelayCommand]
    private void Navigate(string targetName)
    {
        if (Enum.TryParse<NavigationTarget>(targetName, out var target))
        {
            _navigationService.NavigateTo(target);
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
    }

    [RelayCommand]
    private void ToggleConsole()
    {
        IsConsoleExpanded = !IsConsoleExpanded;
    }

    [RelayCommand]
    private void ClearBottomConsole()
    {
        BottomConsoleLogs.Clear();
    }
}
