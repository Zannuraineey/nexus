using System.Runtime.Versioning;
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NexusDeviceService.DeviceAbstractions;

namespace NexusDeviceService.Infrastructure.Windows;

/// <summary>
/// Production implementation of <see cref="IWindowsDeviceEnumerator"/> that queries
/// standard Windows kernel device maps and PnP registry nodes.
/// </summary>
public sealed class WindowsRegistryDeviceEnumerator : IWindowsDeviceEnumerator
{
    private readonly ILogger<WindowsRegistryDeviceEnumerator> _logger;

    public WindowsRegistryDeviceEnumerator(ILogger<WindowsRegistryDeviceEnumerator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IReadOnlyList<RawSerialPortInfo>> EnumerateActiveComPortsAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("Current operating system is not Windows. Skipping Windows COM port registry scan.");
            return Task.FromResult<IReadOnlyList<RawSerialPortInfo>>(Array.Empty<RawSerialPortInfo>());
        }

        return Task.FromResult<IReadOnlyList<RawSerialPortInfo>>(EnumerateComPortsInternal());
    }

    public Task<IReadOnlyList<RawUsbDeviceInfo>> EnumerateUsbDevicesAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogDebug("Current operating system is not Windows. Skipping Windows USB registry scan.");
            return Task.FromResult<IReadOnlyList<RawUsbDeviceInfo>>(Array.Empty<RawUsbDeviceInfo>());
        }

        return Task.FromResult<IReadOnlyList<RawUsbDeviceInfo>>(EnumerateUsbDevicesInternal(cancellationToken));
    }

    [SupportedOSPlatform("windows")]
    private IReadOnlyList<RawSerialPortInfo> EnumerateComPortsInternal()
    {
        var result = new List<RawSerialPortInfo>();

        try
        {
            using var serialCommKey = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM", writable: false);
            if (serialCommKey == null)
            {
                _logger.LogDebug(@"Registry key HKLM\HARDWARE\DEVICEMAP\SERIALCOMM does not exist or has no active serial devices.");
                return result;
            }

            foreach (var valueName in serialCommKey.GetValueNames())
            {
                try
                {
                    var portValue = serialCommKey.GetValue(valueName)?.ToString();
                    if (!string.IsNullOrWhiteSpace(portValue))
                    {
                        result.Add(new RawSerialPortInfo(portValue.Trim(), valueName));
                    }
                }
                catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
                {
                    _logger.LogWarning(ex, "Failed to read serial port mapping for value: {ValueName}", valueName);
                }
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Access denied while querying SERIALCOMM registry.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error reading SERIALCOMM registry.");
        }

        return result;
    }

    [SupportedOSPlatform("windows")]
    private IReadOnlyList<RawUsbDeviceInfo> EnumerateUsbDevicesInternal(CancellationToken cancellationToken)
    {
        var result = new List<RawUsbDeviceInfo>();

        try
        {
            using var usbBaseKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB", writable: false);
            if (usbBaseKey == null)
            {
                _logger.LogDebug(@"Registry key HKLM\SYSTEM\CurrentControlSet\Enum\USB does not exist.");
                return result;
            }

            foreach (var deviceGroup in usbBaseKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    using var groupKey = usbBaseKey.OpenSubKey(deviceGroup, writable: false);
                    if (groupKey == null) continue;

                    foreach (var instanceName in groupKey.GetSubKeyNames())
                    {
                        try
                        {
                            using var instanceKey = groupKey.OpenSubKey(instanceName, writable: false);
                            if (instanceKey == null) continue;

                            var hardwareIdRaw = instanceKey.GetValue("HardwareID");
                            string hardwareId = hardwareIdRaw switch
                            {
                                string[] array when array.Length > 0 => array[0],
                                string str => str,
                                _ => deviceGroup
                            };

                            var friendlyName = instanceKey.GetValue("FriendlyName")?.ToString();
                            var deviceDesc = instanceKey.GetValue("DeviceDesc")?.ToString();
                            var containerId = instanceKey.GetValue("ContainerID")?.ToString();
                            var service = instanceKey.GetValue("Service")?.ToString();
                            var parentInstanceId = instanceKey.GetValue("ParentIdPrefix")?.ToString();

                            // Query device parameters for assigned virtual COM port name if any
                            string? portName = null;
                            try
                            {
                                using var devParams = instanceKey.OpenSubKey("Device Parameters", writable: false);
                                portName = devParams?.GetValue("PortName")?.ToString();
                            }
                            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
                            {
                                _logger.LogTrace(ex, "Could not read Device Parameters for {Group}\\{Instance}", deviceGroup, instanceName);
                            }

                            var fullInstanceId = $@"USB\{deviceGroup}\{instanceName}";
                            result.Add(new RawUsbDeviceInfo(
                                DeviceInstanceId: fullInstanceId,
                                HardwareId: hardwareId,
                                FriendlyName: friendlyName,
                                DeviceDesc: deviceDesc,
                                ContainerId: containerId,
                                Service: service,
                                PortName: portName,
                                ParentInstanceId: parentInstanceId
                            ));
                        }
                        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
                        {
                            _logger.LogTrace(ex, "Could not open instance key {Group}\\{Instance}", deviceGroup, instanceName);
                        }
                    }
                }
                catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
                {
                    _logger.LogTrace(ex, "Could not open USB group key {Group}", deviceGroup);
                }
            }
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Access denied while enumerating USB devices from registry.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error enumerating USB devices from registry.");
        }

        return result;
    }
}
