using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Execution context provided to an operation handler containing target connection,
/// device descriptors, invocation parameters, and cancellation tokens.
/// </summary>
public interface IOperationContext
{
    string OperationId { get; }
    string CorrelationId { get; }
    IDeviceConnection? Connection { get; }
    DeviceDescriptor? Device { get; }
    IReadOnlyDictionary<string, object?> Parameters { get; }
    CancellationToken CancellationToken { get; }
}

/// <summary>
/// Standard in-memory implementation of IOperationContext.
/// </summary>
public sealed class OperationContext : IOperationContext
{
    public required string OperationId { get; init; }
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
    public IDeviceConnection? Connection { get; init; }
    public DeviceDescriptor? Device { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
    public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
}
