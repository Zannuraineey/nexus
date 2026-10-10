namespace NexusDeviceService.Infrastructure.Logging;

public interface IAppLogSink
{
    event EventHandler<AppLogEntry>? LogReceived;
    IReadOnlyList<AppLogEntry> GetRecentLogs();
    void Emit(AppLogEntry entry);
    void Clear();
}
