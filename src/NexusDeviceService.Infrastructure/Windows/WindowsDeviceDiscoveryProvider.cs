using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;

namespace NexusDeviceService.Infrastructure.Windows;

/// <summary>
/// Physical Windows hardware discovery provider that scans active USB nodes and serial COM ports.
/// Separates physical device identity from logical interfaces, redacts sensitive identifiers,
/// and strictly avoids assuming modem capabilities based solely on device friendly names.
/// </summary>
public sealed class WindowsDeviceDiscoveryProvider : IDeviceDiscoveryProvider
{
    private static readonly Regex VidPidRegex = new(
        @"VID_(?<vid>[0-9A-Fa-f]{4})&PID_(?<pid>[0-9A-Fa-f]{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InterfaceMiRegex = new(
        @"&MI_(?<mi>[0-9A-Fa-f]{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IWindowsDeviceEnumerator _enumerator;
    private readonly ISensitiveDataRedactor _redactor;
    private readonly ILogger<WindowsDeviceDiscoveryProvider> _logger;

    public string ProviderId => "provider.windows.hardware";
    public string DisplayName => "Windows Hardware Discovery (USB & COM)";
    public bool IsSimulationProvider => false;

    public WindowsDeviceDiscoveryProvider(
        IWindowsDeviceEnumerator enumerator,
        ISensitiveDataRedactor redactor,
        ILogger<WindowsDeviceDiscoveryProvider> logger)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<DeviceDescriptor>> DiscoverDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("Scanning Windows physical hardware (USB bus and active COM ports)...");

        var comPorts = await _enumerator.EnumerateActiveComPortsAsync(cancellationToken).ConfigureAwait(false);
        var usbNodes = await _enumerator.EnumerateUsbDevicesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Raw enumeration returned {ComCount} active COM ports and {UsbCount} USB nodes.", comPorts.Count, usbNodes.Count);

        var assignedComPorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var physicalDeviceGroups = GroupUsbNodesByPhysicalDevice(usbNodes);
        var resultDevices = new List<DeviceDescriptor>();

        // 1. Convert grouped USB devices into physical DeviceDescriptors with individual interfaces
        foreach (var group in physicalDeviceGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var physicalKey = group.Key;
            var interfacesInGroup = group.Value;

            // Extract VID & PID from first matching node
            var primaryNode = interfacesInGroup.FirstOrDefault(n => VidPidRegex.IsMatch(n.HardwareId))
                              ?? interfacesInGroup.First();

            var (vid, pid) = ExtractVidPid(primaryNode.HardwareId);
            var cleanDesc = CleanRegistryString(primaryNode.DeviceDesc);
            var cleanFriendly = CleanRegistryString(primaryNode.FriendlyName);

            var mappedVendor = MapKnownVendor(vid);
            var manufacturer = mappedVendor ?? "USB";
            var friendlyModel = !string.IsNullOrEmpty(cleanFriendly) 
                ? cleanFriendly 
                : (!string.IsNullOrEmpty(cleanDesc) ? cleanDesc : $"{manufacturer} Device ({vid}:{pid})");

            // Build logical interfaces
            var interfaceDescriptors = new List<DeviceInterfaceDescriptor>();
            string? primaryPort = null;

            foreach (var node in interfacesInGroup)
            {
                var miMatch = InterfaceMiRegex.Match(node.HardwareId);
                var miId = miMatch.Success ? $"MI_{miMatch.Groups["mi"].Value}" : "Interface_0";

                var ifaceType = !string.IsNullOrWhiteSpace(node.PortName)
                    ? DeviceInterfaceType.SerialCom
                    : DeviceInterfaceType.Usb;

                if (!string.IsNullOrWhiteSpace(node.PortName))
                {
                    assignedComPorts.Add(node.PortName);
                    primaryPort ??= node.PortName;
                }

                var ifaceCleanFriendly = CleanRegistryString(node.FriendlyName);
                var ifaceCleanDesc = CleanRegistryString(node.DeviceDesc);
                var resolvedIfaceName = !string.IsNullOrEmpty(ifaceCleanFriendly)
                    ? ifaceCleanFriendly
                    : (!string.IsNullOrEmpty(ifaceCleanDesc) ? ifaceCleanDesc : miId);

                interfaceDescriptors.Add(new DeviceInterfaceDescriptor
                {
                    InterfaceId = miId,
                    FriendlyName = resolvedIfaceName,
                    HardwareId = node.HardwareId,
                    PortName = node.PortName,
                    DriverService = node.Service,
                    InterfaceType = ifaceType,
                    IsOperational = true
                });
            }

            var redactedSerial = _redactor.Redact(physicalKey);

            var descriptor = new DeviceDescriptor
            {
                Identifier = new DeviceIdentifier
                {
                    VendorId = $"0x{vid}",
                    ProductId = $"0x{pid}",
                    SerialNumber = redactedSerial,
                    PortName = primaryPort
                },
                Manufacturer = manufacturer,
                Model = friendlyModel,
                InterfaceType = primaryPort != null ? DeviceInterfaceType.SerialCom : DeviceInterfaceType.Usb,
                DetectionTimestamp = DateTime.UtcNow,
                ProviderId = ProviderId,
                PhysicalDeviceId = primaryNode.ContainerId ?? physicalKey,
                Description = !string.IsNullOrEmpty(cleanDesc) ? cleanDesc : friendlyModel,
                PrimaryHardwareId = primaryNode.HardwareId,
                IsConnected = true,
                // Critical requirement: Do not claim modem status or capabilities solely based on friendly name
                IsModemVerified = false,
                SupportedCapabilities = DeviceCapability.None,
                IsSimulation = false,
                Interfaces = interfaceDescriptors
            };

            resultDevices.Add(descriptor);
        }

        // 2. Add unassigned standalone COM ports (motherboard UART, PCIe serial, external legacy adapters)
        foreach (var comPort in comPorts)
        {
            if (assignedComPorts.Contains(comPort.PortName))
            {
                continue; // Already associated with an enumerated USB device
            }

            var standaloneDescriptor = new DeviceDescriptor
            {
                Identifier = new DeviceIdentifier
                {
                    VendorId = "0x0000",
                    ProductId = "0x0000",
                    SerialNumber = comPort.DeviceMapKey,
                    PortName = comPort.PortName
                },
                Manufacturer = "Standard Serial Port",
                Model = $"Serial Port ({comPort.PortName})",
                InterfaceType = DeviceInterfaceType.SerialCom,
                DetectionTimestamp = DateTime.UtcNow,
                ProviderId = ProviderId,
                PhysicalDeviceId = comPort.DeviceMapKey,
                Description = $"Motherboard / System Serial Port ({comPort.PortName})",
                PrimaryHardwareId = comPort.DeviceMapKey,
                IsConnected = true,
                IsModemVerified = false,
                SupportedCapabilities = DeviceCapability.None,
                IsSimulation = false,
                Interfaces = new List<DeviceInterfaceDescriptor>
                {
                    new()
                    {
                        InterfaceId = "Port_0",
                        FriendlyName = $"System Serial Port ({comPort.PortName})",
                        HardwareId = comPort.DeviceMapKey,
                        PortName = comPort.PortName,
                        InterfaceType = DeviceInterfaceType.SerialCom,
                        IsOperational = true
                    }
                }
            };

            resultDevices.Add(standaloneDescriptor);
        }

        _logger.LogInformation("Windows physical hardware scan completed. Identified {Count} physical devices.", resultDevices.Count);
        return resultDevices;
    }

    private static Dictionary<string, List<RawUsbDeviceInfo>> GroupUsbNodesByPhysicalDevice(IReadOnlyList<RawUsbDeviceInfo> nodes)
    {
        var groups = new Dictionary<string, List<RawUsbDeviceInfo>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            // Prefer ContainerId if present to group composite interfaces
            string groupKey;
            if (!string.IsNullOrWhiteSpace(node.ContainerId))
            {
                groupKey = $"Container_{node.ContainerId}";
            }
            else if (!string.IsNullOrWhiteSpace(node.ParentInstanceId))
            {
                groupKey = $"Parent_{node.ParentInstanceId}";
            }
            else
            {
                // Fall back to extracting base instance without MI_xx
                groupKey = StripInterfaceSuffix(node.DeviceInstanceId);
            }

            if (!groups.TryGetValue(groupKey, out var list))
            {
                list = new List<RawUsbDeviceInfo>();
                groups[groupKey] = list;
            }

            list.Add(node);
        }

        return groups;
    }

    private static string StripInterfaceSuffix(string instanceId)
    {
        var miIndex = instanceId.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase);
        return miIndex >= 0 ? instanceId[..miIndex] : instanceId;
    }

    private static (string vid, string pid) ExtractVidPid(string hardwareId)
    {
        var match = VidPidRegex.Match(hardwareId);
        if (match.Success)
        {
            return (match.Groups["vid"].Value.ToUpperInvariant(), match.Groups["pid"].Value.ToUpperInvariant());
        }

        return ("0000", "0000");
    }

    private static string CleanRegistryString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var semi = raw.IndexOf(';');
        if (semi >= 0 && semi < raw.Length - 1 && raw.StartsWith("@"))
        {
            return raw[(semi + 1)..].Trim();
        }
        return raw.Trim();
    }

    private static string? MapKnownVendor(string vid) => vid switch
    {
        "05C6" => "Qualcomm Technologies",
        "0E8D" => "MediaTek Inc.",
        "18D1" => "Google Inc.",
        "04E8" => "Samsung Electronics",
        "19D2" => "ZTE Corporation",
        "12D1" => "Huawei Technologies",
        "2C7C" => "Quectel Wireless",
        "1E0E" => "SIMCom Wireless",
        "1BC7" => "Telit Communications",
        "1199" => "Sierra Wireless",
        "297F" => "Fibocom Wireless",
        "0403" => "FTDI",
        "10C4" => "Silicon Labs",
        "1A86" => "Winchiphead / QinHeng (CH340)",
        _ => null
    };
}
