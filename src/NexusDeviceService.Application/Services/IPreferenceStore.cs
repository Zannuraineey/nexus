using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Persistence abstraction for reading and saving user configuration across application sessions.
/// </summary>
public interface IPreferenceStore
{
    Task<ApplicationPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default);
    Task SavePreferencesAsync(ApplicationPreferences preferences, CancellationToken cancellationToken = default);
}
