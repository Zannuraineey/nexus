using CommunityToolkit.Mvvm.ComponentModel;

namespace NexusDeviceService.Desktop.Services;

public enum NavigationTarget
{
    Dashboard,
    Devices,
    Operations,
    Firmware,
    Console,
    History,
    Settings,
    About
}

/// <summary>
/// Controls shell view navigation and manages the active ViewModel.
/// </summary>
public interface INavigationService
{
    NavigationTarget CurrentTarget { get; }
    ObservableObject? CurrentViewModel { get; }
    event EventHandler<NavigationTarget>? Navigated;

    void NavigateTo(NavigationTarget target);
}
