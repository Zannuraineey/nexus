namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Immutable invocation payload dispatched to request an operation execution.
/// </summary>
public sealed record OperationExecutionRequest
{
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");
    public required string OperationId { get; init; }
    public DeviceDescriptor? TargetDevice { get; init; }
    public bool UserConfirmed { get; init; } = false;
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
}

/// <summary>
/// Immutable audit report capturing the full lifecycle, telemetry, and outcome of an executed operation.
/// </summary>
public sealed record OperationExecutionReport
{
    public required string ExecutionId { get; init; }
    public required string OperationId { get; init; }
    public required string DisplayName { get; init; }
    public string? ModuleId { get; init; }
    public string? TargetDeviceSerialOrPort { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
    public OperationLifecycleState State { get; init; }
    public double ProgressPercentage { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public string? StepDescription { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> LogEntries { get; init; } = Array.Empty<string>();
    public bool IsSuccess => State == OperationLifecycleState.Succeeded;
}

/// <summary>
/// Deterministic state machine governing allowed transitions across the operation lifecycle.
/// </summary>
public sealed class OperationStateMachine
{
    private static readonly Dictionary<OperationLifecycleState, HashSet<OperationLifecycleState>> AllowedTransitions = new()
    {
        [OperationLifecycleState.Created] = new()
        {
            OperationLifecycleState.Validating,
            OperationLifecycleState.Cancelled
        },
        [OperationLifecycleState.Validating] = new()
        {
            OperationLifecycleState.Waiting,
            OperationLifecycleState.Running,
            OperationLifecycleState.Failed,
            OperationLifecycleState.Cancelled,
            OperationLifecycleState.RequiresAttention
        },
        [OperationLifecycleState.Waiting] = new()
        {
            OperationLifecycleState.Running,
            OperationLifecycleState.Failed,
            OperationLifecycleState.Cancelled,
            OperationLifecycleState.RequiresAttention
        },
        [OperationLifecycleState.RequiresAttention] = new()
        {
            OperationLifecycleState.Running,
            OperationLifecycleState.Waiting,
            OperationLifecycleState.Failed,
            OperationLifecycleState.Cancelled
        },
        [OperationLifecycleState.Running] = new()
        {
            OperationLifecycleState.Succeeded,
            OperationLifecycleState.Failed,
            OperationLifecycleState.Cancelled,
            OperationLifecycleState.RequiresAttention
        },
        // Terminal states cannot transition further
        [OperationLifecycleState.Succeeded] = new(),
        [OperationLifecycleState.Failed] = new(),
        [OperationLifecycleState.Cancelled] = new()
    };

    public OperationLifecycleState CurrentState { get; private set; }
    public event EventHandler<OperationLifecycleState>? StateChanged;

    public OperationStateMachine(OperationLifecycleState initialState = OperationLifecycleState.Created)
    {
        CurrentState = initialState;
    }

    public bool CanTransition(OperationLifecycleState targetState)
    {
        return AllowedTransitions.TryGetValue(CurrentState, out var targets) && targets.Contains(targetState);
    }

    public void TransitionTo(OperationLifecycleState targetState)
    {
        if (!CanTransition(targetState))
        {
            throw new InvalidStateTransitionException(CurrentState, targetState);
        }

        CurrentState = targetState;
        StateChanged?.Invoke(this, targetState);
    }
}
