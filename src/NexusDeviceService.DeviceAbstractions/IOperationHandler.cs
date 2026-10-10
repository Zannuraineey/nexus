using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Executable operation handler with declared prerequisite metadata and lifecycle invocation.
/// </summary>
public interface IOperationHandler
{
    /// <summary>
    /// Static metadata declaring requirements, risks, capabilities, and cancellation support.
    /// </summary>
    OperationDescriptor Descriptor { get; }

    /// <summary>
    /// Executes the operation under the provided context and progress reporting sink.
    /// </summary>
    Task<IOperationResult> ExecuteAsync(
        IOperationContext context,
        IOperationProgressReporter progressReporter);
}
