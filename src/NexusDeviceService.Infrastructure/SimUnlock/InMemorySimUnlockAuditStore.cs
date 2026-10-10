using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Infrastructure.SimUnlock;

/// <summary>
/// Persistent storage abstraction for SIM unlock audit records.
/// </summary>
public interface ISimUnlockAuditStore
{
    Task AddRecordAsync(SimUnlockAuditRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SimUnlockAuditRecord>> GetRecordsAsync(string? deviceId = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thread-safe in-memory audit store for SIM unlock operations.
/// </summary>
public sealed class InMemorySimUnlockAuditStore : ISimUnlockAuditStore
{
    private readonly List<SimUnlockAuditRecord> _records = new();
    private readonly object _lock = new();

    public Task AddRecordAsync(SimUnlockAuditRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _records.Add(record);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SimUnlockAuditRecord>> GetRecordsAsync(string? deviceId = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return Task.FromResult<IReadOnlyList<SimUnlockAuditRecord>>(_records.OrderByDescending(r => r.TimestampUtc).ToList());
            }

            return Task.FromResult<IReadOnlyList<SimUnlockAuditRecord>>(
                _records.Where(r => string.Equals(r.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(r => r.TimestampUtc)
                        .ToList());
        }
    }
}
