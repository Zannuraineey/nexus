using System.Collections.Concurrent;

namespace NexusDeviceService.Infrastructure.Logging;

/// <summary>
/// Thread-safe circular buffer for in-memory activity logs displayed in the desktop console.
/// </summary>
public sealed class InMemoryLogSink : IAppLogSink
{
    private readonly int _maxCapacity;
    private readonly ConcurrentQueue<AppLogEntry> _queue = new();

    public event EventHandler<AppLogEntry>? LogReceived;

    public InMemoryLogSink(int maxCapacity = 1000)
    {
        _maxCapacity = maxCapacity;
    }

    public void Emit(AppLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _queue.Enqueue(entry);

        while (_queue.Count > _maxCapacity && _queue.TryDequeue(out _))
        {
        }

        LogReceived?.Invoke(this, entry);
    }

    public IReadOnlyList<AppLogEntry> GetRecentLogs() => _queue.ToArray();

    public void Clear()
    {
        while (_queue.TryDequeue(out _))
        {
        }
    }
}
