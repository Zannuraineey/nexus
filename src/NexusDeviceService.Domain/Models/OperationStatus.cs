namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Status lifecycle of long-running hardware and service operations.
/// </summary>
public enum OperationStatus
{
    NotStarted,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
