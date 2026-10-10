using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Pluggable provider for discovering connected physical or simulated devices.
/// </summary>
public interface IDeviceDiscoveryProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    bool IsSimulationProvider { get; }

    Task<IReadOnlyList<DeviceDescriptor>> DiscoverDevicesAsync(CancellationToken cancellationToken = default);
}
