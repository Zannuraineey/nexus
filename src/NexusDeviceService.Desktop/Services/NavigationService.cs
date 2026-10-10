using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Desktop.ViewModels.Pages;

namespace NexusDeviceService.Desktop.Services;

/// <summary>
/// Manages application shell navigation and resolves page ViewModels.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly ILogger<NavigationService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<NavigationTarget, ObservableObject> _viewModelCache = new();

    public NavigationTarget CurrentTarget { get; private set; } = NavigationTarget.Dashboard;
    
    private ObservableObject? _currentViewModel;
    public ObservableObject? CurrentViewModel => _currentViewModel ??= ResolveViewModel(CurrentTarget);

    public event EventHandler<NavigationTarget>? Navigated;

    public NavigationService(ILogger<NavigationService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    private ObservableObject ResolveViewModel(NavigationTarget target)
    {
        if (!_viewModelCache.TryGetValue(target, out var vm))
        {
            vm = target switch
            {
                NavigationTarget.Dashboard => _serviceProvider.GetRequiredService<DashboardViewModel>(),
                NavigationTarget.Devices => _serviceProvider.GetRequiredService<DevicesViewModel>(),
                NavigationTarget.Operations => _serviceProvider.GetRequiredService<OperationsViewModel>(),
                NavigationTarget.Firmware => _serviceProvider.GetRequiredService<FirmwareViewModel>(),
                NavigationTarget.Console => _serviceProvider.GetRequiredService<ConsoleViewModel>(),
                NavigationTarget.History => _serviceProvider.GetRequiredService<HistoryViewModel>(),
                NavigationTarget.Settings => _serviceProvider.GetRequiredService<SettingsViewModel>(),
                NavigationTarget.About => _serviceProvider.GetRequiredService<AboutViewModel>(),
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
            };

            _viewModelCache[target] = vm;
        }

        return vm;
    }

    public void NavigateTo(NavigationTarget target)
    {
        if (CurrentTarget == target && _currentViewModel != null)
        {
            return;
        }

        _logger.LogInformation("Navigating to destination: {Target}", target);

        var vm = ResolveViewModel(target);

        CurrentTarget = target;
        _currentViewModel = vm;
        Navigated?.Invoke(this, target);
    }
}
