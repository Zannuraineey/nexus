using NexusDeviceService.Application.Events;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Orchestrates active device session lifetime, attachment, and disconnection.
/// </summary>
public interface IDeviceSessionManager
{
    IDeviceSession? CurrentSession { get; }
    bool HasActiveSession => CurrentSession != null && CurrentSession.IsActive;

    event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

    Task<IDeviceSession> OpenSessionAsync(IDeviceProvider provider, DeviceIdentifier identifier, CancellationToken cancellationToken = default);
    Task CloseCurrentSessionAsync(CancellationToken cancellationToken = default);
}
