using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.ViewModels;
using NexusDeviceService.Infrastructure.Extensions;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.System;
using Xunit;

namespace NexusDeviceService.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void CompositionRoot_ResolvesAllCoreServices()
    {
        var services = new ServiceCollection();

        services.AddNexusServices();
        services.AddSingleton<NexusDeviceService.Desktop.Services.IThemeService, NexusDeviceService.Desktop.Services.ThemeService>();
        services.AddSingleton<NexusDeviceService.Desktop.Services.INavigationService, NexusDeviceService.Desktop.Services.NavigationService>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.DashboardViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.DevicesViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.OperationsViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.FirmwareViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.ConsoleViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.HistoryViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.SettingsViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.AboutViewModel>();
        services.AddSingleton<MainViewModel>();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IThemePreferenceStore>());
        Assert.NotNull(provider.GetService<NexusDeviceService.Desktop.Services.IThemeService>());
        Assert.NotNull(provider.GetService<NexusDeviceService.Desktop.Services.INavigationService>());
        Assert.NotNull(provider.GetService<NexusDeviceService.Desktop.ViewModels.Pages.DashboardViewModel>());
        Assert.NotNull(provider.GetService<IAppLogSink>());
        Assert.NotNull(provider.GetService<ISystemEnvironmentProvider>());
        Assert.NotNull(provider.GetService<IDeviceSessionManager>());
        Assert.NotNull(provider.GetService<IOperationOrchestrator>());
        Assert.NotNull(provider.GetService<ILogger<MainViewModel>>());
        Assert.NotNull(provider.GetService<MainViewModel>());
    }

    [Fact]
    public void LogSink_BuffersAndEmitsLogs()
    {
        var sink = new InMemoryLogSink(50);
        var received = false;

        sink.LogReceived += (s, e) => received = true;

        sink.Emit(new AppLogEntry(DateTime.UtcNow, LogLevel.Information, "Test", "Sample log message"));

        Assert.True(received);
        Assert.Single(sink.GetRecentLogs());
        Assert.Equal("Sample log message", sink.GetRecentLogs()[0].Message);
    }
}
