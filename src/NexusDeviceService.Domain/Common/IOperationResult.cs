using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Domain.Common;

/// <summary>
/// Immutable contract for operation outcomes.
/// </summary>
public interface IOperationResult
{
    bool IsSuccess { get; }
    OperationStatus Status { get; }
    string Message { get; }
    string? ErrorDetails { get; }
    TimeSpan ElapsedDuration { get; }
    IReadOnlyList<string> LogLines { get; }
}

/// <summary>
/// Generic variant carrying strongly-typed payload data.
/// </summary>
public interface IOperationResult<out TData> : IOperationResult
{
    TData? Data { get; }
}
