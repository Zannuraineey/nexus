using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.Services;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.System;

namespace NexusDeviceService.Desktop.ViewModels.Pages;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IDashboardDataProvider _dataProvider;
    private readonly INavigationService _navigationService;
    private readonly ISystemEnvironmentProvider _envProvider;
    private readonly ILogger<DashboardViewModel> _logger;
    private CancellationTokenSource? _activeRefreshCts;

    [ObservableProperty]
    private string _headline = "Service Hub & Device Overview";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isDemonstrationData = false;

    [ObservableProperty]
    private string _quickStatus = "Hardware Engine Active. Ready for mobile diagnostics.";

    [ObservableProperty]
    private DeviceConnectionSummary? _deviceSummary;

    [ObservableProperty]
    private ApplicationStatusInfo? _applicationStatus;

    [ObservableProperty]
    private RecentOperationRecord? _selectedOperation;

    public ObservableCollection<RecentOperationRecord> RecentOperations { get; } = new();
    public ObservableCollection<RecentActivityRecord> RecentActivities { get; } = new();
    public ObservableCollection<DeviceModuleInfo> AvailableModules { get; } = new();
    public ObservableCollection<LatestApplicationEvent> LatestEvents { get; } = new();

    public string HostInfo => $"{_envProvider.MachineName} ({_envProvider.OsDescription})";

    public bool HasOperations => RecentOperations.Count > 0;
    public bool HasNoOperations => !IsLoading && RecentOperations.Count == 0;
    public bool HasActivities => RecentActivities.Count > 0;
    public bool HasNoActivities => !IsLoading && RecentActivities.Count == 0;

    public DashboardViewModel(
        IDashboardDataProvider dataProvider,
        INavigationService navigationService,
        ISystemEnvironmentProvider envProvider,
        ILogger<DashboardViewModel> logger)
    {
        _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _envProvider = envProvider ?? throw new ArgumentNullException(nameof(envProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    public async Task RefreshDashboardAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            _activeRefreshCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if already disposed
        }
        finally
        {
            _activeRefreshCts?.Dispose();
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        _activeRefreshCts = cts;

        IsLoading = true;
        ErrorMessage = null;
        QuickStatus = "Refreshing platform telemetry and operational metrics...";

        try
        {
            var snapshot = await _dataProvider.GetDashboardSnapshotAsync(cts.Token);

            void UpdateCollections()
            {
                DeviceSummary = snapshot.DeviceSummary;
                ApplicationStatus = snapshot.ApplicationStatus;
                IsDemonstrationData = snapshot.IsDemonstrationData;

                RecentOperations.Clear();
                foreach (var op in snapshot.RecentOperations)
                {
                    RecentOperations.Add(op);
                }

                RecentActivities.Clear();
                foreach (var act in snapshot.RecentActivities)
                {
                    RecentActivities.Add(act);
                }

                AvailableModules.Clear();
                foreach (var mod in snapshot.AvailableModules)
                {
                    AvailableModules.Add(mod);
                }

                LatestEvents.Clear();
                foreach (var evt in snapshot.LatestEvents)
                {
                    LatestEvents.Add(evt);
                }
            }

            if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(UpdateCollections);
            }
            else
            {
                UpdateCollections();
            }

            QuickStatus = $"Telemetry synchronized @ {DateTime.Now:HH:mm:ss}. Simulation tier operational.";
            _logger.LogInformation("Dashboard refreshed successfully from data provider");
        }
        catch (OperationCanceledException)
        {
            QuickStatus = "Telemetry refresh cancelled.";
            _logger.LogInformation("Dashboard refresh operation cancelled");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to synchronize dashboard telemetry: {ex.Message}";
            QuickStatus = "Telemetry synchronization failure.";
            _logger.LogError(ex, "Error refreshing dashboard snapshot");
        }
        finally
        {
            IsLoading = false;
            if (_activeRefreshCts == cts)
            {
                _activeRefreshCts = null;
            }
            cts.Dispose();
            OnPropertyChanged(nameof(HasOperations));
            OnPropertyChanged(nameof(HasNoOperations));
            OnPropertyChanged(nameof(HasActivities));
            OnPropertyChanged(nameof(HasNoActivities));
        }
    }

    [RelayCommand]
    public void NavigateTo(string target)
    {
        _logger.LogInformation("Quick action triggered navigation to: {Target}", target);

        switch (target?.Trim().ToLowerInvariant())
        {
            case "devices":
                _navigationService.NavigateTo(NavigationTarget.Devices);
                break;
            case "operations":
                _navigationService.NavigateTo(NavigationTarget.Operations);
                break;
            case "firmware":
                _navigationService.NavigateTo(NavigationTarget.Firmware);
                break;
            case "console":
                _navigationService.NavigateTo(NavigationTarget.Console);
                break;
            case "settings":
                _navigationService.NavigateTo(NavigationTarget.Settings);
                break;
            default:
                _logger.LogWarning("Unknown navigation quick-action requested: {Target}", target);
                break;
        }
    }
}
