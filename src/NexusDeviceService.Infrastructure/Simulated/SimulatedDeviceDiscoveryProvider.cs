using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Infrastructure.Simulated;

/// <summary>
/// Development and testing device discovery provider that returns virtual simulated device descriptors.
/// Clearly labeled as simulated to prevent confusion with real physical hardware.
/// </summary>
public sealed class SimulatedDeviceDiscoveryProvider : IDeviceDiscoveryProvider
{
    private readonly ILogger<SimulatedDeviceDiscoveryProvider> _logger;
    private bool _simulateEmptyMode = false;
    private int _simulatedDelayMs = 250;

    public string ProviderId => "provider.nexus.simulated";
    public string DisplayName => "Nexus Virtual Device Simulator (Non-Hardware)";
    public bool IsSimulationProvider => true;

    public bool SimulateEmptyMode
    {
        get => _simulateEmptyMode;
        set => _simulateEmptyMode = value;
    }

    public int SimulatedDelayMs
    {
        get => _simulatedDelayMs;
        set => _simulatedDelayMs = value;
    }

    public SimulatedDeviceDiscoveryProvider(ILogger<SimulatedDeviceDiscoveryProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SimulatedDeviceDiscoveryProvider: Querying virtual simulation devices...");

        if (_simulatedDelayMs > 0)
        {
            await Task.Delay(_simulatedDelayMs, cancellationToken).ConfigureAwait(false);
        }

        if (_simulateEmptyMode)
        {
            _logger.LogInformation("SimulatedDeviceDiscoveryProvider: Empty mode active, returning 0 devices.");
            return Array.Empty<DeviceDescriptor>();
        }

        var devices = new List<DeviceDescriptor>
        {
            new()
            {
                Identifier = new DeviceIdentifier
                {
                    VendorId = "0x05C6", // Qualcomm
                    ProductId = "0x9001",
                    SerialNumber = "NX-SIM-QC-88412",
                    Revision = "2.10",
                    PortName = "COM3"
                },
                Manufacturer = "Qualcomm Reference (Simulated)",
                Model = "Snapdragon X65 5G Modem",
                ChipsetFamily = "Qualcomm SDX65",
                HardwareRevision = "HW_REV_2.1",
                FirmwareVersion = "MPSS.DE.1.0-00214-SDX65",
                InterfaceType = DeviceInterfaceType.SerialCom,
                DetectionTimestamp = DateTime.UtcNow.AddMinutes(-5),
                ProviderId = ProviderId,
                SupportedCapabilities = DeviceCapability.DeviceDiscovery |
                                        DeviceCapability.InformationReading |
                                        DeviceCapability.ModemCommunication |
                                        DeviceCapability.AtCommandSession |
                                        DeviceCapability.DiagnosticLogging,
                IsSimulation = true
            },
            new()
            {
                Identifier = new DeviceIdentifier
                {
                    VendorId = "0x19D2", // ZTE
                    ProductId = "0x0534",
                    SerialNumber = "NX-SIM-ZTE-10499",
                    Revision = "1.00",
                    PortName = "COM4"
                },
                Manufacturer = "ZTE Mobile Corp (Simulated)",
                Model = "Blade A72 Diagnostic Interface",
                ChipsetFamily = "UNISOC Spreadtrum T606",
                HardwareRevision = "HW_V1.0",
                FirmwareVersion = "GEN_EU_ZTE_A72_V1.0.0B03",
                InterfaceType = DeviceInterfaceType.SerialCom,
                DetectionTimestamp = DateTime.UtcNow.AddMinutes(-2),
                ProviderId = ProviderId,
                SupportedCapabilities = DeviceCapability.DeviceDiscovery |
                                        DeviceCapability.InformationReading |
                                        DeviceCapability.AtCommandSession |
                                        DeviceCapability.SimStatusCheck |
                                        DeviceCapability.AuthorizedSimUnlock,
                IsSimulation = true
            },
            new()
            {
                Identifier = new DeviceIdentifier
                {
                    VendorId = "0x0E8D", // MediaTek
                    ProductId = "0x2000",
                    SerialNumber = "NX-SIM-MTK-99014",
                    Revision = "1.10",
                    PortName = "USB-01"
                },
                Manufacturer = "MediaTek Inc (Simulated)",
                Model = "Dimensity 7050 BootROM Interface",
                ChipsetFamily = "MediaTek MT6789 / Dimensity",
                HardwareRevision = "EVB_V01",
                FirmwareVersion = "MTK_LK_BOOT_V2.04",
                InterfaceType = DeviceInterfaceType.Usb,
                DetectionTimestamp = DateTime.UtcNow.AddMinutes(-1),
                ProviderId = ProviderId,
                SupportedCapabilities = DeviceCapability.DeviceDiscovery |
                                        DeviceCapability.InformationReading |
                                        DeviceCapability.FirmwareValidation,
                IsSimulation = true
            }
        };

        _logger.LogInformation("SimulatedDeviceDiscoveryProvider: Returned {Count} simulated device profiles", devices.Count);
        return devices;
    }
}
