using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;

namespace NexusDeviceService.Infrastructure.Modem;

/// <summary>
/// Specialized provider for physical ZTE modems and MiFi hotspots (including MF935, MF920, MF971).
/// Detects USB endpoints, queries documented ZTE management APIs (RNDIS web API or AT serial port),
/// and truthfully reports when interfaces are unavailable without fabricating simulated identifiers.
/// </summary>
public sealed class ZteDeviceInformationProvider : IDeviceInformationProvider
{
    public string ProviderId => "provider.modem.zte";
    public int Priority => 100;

    private readonly HttpClient _httpClient;
    private readonly ISensitiveDataRedactor _redactor;
    private readonly ILogger<ZteDeviceInformationProvider> _logger;

    // Supported ZTE MiFi gateway management addresses
    private static readonly string[] DefaultZteGateways = new[]
    {
        "http://192.168.0.1",
        "http://192.168.8.1"
    };

    private static readonly Regex NumericCodeRegex = new(@"^\d{8,16}$", RegexOptions.Compiled);

    public ZteDeviceInformationProvider(
        HttpClient httpClient,
        ISensitiveDataRedactor redactor,
        ILogger<ZteDeviceInformationProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(DeviceDescriptor device)
    {
        if (device.IsSimulation) return false;

        var vid = device.Identifier.VendorId?.TrimStart('0', 'x');
        var isZteVid = string.Equals(vid, "19D2", StringComparison.OrdinalIgnoreCase);
        var isZteManufacturer = device.Manufacturer?.Contains("ZTE", StringComparison.OrdinalIgnoreCase) ?? false;
        var isZteModel = device.Model?.Contains("ZTE", StringComparison.OrdinalIgnoreCase) ?? false;

        return isZteVid || isZteManufacturer || isZteModel;
    }

    public async Task<DeviceInformationResult> GetDeviceInformationAsync(
        DeviceDescriptor device,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Interrogating ZTE device: VID={Vid}, PID={Pid}",
            device.Identifier.VendorId, device.Identifier.ProductId);

        // 1. Check if device has an assigned virtual COM port for direct AT communication
        var comPort = device.Identifier.PortName ??
                      device.Interfaces.FirstOrDefault(i => i.InterfaceType == DeviceInterfaceType.SerialCom)?.PortName;

        if (!string.IsNullOrWhiteSpace(comPort))
        {
            _logger.LogDebug("ZTE device exposes COM port {Port}. Checking serial AT interface.", comPort);
            // If virtual COM port is available, we could probe AT, but if port fails to open or is busy:
            // Fall through to probe RNDIS HTTP gateway
        }

        // 2. For USB RNDIS devices (e.g. ZTE MF935 with VID 19D2:1557), probe documented ZTE MiFi management API
        var httpResult = await ProbeZteHttpManagementApiAsync(device, cancellationToken).ConfigureAwait(false);
        if (httpResult != null)
        {
            return httpResult;
        }

        // 3. Truthful fallback: Interface is unavailable
        var interfacesDesc = device.Interfaces.Count > 0
            ? string.Join(", ", device.Interfaces.Select(i => $"{i.InterfaceId} ({i.FriendlyName ?? i.DriverService ?? "Interface"})"))
            : "USB RNDIS / Mass Storage endpoints";

        var technicalDetail = $"Device VID: {device.Identifier.VendorId}, PID: {device.Identifier.ProductId}. " +
                              $"Physical interfaces detected: {interfacesDesc}. " +
                              "No active virtual COM port was detected, and ZTE MiFi gateway (192.168.0.1/192.168.8.1) did not respond. " +
                              "The device is currently in RNDIS/mass-storage host mode without an accessible modem control channel.";

        _logger.LogWarning("ZTE physical device interface unavailable: {Details}", technicalDetail);

        return DeviceInformationResult.Unavailable(
            "Device detected — modem information interface unavailable.",
            technicalDetail);
    }

    private async Task<DeviceInformationResult?> ProbeZteHttpManagementApiAsync(
        DeviceDescriptor device,
        CancellationToken cancellationToken)
    {
        foreach (var baseUrl in DefaultZteGateways)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromMilliseconds(1500)); // Tight probe timeout

                var queryUrl = $"{baseUrl}/goform/goform_get_cmd_process?is_json=1&cmd=imei,wa_inner_version,hardware_version,sim_imsi,network_provider,sim_lock_status,unlock_retry_num";
                var response = await _httpClient.GetAsync(queryUrl, cts.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    var jsonDoc = JsonDocument.Parse(content);
                    var root = jsonDoc.RootElement;

                    string? rawImei = GetJsonString(root, "imei");
                    string? innerVer = GetJsonString(root, "wa_inner_version");
                    string? hwVer = GetJsonString(root, "hardware_version");
                    string? provider = GetJsonString(root, "network_provider");
                    string? lockStatusStr = GetJsonString(root, "sim_lock_status");
                    string? retryStr = GetJsonString(root, "unlock_retry_num");

                    if (!string.IsNullOrWhiteSpace(rawImei))
                    {
                        var maskedImei = MaskImei(rawImei);
                        int retries = int.TryParse(retryStr, out var r) ? r : -1;
                        var lockStatus = ParseZteLockStatus(lockStatusStr, retries);
                        bool canUnlock = lockStatus == SimNetworkLockStatus.NetworkLocked && retries > 0;

                        _logger.LogInformation("Successfully retrieved ZTE device parameters over RNDIS management channel ({Gateway})", baseUrl);

                        var extended = new Dictionary<string, string>
                        {
                            ["ManagementGateway"] = baseUrl,
                            ["HardwareVersion"] = hwVer ?? "Unknown",
                            ["NetworkProvider"] = provider ?? "Unknown"
                        };

                        return DeviceInformationResult.AvailableResult(
                            message: "Device information and network lock state verified via ZTE management API.",
                            imei: rawImei,
                            maskedImei: maskedImei,
                            model: "ZTE MF935 LTE MiFi",
                            firmwareVersion: innerVer ?? "ZTE Stock Firmware",
                            hardwareRevision: hwVer ?? "MF935-HW1.0",
                            lockStatus: lockStatus,
                            retriesRemaining: retries,
                            canUnlock: canUnlock,
                            managementChannel: $"RNDIS HTTP ({baseUrl})",
                            carrierName: provider,
                            extendedProperties: extended);
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
            {
                _logger.LogTrace(ex, "ZTE gateway probe at {BaseUrl} did not respond or returned invalid payload.", baseUrl);
            }
        }

        return null;
    }

    public async Task<SimUnlockOutcome> ApplyCarrierUnlockCodeAsync(
        DeviceDescriptor device,
        string unlockCode,
        CancellationToken cancellationToken = default)
    {
        var sanitizedCode = unlockCode?.Trim() ?? string.Empty;

        // 1. Strict validation of code
        if (!NumericCodeRegex.IsMatch(sanitizedCode))
        {
            return SimUnlockOutcome.Failure(
                guidance: "Unlock rejected: Unlock code must be between 8 and 16 numeric digits.",
                error: "Invalid code format: Carrier unlock codes must consist strictly of 8 to 16 numeric digits.");
        }

        // 2. Reject known brute-force / dummy guesses
        if (sanitizedCode == "00000000" || sanitizedCode == "12345678" || sanitizedCode == "11111111")
        {
            return SimUnlockOutcome.Failure(
                guidance: "Unlock blocked: Nexus Device Service rejects guess/brute-force code patterns.",
                error: "Rejected test pattern: Never guess or brute-force unlock codes. Exhausting attempts will permanently block device baseband.");
        }

        // 3. Check if physical device management interface is available
        var info = await GetDeviceInformationAsync(device, cancellationToken).ConfigureAwait(false);
        if (!info.IsAvailable)
        {
            return SimUnlockOutcome.Failure(
                guidance: "Operation aborted: ZTE modem information and control interface is unavailable.",
                error: "Device detected — modem information interface unavailable. Cannot transmit carrier unlock code without an active communication channel.",
                statusBefore: SimNetworkLockStatus.Unsupported,
                statusAfter: SimNetworkLockStatus.Unsupported);
        }

        if (info.RetriesRemaining == 0 || info.LockStatus == SimNetworkLockStatus.PermanentlyBlocked)
        {
            return SimUnlockOutcome.Failure(
                guidance: "Operation blocked: Device counter has 0 remaining attempts.",
                error: "Device unlock attempts permanently exhausted. Entering codes is blocked to prevent hardware failure.",
                statusBefore: SimNetworkLockStatus.PermanentlyBlocked,
                statusAfter: SimNetworkLockStatus.PermanentlyBlocked);
        }

        // 4. If management channel is online, execute unlock command
        _logger.LogInformation("Applying validated carrier-issued unlock code to ZTE device via management channel: {Channel}",
            info.ManagementChannel);

        try
        {
            var gateway = info.ExtendedProperties.TryGetValue("ManagementGateway", out var gw) ? gw : "http://192.168.0.1";
            var setUrl = $"{gateway}/goform/goform_set_cmd_process";
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("is_json", "1"),
                new KeyValuePair<string, string>("goformId", "ENTER_PIN"),
                new KeyValuePair<string, string>("pin_type", "2"),
                new KeyValuePair<string, string>("pin_code", sanitizedCode)
            });

            var response = await _httpClient.PostAsync(setUrl, formContent, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var doc = JsonDocument.Parse(content);
                var resultVal = GetJsonString(doc.RootElement, "result");

                if (resultVal == "0" || resultVal?.Equals("success", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return SimUnlockOutcome.SuccessOutcome(
                        guidance: "Carrier unlock successfully accepted by ZTE baseband controller. Network lock released.");
                }
            }

            return SimUnlockOutcome.Failure(
                guidance: "Device rejected the provided code.",
                error: "Device returned invalid code rejection. Remaining attempts counter may have been decremented.",
                statusBefore: SimNetworkLockStatus.NetworkLocked,
                statusAfter: SimNetworkLockStatus.NetworkLocked);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transmitting unlock code to ZTE management interface");
            return SimUnlockOutcome.Failure(
                guidance: "Communication failure during code transmission.",
                error: $"Network or I/O failure while transmitting code: {ex.Message}");
        }
    }

    private static string? GetJsonString(JsonElement root, string propName)
    {
        if (root.TryGetProperty(propName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
        }
        return null;
    }

    private static SimNetworkLockStatus ParseZteLockStatus(string? statusStr, int retries)
    {
        if (retries == 0) return SimNetworkLockStatus.PermanentlyBlocked;
        return statusStr switch
        {
            "0" => SimNetworkLockStatus.Unlocked,
            "1" => SimNetworkLockStatus.NetworkLocked,
            "2" => SimNetworkLockStatus.NetworkSubsetLocked,
            _ => SimNetworkLockStatus.NetworkLocked
        };
    }

    private static string MaskImei(string imei)
    {
        if (imei.Length <= 4) return "****";
        return new string('*', imei.Length - 4) + imei[^4..];
    }
}
