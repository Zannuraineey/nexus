using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.Services;
using NexusDeviceService.Desktop.ViewModels.Pages;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.Simulated;
using NexusDeviceService.Infrastructure.System;
using Xunit;

namespace NexusDeviceService.Tests;

public class DashboardTests
{
    private sealed class MockNavigationService : INavigationService
    {
        public NavigationTarget CurrentTarget { get; private set; } = NavigationTarget.Dashboard;
        public CommunityToolkit.Mvvm.ComponentModel.ObservableObject? CurrentViewModel => null;
        public event EventHandler<NavigationTarget>? Navigated;

        public List<NavigationTarget> NavigationHistory { get; } = new();

        public void NavigateTo(NavigationTarget target)
        {
            CurrentTarget = target;
            NavigationHistory.Add(target);
            Navigated?.Invoke(this, target);
        }
    }

    private sealed class MockDashboardDataProvider : IDashboardDataProvider
    {
        public bool ShouldThrow { get; set; } = false;
        public DashboardSnapshot? SnapshotToReturn { get; set; }

        public Task<DashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated data provider connection fault.");
            }

            return Task.FromResult(SnapshotToReturn ?? CreateDefaultSnapshot());
        }

        public static DashboardSnapshot CreateDefaultSnapshot(bool emptyOperations = false, bool emptyActivities = false)
        {
            return new DashboardSnapshot
            {
                DeviceSummary = new DeviceConnectionSummary
                {
                    TotalConnectedCount = 2,
                    SimulatedCount = 2,
                    PhysicalCount = 0,
                    ActiveInterfaceOrPort = "COM3",
                    PrimaryDeviceDisplayName = "Simulated Modem"
                },
                RecentOperations = emptyOperations ? Array.Empty<RecentOperationRecord>() : new[]
                {
                    new RecentOperationRecord
                    {
                        Id = "OP-1",
                        OperationName = "[SIMULATED] AT Query",
                        TargetDevice = "Modem",
                        Status = OperationStatus.Completed,
                        Timestamp = DateTime.UtcNow,
                        DurationText = "500ms",
                        IsSimulated = true
                    }
                },
                RecentActivities = emptyActivities ? Array.Empty<RecentActivityRecord>() : new[]
                {
                    new RecentActivityRecord
                    {
                        Id = "ACT-1",
                        Timestamp = DateTime.UtcNow,
                        Description = "Event description",
                        Category = "Test",
                        Severity = "Info"
                    }
                },
                AvailableModules = new[]
                {
                    new DeviceModuleInfo
                    {
                        ModuleName = "Test Module",
                        Category = "Protocol",
                        IsReady = true,
                        Description = "Test desc",
                        DriverVersion = "v1.0",
                        IsSimulated = true
                    }
                },
                ApplicationStatus = new ApplicationStatusInfo
                {
                    EngineVersion = "v1.0.0",
                    RuntimeFramework = "net10.0",
                    DatabaseStatus = "Memory",
                    StationProfile = "Lab Station",
                    IsSimulationActive = true,
                    ActiveLogSinkEntries = 5
                },
                LatestEvents = new[]
                {
                    new LatestApplicationEvent
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Info",
                        Source = "Test",
                        Message = "Test message"
                    }
                },
                IsDemonstrationData = true
            };
        }
    }

    [Fact]
    public async Task DemoDashboardDataProvider_ReturnsPopulatedDemonstrationSnapshot()
    {
        var discoveryProvider = new SimulatedDeviceDiscoveryProvider(NullLogger<SimulatedDeviceDiscoveryProvider>.Instance);
        var discoveryService = new DeviceDiscoveryService(new[] { discoveryProvider }, NullLogger<DeviceDiscoveryService>.Instance);
        var env = new SystemEnvironmentProvider();
        var sink = new InMemoryLogSink();

        var demoProvider = new DemoDashboardDataProvider(discoveryService, env, sink, NullLogger<DemoDashboardDataProvider>.Instance);

        var snapshot = await demoProvider.GetDashboardSnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsDemonstrationData);
        Assert.True(snapshot.RecentOperations.Count > 0);
        Assert.All(snapshot.RecentOperations, op => Assert.True(op.IsSimulated));
        Assert.True(snapshot.AvailableModules.Count > 0);
        Assert.All(snapshot.AvailableModules, mod => Assert.True(mod.IsSimulated));
        Assert.True(snapshot.DeviceSummary.SimulatedCount > 0);
        Assert.True(snapshot.ApplicationStatus.IsSimulationActive);
    }

    [Fact]
    public async Task DemoDashboardDataProvider_RespectsEmptyFlags()
    {
        var discoveryProvider = new SimulatedDeviceDiscoveryProvider(NullLogger<SimulatedDeviceDiscoveryProvider>.Instance);
        var discoveryService = new DeviceDiscoveryService(new[] { discoveryProvider }, NullLogger<DeviceDiscoveryService>.Instance);
        var env = new SystemEnvironmentProvider();
        var sink = new InMemoryLogSink();

        var demoProvider = new DemoDashboardDataProvider(discoveryService, env, sink, NullLogger<DemoDashboardDataProvider>.Instance)
        {
            SimulateEmptyOperations = true,
            SimulateEmptyActivities = true
        };

        var snapshot = await demoProvider.GetDashboardSnapshotAsync();

        Assert.Empty(snapshot.RecentOperations);
        Assert.Empty(snapshot.RecentActivities);
    }

    [Fact]
    public async Task DashboardViewModel_LoadsSnapshotAndBindsProperties()
    {
        var mockDataProvider = new MockDashboardDataProvider();
        var mockNav = new MockNavigationService();
        var env = new SystemEnvironmentProvider();

        var vm = new DashboardViewModel(mockDataProvider, mockNav, env, NullLogger<DashboardViewModel>.Instance);

        await vm.RefreshDashboardAsync();

        Assert.NotNull(vm.DeviceSummary);
        Assert.Equal(2, vm.DeviceSummary.TotalConnectedCount);
        Assert.True(vm.IsDemonstrationData);
        Assert.Single(vm.RecentOperations);
        Assert.True(vm.HasOperations);
        Assert.False(vm.HasNoOperations);
        Assert.Single(vm.RecentActivities);
        Assert.True(vm.HasActivities);
        Assert.False(vm.HasNoActivities);
        Assert.Single(vm.AvailableModules);
        Assert.Single(vm.LatestEvents);
        Assert.False(vm.IsLoading);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task DashboardViewModel_EvaluatesEmptyStatesCorrectly()
    {
        var mockDataProvider = new MockDashboardDataProvider
        {
            SnapshotToReturn = MockDashboardDataProvider.CreateDefaultSnapshot(emptyOperations: true, emptyActivities: true)
        };
        var mockNav = new MockNavigationService();
        var env = new SystemEnvironmentProvider();

        var vm = new DashboardViewModel(mockDataProvider, mockNav, env, NullLogger<DashboardViewModel>.Instance);

        await vm.RefreshDashboardAsync();

        Assert.Empty(vm.RecentOperations);
        Assert.False(vm.HasOperations);
        Assert.True(vm.HasNoOperations);

        Assert.Empty(vm.RecentActivities);
        Assert.False(vm.HasActivities);
        Assert.True(vm.HasNoActivities);
    }

    [Fact]
    public void DashboardViewModel_QuickActionsNavigateWithoutHardwareOperations()
    {
        var mockDataProvider = new MockDashboardDataProvider();
        var mockNav = new MockNavigationService();
        var env = new SystemEnvironmentProvider();

        var vm = new DashboardViewModel(mockDataProvider, mockNav, env, NullLogger<DashboardViewModel>.Instance);

        vm.NavigateTo("devices");
        Assert.Equal(NavigationTarget.Devices, mockNav.CurrentTarget);

        vm.NavigateTo("operations");
        Assert.Equal(NavigationTarget.Operations, mockNav.CurrentTarget);

        vm.NavigateTo("firmware");
        Assert.Equal(NavigationTarget.Firmware, mockNav.CurrentTarget);

        vm.NavigateTo("console");
        Assert.Equal(NavigationTarget.Console, mockNav.CurrentTarget);

        vm.NavigateTo("settings");
        Assert.Equal(NavigationTarget.Settings, mockNav.CurrentTarget);

        Assert.Equal(5, mockNav.NavigationHistory.Count);
    }

    [Fact]
    public async Task DashboardViewModel_HandlesProviderFailureGracefully()
    {
        var mockDataProvider = new MockDashboardDataProvider { ShouldThrow = true };
        var mockNav = new MockNavigationService();
        var env = new SystemEnvironmentProvider();

        var vm = new DashboardViewModel(mockDataProvider, mockNav, env, NullLogger<DashboardViewModel>.Instance);

        await vm.RefreshDashboardAsync();

        Assert.False(vm.IsLoading);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("Failed to synchronize", vm.ErrorMessage);
    }
}
