using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.Converters;
using NexusDeviceService.Desktop.Services;
using NexusDeviceService.Desktop.ViewModels.Pages;
using NexusDeviceService.Infrastructure.Extensions;
using Xunit;

namespace NexusDeviceService.Tests;

public class NavigationServiceTests
{
    private static IServiceProvider CreateTestServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddNexusServices();

        // Presentation
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<INavigationService, NavigationService>();

        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<DevicesViewModel>();
        services.AddSingleton<OperationsViewModel>();
        services.AddSingleton<FirmwareViewModel>();
        services.AddSingleton<ConsoleViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void NavigationService_InitializesAtDashboard()
    {
        var sp = CreateTestServiceProvider();
        var nav = sp.GetRequiredService<INavigationService>();

        Assert.Equal(NavigationTarget.Dashboard, nav.CurrentTarget);
        Assert.IsType<DashboardViewModel>(nav.CurrentViewModel);
    }

    [Theory]
    [InlineData(NavigationTarget.Dashboard, typeof(DashboardViewModel))]
    [InlineData(NavigationTarget.Devices, typeof(DevicesViewModel))]
    [InlineData(NavigationTarget.Operations, typeof(OperationsViewModel))]
    [InlineData(NavigationTarget.Firmware, typeof(FirmwareViewModel))]
    [InlineData(NavigationTarget.Console, typeof(ConsoleViewModel))]
    [InlineData(NavigationTarget.History, typeof(HistoryViewModel))]
    [InlineData(NavigationTarget.Settings, typeof(SettingsViewModel))]
    [InlineData(NavigationTarget.About, typeof(AboutViewModel))]
    public void NavigationService_NavigatesToAllDestinations(NavigationTarget target, Type expectedViewModelType)
    {
        var sp = CreateTestServiceProvider();
        var nav = sp.GetRequiredService<INavigationService>();

        NavigationTarget? lastEventTarget = null;
        nav.Navigated += (s, t) => lastEventTarget = t;

        nav.NavigateTo(target);

        Assert.Equal(target, nav.CurrentTarget);
        Assert.NotNull(nav.CurrentViewModel);
        Assert.IsType(expectedViewModelType, nav.CurrentViewModel);

        if (target != NavigationTarget.Dashboard) // Dashboard is initial, so event fires for subsequent
        {
            Assert.Equal(target, lastEventTarget);
        }
    }

    [Fact]
    public void NavigationService_ReusesCachedViewModelsWithoutDuplicates()
    {
        var sp = CreateTestServiceProvider();
        var nav = sp.GetRequiredService<INavigationService>();

        nav.NavigateTo(NavigationTarget.Devices);
        var instance1 = nav.CurrentViewModel;

        nav.NavigateTo(NavigationTarget.Settings);
        nav.NavigateTo(NavigationTarget.Devices);
        var instance2 = nav.CurrentViewModel;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void NavigationConverter_EvaluatesTargetCorrectly()
    {
        var converter = new NavigationTargetToBooleanConverter();

        var isDashboard = (bool)converter.Convert(NavigationTarget.Dashboard, typeof(bool), "Dashboard", null!);
        var isDevices = (bool)converter.Convert(NavigationTarget.Dashboard, typeof(bool), "Devices", null!);

        Assert.True(isDashboard);
        Assert.False(isDevices);
    }
}
