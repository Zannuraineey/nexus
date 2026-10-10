using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Service abstraction for retrieving consolidated telemetry, module readiness,
/// and recent operational summaries for the dashboard presentation layer.
/// </summary>
public interface IDashboardDataProvider
{
    Task<DashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}
