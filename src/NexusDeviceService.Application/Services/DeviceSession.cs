using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Managed session implementation wrapping a device profile and connection.
/// </summary>
public sealed class DeviceSession : IDeviceSession
{
    public DeviceIdentifier Identifier => Connection.Identifier;
    public DeviceProfile Profile { get; }
    public IDeviceConnection Connection { get; }
    public bool IsActive => Connection.IsConnected;

    public DeviceSession(DeviceProfile profile, IDeviceConnection connection)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync().ConfigureAwait(false);
    }
}
