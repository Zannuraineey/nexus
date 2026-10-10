using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Infrastructure.Modem;

/// <summary>
/// Fallback provider for physical devices that inspects serial COM ports or returns truthful
/// unavailable status when no supported management interface is detected.
/// </summary>
public sealed class GenericDeviceInformationProvider : IDeviceInformationProvider
{
    public string ProviderId => "provider.modem.generic";
    public int Priority => 1;

    private readonly ILogger<GenericDeviceInformationProvider> _logger;

    public GenericDeviceInformationProvider(ILogger<GenericDeviceInformationProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(DeviceDescriptor device) => !device.IsSimulation;

    public Task<DeviceInformationResult> GetDeviceInformationAsync(
        DeviceDescriptor device,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Inspecting generic physical device: {DisplayName}", device.DisplayName);

        var comPort = device.Identifier.PortName ??
                      device.Interfaces.FirstOrDefault(i => i.InterfaceType == DeviceInterfaceType.SerialCom)?.PortName;

        if (!string.IsNullOrWhiteSpace(comPort))
        {
            // If the device has an active serial COM port, report port presence
            return Task.FromResult(new DeviceInformationResult
            {
                IsAvailable = false,
                StatusMessage = $"Device detected on {comPort} — AT command interrogation not verified.",
                InformationState = ModemInformationState.PartiallyAvailable,
                LockStatus = SimNetworkLockStatus.Unknown,
                ManagementChannel = $"Serial COM ({comPort})",
                TechnicalDetails = $"Physical serial port {comPort} is present, but direct modem identification requires manual AT query verification."
            });
        }

        // Truthful fallback when no COM port or management channel exists
        return Task.FromResult(DeviceInformationResult.Unavailable(
            "Device detected — modem information interface unavailable.",
            $"Physical device ({device.Identifier.VendorId}:{device.Identifier.ProductId}) detected via USB, " +
            "but no virtual COM port or documented device management channel was detected. Direct modem interrogation is disabled."));
    }

    public Task<SimUnlockOutcome> ApplyCarrierUnlockCodeAsync(
        DeviceDescriptor device,
        string unlockCode,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(SimUnlockOutcome.Failure(
            guidance: "Operation unsupported: No verified communication interface available for this physical device.",
            error: "Device detected — modem information interface unavailable. Cannot apply carrier unlock code.",
            statusBefore: SimNetworkLockStatus.Unsupported,
            statusAfter: SimNetworkLockStatus.Unsupported));
    }
}
