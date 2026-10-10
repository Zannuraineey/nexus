using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Infrastructure.Modem;

/// <summary>
/// Provider for simulated devices in demonstration, lab, or test environments.
/// Explicitly flagged as virtual simulation to prevent confusing simulated identifiers with physical hardware.
/// </summary>
public sealed class SimulatedDeviceInformationProvider : IDeviceInformationProvider
{
    public string ProviderId => "provider.modem.simulated";
    public int Priority => 10;

    private readonly ILogger<SimulatedDeviceInformationProvider> _logger;
    private static readonly Regex NumericCodeRegex = new(@"^\d{8,16}$", RegexOptions.Compiled);

    public SimulatedDeviceInformationProvider(ILogger<SimulatedDeviceInformationProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(DeviceDescriptor device) => device.IsSimulation;

    public Task<DeviceInformationResult> GetDeviceInformationAsync(
        DeviceDescriptor device,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Querying simulated device information for: {Device}", device.DisplayName);

        var isLocked = device.SupportedCapabilities.HasFlag(DeviceCapability.AuthorizedSimUnlock);
        var lockStatus = isLocked ? SimNetworkLockStatus.NetworkLocked : SimNetworkLockStatus.Unlocked;
        var retries = isLocked ? 10 : -1;

        var result = DeviceInformationResult.AvailableResult(
            message: "Simulated hardware telemetry active (Virtual Lab Environment).",
            imei: "860123456789012",
            maskedImei: "***********9012",
            model: device.Model ?? "Nexus Virtual Diagnostic Modem",
            firmwareVersion: device.FirmwareVersion ?? "VIRT-1.0.0-LTS",
            hardwareRevision: device.HardwareRevision ?? "REV-SIM-A",
            lockStatus: lockStatus,
            retriesRemaining: retries,
            canUnlock: isLocked,
            managementChannel: "Virtual Emulated Channel (Loopback)",
            carrierName: "Global Lab Test Network",
            extendedProperties: new Dictionary<string, string>
            {
                ["SimulationMode"] = "True",
                ["CarrierProfile"] = "Simulated Carrier MCC 001 MNC 01"
            });

        return Task.FromResult(result);
    }

    public Task<SimUnlockOutcome> ApplyCarrierUnlockCodeAsync(
        DeviceDescriptor device,
        string unlockCode,
        CancellationToken cancellationToken = default)
    {
        var sanitized = unlockCode?.Trim() ?? string.Empty;

        if (!NumericCodeRegex.IsMatch(sanitized))
        {
            return Task.FromResult(SimUnlockOutcome.Failure(
                guidance: "Unlock rejected: Unlock code must be between 8 and 16 numeric digits.",
                error: "Invalid code format: Carrier unlock codes must consist strictly of 8 to 16 numeric digits."));
        }

        if (sanitized == "00000000" || sanitized == "12345678" || sanitized == "11111111")
        {
            return Task.FromResult(SimUnlockOutcome.Failure(
                guidance: "Unlock blocked: Nexus Device Service rejects guess/brute-force code patterns.",
                error: "Rejected test pattern: Never guess or brute-force unlock codes."));
        }

        _logger.LogInformation("Simulating carrier unlock on virtual device with valid code format");

        return Task.FromResult(SimUnlockOutcome.SuccessOutcome(
            guidance: "Simulated carrier unlock succeeded: Baseband network lock cleared on virtual device.",
            isMock: true));
    }
}
