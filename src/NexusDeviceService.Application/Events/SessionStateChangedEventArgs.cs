using NexusDeviceService.DeviceAbstractions;

namespace NexusDeviceService.Application.Events;

public sealed class SessionStateChangedEventArgs : EventArgs
{
    public IDeviceSession? ActiveSession { get; }
    public ConnectionState State { get; }

    public SessionStateChangedEventArgs(IDeviceSession? activeSession, ConnectionState state)
    {
        ActiveSession = activeSession;
        State = state;
    }
}
