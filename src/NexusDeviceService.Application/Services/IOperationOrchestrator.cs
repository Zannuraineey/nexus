using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Common;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Orchestrates execution of device operations ensuring pre-flight capability checks, cancellation, and logging.
/// </summary>
public interface IOperationOrchestrator
{
    bool IsBusy { get; }
    IDeviceOperation? ActiveOperation { get; }

    Task<IOperationResult> RunOperationAsync(
        IDeviceSession session,
        IDeviceOperation operation,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    void CancelActiveOperation();
}
