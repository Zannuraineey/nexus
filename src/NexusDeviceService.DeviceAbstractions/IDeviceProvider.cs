using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Discovers supported physical or simulated devices and creates connection channels.
/// </summary>
public interface IDeviceProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    bool IsSimulationProvider { get; }

    Task<IReadOnlyList<DeviceIdentifier>> DiscoverDevicesAsync(CancellationToken cancellationToken = default);
    Task<IDeviceConnection> CreateConnectionAsync(DeviceIdentifier identifier, CancellationToken cancellationToken = default);
    Task<DeviceProfile> GetDeviceProfileAsync(DeviceIdentifier identifier, CancellationToken cancellationToken = default);
}
