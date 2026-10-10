using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Infrastructure.SimUnlock;

/// <summary>
/// Mock authorization client simulating an authorized carrier backend service.
/// Keeps credentials on the server side and clearly tags generated authorization tickets
/// with IsMockAuthorization = true so that simulations are never confused with production unlocks.
/// </summary>
public sealed class MockSimUnlockAuthorizationClient : ISimUnlockAuthorizationClient
{
    public Task<SimUnlockAuthorizationTicket> RequestTicketAsync(
        DeviceDescriptor device,
        SimUnlockConsent consent,
        string maskedImei,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!consent.UserConfirmed || !consent.AcknowledgedRiskNotice)
        {
            throw new InvalidOperationException(
                "Cannot request authorization ticket without verified user consent and legal risk acknowledgment.");
        }

        var ticket = new SimUnlockAuthorizationTicket
        {
            TicketId = $"MOCK-TICKET-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            DeviceId = device.Identifier.ToString(),
            MaskedImei = maskedImei,
            IsMockAuthorization = true,
            NetworkControlKeyToken = "12345678",
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            ProviderName = "Mock Carrier Authorization Authority (Simulated)"
        };

        return Task.FromResult(ticket);
    }
}
