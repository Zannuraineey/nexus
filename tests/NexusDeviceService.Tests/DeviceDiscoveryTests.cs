using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.ViewModels.Pages;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Simulated;
using Xunit;

namespace NexusDeviceService.Tests;

public class DeviceDiscoveryTests
{
    private sealed class MockProvider : IDeviceDiscoveryProvider
    {
        public string ProviderId => "mock.provider.test";
        public string DisplayName => "Mock Test Provider";
        public bool IsSimulationProvider => false;

        public List<DeviceDescriptor> DevicesToReturn { get; } = new();
        public bool ShouldThrow { get; set; } = false;

        public Task<IReadOnlyList<DeviceDescriptor>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated hardware bus fault.");
            }

            return Task.FromResult<IReadOnlyList<DeviceDescriptor>>(DevicesToReturn.ToList());
        }
    }

    [Fact]
    public void DeviceDescriptor_ValidatesCapabilitiesAndMetadata()
    {
        var descriptor = new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier
            {
                VendorId = "0x1234",
                ProductId = "0x5678",
                SerialNumber = "SN-TEST-001",
                PortName = "COM7"
            },
            Manufacturer = "TestManufacturer",
            Model = "TestModem-Pro",
            ChipsetFamily = "TestChip",
            HardwareRevision = "V1.0",
            FirmwareVersion = "FW-1.0.0",
            InterfaceType = DeviceInterfaceType.SerialCom,
            ProviderId = "test.provider",
            SupportedCapabilities = DeviceCapability.InformationReading | DeviceCapability.AtCommandSession,
            IsSimulation = true
        };

        Assert.True(descriptor.Supports(DeviceCapability.InformationReading));
        Assert.True(descriptor.Supports(DeviceCapability.AtCommandSession));
        Assert.False(descriptor.Supports(DeviceCapability.AuthorizedSimUnlock));
        Assert.Equal("TestManufacturer TestModem-Pro", descriptor.DisplayName);
        Assert.True(descriptor.IsSimulation);
    }

    [Fact]
    public async Task DiscoveryService_CoordinatesMultipleProviders_AggregatingResults()
    {
        var provider1 = new MockProvider();
        provider1.DevicesToReturn.Add(new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x1", ProductId = "0x1", SerialNumber = "DEV-01", PortName = "COM1" },
            ProviderId = "p1"
        });

        var provider2 = new MockProvider();
        provider2.DevicesToReturn.Add(new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x2", ProductId = "0x2", SerialNumber = "DEV-02", PortName = "COM2" },
            ProviderId = "p2"
        });

        var service = new DeviceDiscoveryService(new[] { provider1, provider2 }, NullLogger<DeviceDiscoveryService>.Instance);

        var eventFired = false;
        service.DevicesDiscovered += (s, devices) => eventFired = true;

        var results = await service.DiscoverAllDevicesAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(2, service.LastDiscoveredDevices.Count);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task DiscoveryService_HandlesProviderException_Resiliently()
    {
        var failingProvider = new MockProvider { ShouldThrow = true };
        var workingProvider = new MockProvider();
        workingProvider.DevicesToReturn.Add(new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x3", ProductId = "0x3", SerialNumber = "DEV-03" },
            ProviderId = "p3"
        });

        var service = new DeviceDiscoveryService(new[] { failingProvider, workingProvider }, NullLogger<DeviceDiscoveryService>.Instance);

        var results = await service.DiscoverAllDevicesAsync();

        Assert.Single(results);
        Assert.Equal("DEV-03", results[0].Identifier.SerialNumber);
    }

    [Fact]
    public async Task DiscoveryService_RespectsCancellation()
    {
        var provider = new MockProvider();
        var service = new DeviceDiscoveryService(new[] { provider }, NullLogger<DeviceDiscoveryService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await service.DiscoverAllDevicesAsync(cts.Token);
        });
    }

    [Fact]
    public async Task SimulatedProvider_ReturnsValidSimulatedProfiles()
    {
        var provider = new SimulatedDeviceDiscoveryProvider(NullLogger<SimulatedDeviceDiscoveryProvider>.Instance)
        {
            SimulatedDelayMs = 0
        };

        var devices = await provider.DiscoverDevicesAsync();

        Assert.NotEmpty(devices);
        Assert.All(devices, d =>
        {
            Assert.True(d.IsSimulation);
            Assert.Contains("Simulated", d.Manufacturer, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(d.Identifier.SerialNumber);
        });

        // Test Empty Mode
        provider.SimulateEmptyMode = true;
        var emptyDevices = await provider.DiscoverDevicesAsync();
        Assert.Empty(emptyDevices);
    }

    [Fact]
    public async Task DevicesViewModel_HandlesFilteringAndSelection()
    {
        var provider = new MockProvider();
        provider.DevicesToReturn.Add(new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x1111", ProductId = "0x2222", SerialNumber = "SN-A", PortName = "COM3" },
            Manufacturer = "Qualcomm",
            Model = "Snapdragon Modem",
            ChipsetFamily = "SDX65",
            InterfaceType = DeviceInterfaceType.SerialCom,
            ProviderId = "mock",
            SupportedCapabilities = DeviceCapability.InformationReading | DeviceCapability.AtCommandSession
        });
        provider.DevicesToReturn.Add(new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x3333", ProductId = "0x4444", SerialNumber = "SN-B", PortName = "USB-1" },
            Manufacturer = "MediaTek",
            Model = "Dimensity Board",
            ChipsetFamily = "MT6789",
            InterfaceType = DeviceInterfaceType.Usb,
            ProviderId = "mock",
            SupportedCapabilities = DeviceCapability.FirmwareValidation
        });

        var discoveryService = new DeviceDiscoveryService(new[] { provider }, NullLogger<DeviceDiscoveryService>.Instance);
        var vm = new DevicesViewModel(discoveryService, NullLogger<DevicesViewModel>.Instance);

        // Await refresh
        await vm.RefreshDevicesAsync();

        Assert.Equal(2, vm.AllDevices.Count);
        Assert.Equal(2, vm.FilteredDevices.Count);
        Assert.True(vm.HasDevices);
        Assert.False(vm.HasNoDevices);
        Assert.NotNull(vm.SelectedDevice);

        // Test Text Filter
        vm.SearchQuery = "MediaTek";
        Assert.Single(vm.FilteredDevices);
        Assert.Equal("MediaTek", vm.FilteredDevices[0].Manufacturer);

        // Test Interface Filter
        vm.SearchQuery = string.Empty;
        vm.SelectedInterfaceFilter = "Serial COM";
        Assert.Single(vm.FilteredDevices);
        Assert.Equal(DeviceInterfaceType.SerialCom, vm.FilteredDevices[0].InterfaceType);

        // Test Empty Filter Match
        vm.SearchQuery = "NonExistentDevice";
        Assert.Empty(vm.FilteredDevices);
        Assert.False(vm.HasDevices);
        Assert.True(vm.HasNoDevices);

        // Clear Filter
        vm.ClearFilter();
        Assert.Equal(2, vm.FilteredDevices.Count);
    }
}
