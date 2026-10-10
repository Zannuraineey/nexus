using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Orchestrates the lifecycle, queuing, validation, mutual exclusion, and cancellation of device operations.
/// </summary>
public interface IOperationExecutor
{
    /// <summary>
    /// Currently running or most recently active operation execution.
    /// </summary>
    OperationExecutionReport? ActiveExecution { get; }

    /// <summary>
    /// Historical ledger of all executed operations in the current session.
    /// </summary>
    IReadOnlyList<OperationExecutionReport> History { get; }

    /// <summary>
    /// Event fired whenever an operation transitions lifecycle states or publishes progress.
    /// </summary>
    event EventHandler<OperationExecutionReport>? ExecutionUpdated;

    /// <summary>
    /// Validates prerequisites, secures device mutual exclusion, and executes the operation.
    /// </summary>
    Task<OperationExecutionReport> ExecuteAsync(
        OperationExecutionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cooperative cancellation of the active operation.
    /// </summary>
    Task<bool> CancelActiveExecutionAsync();
}
