using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Abstraction for communicating with an authorized carrier/manufacturer unlock authority.
/// In accordance with security requirements, provider credentials and master keys are maintained
/// on the authorization server, never in the desktop client application.
/// </summary>
public interface ISimUnlockAuthorizationClient
{
    /// <summary>
    /// Requests an authorized unlock ticket for a verified device and consent agreement.
    /// </summary>
    Task<SimUnlockAuthorizationTicket> RequestTicketAsync(
        DeviceDescriptor device,
        SimUnlockConsent consent,
        string maskedImei,
        CancellationToken cancellationToken = default);
}
