using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Orchestrates discovery across all registered hardware and simulated device discovery providers.
/// </summary>
public sealed class DeviceDiscoveryService : IDeviceDiscoveryService
{
    private readonly IEnumerable<IDeviceDiscoveryProvider> _providers;
    private readonly ILogger<DeviceDiscoveryService> _logger;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private List<DeviceDescriptor> _lastDiscovered = new();

    public IReadOnlyList<DeviceDescriptor> LastDiscoveredDevices => _lastDiscovered;
    public bool IsScanning => _scanGate.CurrentCount == 0;
    public event EventHandler<IReadOnlyList<DeviceDescriptor>>? DevicesDiscovered;

    public DeviceDiscoveryService(
        IEnumerable<IDeviceDiscoveryProvider> providers,
        ILogger<DeviceDiscoveryService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverAllDevicesAsync(CancellationToken cancellationToken = default)
    {
        await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Beginning device discovery scan across registered providers...");
            var aggregated = new List<DeviceDescriptor>();

            foreach (var provider in _providers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _logger.LogDebug("Querying discovery provider: {ProviderName} (Id: {ProviderId})", provider.DisplayName, provider.ProviderId);
                    var devices = await provider.DiscoverDevicesAsync(cancellationToken).ConfigureAwait(false);
                    aggregated.AddRange(devices);
                    _logger.LogDebug("Provider {ProviderId} returned {Count} devices", provider.ProviderId, devices.Count);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to query provider {ProviderId}. Continuing with remaining providers.", provider.ProviderId);
                }
            }

            _lastDiscovered = aggregated;
            _logger.LogInformation("Device discovery scan completed. Total devices found: {Count}", aggregated.Count);

            DevicesDiscovered?.Invoke(this, aggregated);
            return aggregated;
        }
        finally
        {
            _scanGate.Release();
        }
    }
}
