using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.Windows;
using Xunit;

namespace NexusDeviceService.Tests;

public class WindowsDeviceDiscoveryTests
{
    private sealed class MockWindowsDeviceEnumerator : IWindowsDeviceEnumerator
    {
        public List<RawSerialPortInfo> ComPorts { get; set; } = new();
        public List<RawUsbDeviceInfo> UsbDevices { get; set; } = new();

        public Task<IReadOnlyList<RawSerialPortInfo>> EnumerateActiveComPortsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RawSerialPortInfo>>(ComPorts);
        }

        public Task<IReadOnlyList<RawUsbDeviceInfo>> EnumerateUsbDevicesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RawUsbDeviceInfo>>(UsbDevices);
        }
    }

    [Fact]
    public async Task DiscoverDevicesAsync_GroupsCompositeUsbInterfaces_UnderSinglePhysicalDevice()
    {
        // Arrange
        var mockEnumerator = new MockWindowsDeviceEnumerator();
        const string sharedContainer = "{4F77B08B-8D7D-4C67-BAA9-A9711674DB93}";

        mockEnumerator.UsbDevices.Add(new RawUsbDeviceInfo(
            DeviceInstanceId: @"USB\VID_05C6&PID_9001&MI_00\6&1234567&0&0000",
            HardwareId: @"USB\VID_05C6&PID_9001&REV_0100&MI_00",
            FriendlyName: "Qualcomm HS-USB Diagnostics 9001",
            DeviceDesc: "Qualcomm USB Diagnostics Port",
            ContainerId: sharedContainer,
            Service: "qcusbser",
            PortName: "COM3",
            ParentInstanceId: "12345678"
        ));

        mockEnumerator.UsbDevices.Add(new RawUsbDeviceInfo(
            DeviceInstanceId: @"USB\VID_05C6&PID_9001&MI_01\6&1234567&0&0001",
            HardwareId: @"USB\VID_05C6&PID_9001&REV_0100&MI_01",
            FriendlyName: "Qualcomm HS-USB NMEA 9001",
            DeviceDesc: "Qualcomm NMEA Port",
            ContainerId: sharedContainer,
            Service: "qcusbser",
            PortName: "COM4",
            ParentInstanceId: "12345678"
        ));

        mockEnumerator.UsbDevices.Add(new RawUsbDeviceInfo(
            DeviceInstanceId: @"USB\VID_05C6&PID_9001&MI_02\6&1234567&0&0002",
            HardwareId: @"USB\VID_05C6&PID_9001&REV_0100&MI_02",
            FriendlyName: "Qualcomm HS-USB Modem 9001",
            DeviceDesc: "Qualcomm Mobile Broadband Modem",
            ContainerId: sharedContainer,
            Service: "qcusbser",
            PortName: "COM5",
            ParentInstanceId: "12345678"
        ));

        mockEnumerator.ComPorts.Add(new RawSerialPortInfo("COM3", @"\Device\QCUSB_COM3"));
        mockEnumerator.ComPorts.Add(new RawSerialPortInfo("COM4", @"\Device\QCUSB_COM4"));
        mockEnumerator.ComPorts.Add(new RawSerialPortInfo("COM5", @"\Device\QCUSB_COM5"));

        var redactor = new SensitiveDataRedactor();
        var provider = new WindowsDeviceDiscoveryProvider(
            mockEnumerator,
            redactor,
            NullLogger<WindowsDeviceDiscoveryProvider>.Instance);

        // Act
        var devices = await provider.DiscoverDevicesAsync();

        // Assert
        Assert.Single(devices);
        var physicalDevice = devices[0];

        Assert.Equal("0x05C6", physicalDevice.Identifier.VendorId);
        Assert.Equal("0x9001", physicalDevice.Identifier.ProductId);
        Assert.Equal("Qualcomm Technologies", physicalDevice.Manufacturer);
        Assert.False(physicalDevice.IsSimulation);

        // Physical device separates 3 distinct logical interfaces
        Assert.Equal(3, physicalDevice.Interfaces.Count);
        Assert.Equal("MI_00", physicalDevice.Interfaces[0].InterfaceId);
        Assert.Equal("COM3", physicalDevice.Interfaces[0].PortName);
        Assert.Equal("MI_01", physicalDevice.Interfaces[1].InterfaceId);
        Assert.Equal("COM4", physicalDevice.Interfaces[1].PortName);
        Assert.Equal("MI_02", physicalDevice.Interfaces[2].InterfaceId);
        Assert.Equal("COM5", physicalDevice.Interfaces[2].PortName);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_DoesNotClaimModem_BasedSolelyOnFriendlyName()
    {
        // Arrange
        var mockEnumerator = new MockWindowsDeviceEnumerator();
        mockEnumerator.UsbDevices.Add(new RawUsbDeviceInfo(
            DeviceInstanceId: @"USB\VID_1A86&PID_7523\5&9876543&0&1",
            HardwareId: @"USB\VID_1A86&PID_7523",
            FriendlyName: "USB High Speed Wireless Modem & Diagnostic Interface",
            DeviceDesc: "USB Serial Controller",
            ContainerId: "{11111111-2222-3333-4444-555555555555}",
            Service: "CH341SER",
            PortName: "COM7",
            ParentInstanceId: null
        ));

        mockEnumerator.ComPorts.Add(new RawSerialPortInfo("COM7", @"\Device\CH341SER0"));

        var provider = new WindowsDeviceDiscoveryProvider(
            mockEnumerator,
            new SensitiveDataRedactor(),
            NullLogger<WindowsDeviceDiscoveryProvider>.Instance);

        // Act
        var devices = await provider.DiscoverDevicesAsync();

        // Assert: Friendly name containing "Modem" must NEVER cause premature verification as modem
        Assert.Single(devices);
        var device = devices[0];
        Assert.False(device.IsModemVerified, "Device must NOT be verified as a modem based solely on friendly name.");
        Assert.Equal(DeviceCapability.None, device.SupportedCapabilities);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_DiscoversStandaloneMotherboardComPort()
    {
        // Arrange
        var mockEnumerator = new MockWindowsDeviceEnumerator();
        mockEnumerator.ComPorts.Add(new RawSerialPortInfo("COM1", @"\Device\Serial0"));

        var provider = new WindowsDeviceDiscoveryProvider(
            mockEnumerator,
            new SensitiveDataRedactor(),
            NullLogger<WindowsDeviceDiscoveryProvider>.Instance);

        // Act
        var devices = await provider.DiscoverDevicesAsync();

        // Assert
        Assert.Single(devices);
        var com1 = devices[0];
        Assert.Equal("COM1", com1.Identifier.PortName);
        Assert.Equal(DeviceInterfaceType.SerialCom, com1.InterfaceType);
        Assert.Single(com1.Interfaces);
        Assert.Equal("COM1", com1.Interfaces[0].PortName);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_HandlesEmptyEnumeration_WithoutError()
    {
        // Arrange
        var mockEnumerator = new MockWindowsDeviceEnumerator();
        var provider = new WindowsDeviceDiscoveryProvider(
            mockEnumerator,
            new SensitiveDataRedactor(),
            NullLogger<WindowsDeviceDiscoveryProvider>.Instance);

        // Act
        var devices = await provider.DiscoverDevicesAsync();

        // Assert
        Assert.Empty(devices);
    }

    [Fact]
    public async Task DiscoverDevicesAsync_HonorsCancellationToken()
    {
        // Arrange
        var mockEnumerator = new MockWindowsDeviceEnumerator();
        var provider = new WindowsDeviceDiscoveryProvider(
            mockEnumerator,
            new SensitiveDataRedactor(),
            NullLogger<WindowsDeviceDiscoveryProvider>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await provider.DiscoverDevicesAsync(cts.Token);
        });
    }

    private readonly Xunit.Abstractions.ITestOutputHelper? _output;

    public WindowsDeviceDiscoveryTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task WindowsRegistryDeviceEnumerator_CanExecuteSafely_OnHost()
    {
        // Arrange
        var enumerator = new WindowsRegistryDeviceEnumerator(NullLogger<WindowsRegistryDeviceEnumerator>.Instance);
        var provider = new WindowsDeviceDiscoveryProvider(
            enumerator,
            new SensitiveDataRedactor(),
            NullLogger<WindowsDeviceDiscoveryProvider>.Instance);

        // Act
        var comPorts = await enumerator.EnumerateActiveComPortsAsync();
        var usbDevices = await enumerator.EnumerateUsbDevicesAsync();
        var physicalDevices = await provider.DiscoverDevicesAsync();

        _output?.WriteLine($"Host Enumeration Results:");
        _output?.WriteLine($"Active COM Ports ({comPorts.Count}):");
        foreach (var cp in comPorts)
        {
            _output?.WriteLine($"  Port: {cp.PortName}, DeviceMapKey: {cp.DeviceMapKey}");
        }

        _output?.WriteLine($"Raw USB Nodes ({usbDevices.Count}):");
        foreach (var usb in usbDevices)
        {
            _output?.WriteLine($"  Instance: {usb.DeviceInstanceId}, HWID: {usb.HardwareId}, Name: {usb.FriendlyName ?? usb.DeviceDesc}, Port: {usb.PortName}, Service: {usb.Service}");
        }

        _output?.WriteLine($"Aggregated Physical Devices ({physicalDevices.Count}):");
        foreach (var dev in physicalDevices)
        {
            _output?.WriteLine($"  Device: {dev.Model} (VID: {dev.Identifier.VendorId}, PID: {dev.Identifier.ProductId}, Port: {dev.Identifier.PortName}, Interfaces: {dev.Interfaces.Count})");
        }

        // Assert: Must return non-null collections without throwing exceptions
        Assert.NotNull(comPorts);
        Assert.NotNull(usbDevices);
    }
}
