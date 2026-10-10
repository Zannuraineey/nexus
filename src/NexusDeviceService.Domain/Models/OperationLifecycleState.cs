namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Formal lifecycle states for device operations governed by the execution state machine.
/// </summary>
public enum OperationLifecycleState
{
    Created = 0,
    Validating = 1,
    Waiting = 2,
    Running = 3,
    Succeeded = 4,
    Failed = 5,
    Cancelled = 6,
    RequiresAttention = 7
}

/// <summary>
/// Exception thrown when an illegal transition is attempted in the operation state machine.
/// </summary>
public sealed class InvalidStateTransitionException : InvalidOperationException
{
    public OperationLifecycleState FromState { get; }
    public OperationLifecycleState ToState { get; }

    public InvalidStateTransitionException(OperationLifecycleState from, OperationLifecycleState to)
        : base($"Invalid state transition: Cannot transition operation from '{from}' to '{to}'.")
    {
        FromState = from;
        ToState = to;
    }
}
