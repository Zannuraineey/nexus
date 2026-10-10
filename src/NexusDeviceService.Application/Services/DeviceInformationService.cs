using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Orchestrates device information retrieval and legitimate carrier unlock code submission.
/// Enforces safety barriers, redacts sensitive identifiers from logs, and prevents fabrication
/// of simulated identifiers for real physical hardware.
/// </summary>
public sealed class DeviceInformationService : IDeviceInformationService
{
    private readonly IEnumerable<IDeviceInformationProvider> _providers;
    private readonly ILogger<DeviceInformationService> _logger;

    private static readonly Regex NumericCodeRegex = new(@"^\d{8,16}$", RegexOptions.Compiled);

    public DeviceInformationService(
        IEnumerable<IDeviceInformationProvider> providers,
        ILogger<DeviceInformationService> logger)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeviceInformationResult> GetDeviceInformationAsync(
        DeviceDescriptor device,
        CancellationToken cancellationToken = default)
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        if (!device.IsConnected)
        {
            _logger.LogWarning("Cannot retrieve information: Device {DisplayName} is disconnected.", device.DisplayName);
            return DeviceInformationResult.Unavailable("Device is disconnected.");
        }

        _logger.LogInformation("Querying device information for {DisplayName} (IsSimulation: {IsSim})",
            device.DisplayName, device.IsSimulation);

        var provider = _providers
            .OrderByDescending(p => p.Priority)
            .FirstOrDefault(p => p.CanHandle(device));

        if (provider == null)
        {
            _logger.LogWarning("No registered information provider found for device {DisplayName}", device.DisplayName);
            return DeviceInformationResult.Unavailable(
                "Device detected — modem information interface unavailable.",
                "No registered hardware information provider can communicate with this device profile.");
        }

        try
        {
            var result = await provider.GetDeviceInformationAsync(device, cancellationToken).ConfigureAwait(false);

            if (result.IsAvailable)
            {
                var maskedImei = !string.IsNullOrEmpty(result.Imei) ? MaskImei(result.Imei) : "N/A";
                _logger.LogInformation("Retrieved verified modem information from {ProviderId}. Masked IMEI: {MaskedImei}, Lock: {LockStatus}",
                    provider.ProviderId, maskedImei, result.LockStatus);
            }
            else
            {
                _logger.LogInformation("Provider {ProviderId} reported modem interface unavailable: {Message}",
                    provider.ProviderId, result.StatusMessage);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error retrieving information from provider {ProviderId}", provider.ProviderId);
            return DeviceInformationResult.Unavailable(
                "Error querying device interface.",
                $"Communication error with device management provider: {ex.Message}");
        }
    }

    public async Task<SimUnlockOutcome> ApplyCarrierUnlockCodeAsync(
        DeviceDescriptor device,
        string unlockCode,
        CancellationToken cancellationToken = default)
    {
        if (device == null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        var sanitizedCode = unlockCode?.Trim() ?? string.Empty;

        // 1. Strict validation
        if (string.IsNullOrWhiteSpace(sanitizedCode))
        {
            return SimUnlockOutcome.Failure(
                guidance: "Unlock rejected: Unlock code cannot be empty.",
                error: "Please enter a valid carrier-issued Network Control Key (NCK).");
        }

        if (!NumericCodeRegex.IsMatch(sanitizedCode))
        {
            return SimUnlockOutcome.Failure(
                guidance: "Unlock rejected: Code format invalid.",
                error: "Invalid code format: Carrier unlock codes must consist strictly of 8 to 16 numeric digits.");
        }

        // 2. Reject obvious dummy guesses
        if (sanitizedCode == "00000000" || sanitizedCode == "12345678" || sanitizedCode == "11111111" || sanitizedCode == "88888888")
        {
            return SimUnlockOutcome.Failure(
                guidance: "Unlock blocked: Insecure code pattern rejected.",
                error: "Rejected test pattern: Nexus Device Service prohibits brute-force or placeholder codes to protect baseband hardware.");
        }

        if (!device.IsConnected)
        {
            return SimUnlockOutcome.Failure(
                guidance: "Operation aborted: Device is disconnected.",
                error: "The target device is no longer attached to the workstation.");
        }

        var provider = _providers
            .OrderByDescending(p => p.Priority)
            .FirstOrDefault(p => p.CanHandle(device));

        if (provider == null)
        {
            return SimUnlockOutcome.Failure(
                guidance: "Unsupported device profile.",
                error: "No suitable communication provider found for this device.",
                statusBefore: SimNetworkLockStatus.Unsupported,
                statusAfter: SimNetworkLockStatus.Unsupported);
        }

        _logger.LogInformation("Forwarding validated carrier unlock code to provider {ProviderId} (Code Redacted: [REDACTED_NCK])",
            provider.ProviderId);

        return await provider.ApplyCarrierUnlockCodeAsync(device, sanitizedCode, cancellationToken).ConfigureAwait(false);
    }

    private static string MaskImei(string imei)
    {
        if (imei.Length <= 4) return "****";
        return new string('*', imei.Length - 4) + imei[^4..];
    }
}
