using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Coordinates multiple device discovery providers (USB, COM ports, simulation engines) and aggregates discovered devices.
/// </summary>
public interface IDeviceDiscoveryService
{
    IReadOnlyList<DeviceDescriptor> LastDiscoveredDevices { get; }
    bool IsScanning { get; }
    event EventHandler<IReadOnlyList<DeviceDescriptor>>? DevicesDiscovered;

    Task<IReadOnlyList<DeviceDescriptor>> DiscoverAllDevicesAsync(CancellationToken cancellationToken = default);
}
