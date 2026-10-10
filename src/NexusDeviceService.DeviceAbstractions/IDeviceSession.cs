using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Represents an active managed device session bound to a profile and connection.
/// </summary>
public interface IDeviceSession : IAsyncDisposable
{
    DeviceIdentifier Identifier { get; }
    DeviceProfile Profile { get; }
    IDeviceConnection Connection { get; }
    bool IsActive { get; }

    bool CanExecute(IDeviceOperation operation) =>
        Profile.Supports(operation.RequiredCapability);
}
