using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Progress reporting payload during long-running device actions.
/// </summary>
public sealed record OperationProgress(double Percentage, string StatusText, string? StepDescription = null);

/// <summary>
/// Encapsulates an executable device operation with explicit capability requirements.
/// </summary>
public interface IDeviceOperation
{
    string OperationId { get; }
    string DisplayName { get; }
    string Description { get; }
    DeviceCapability RequiredCapability { get; }

    Task<IOperationResult> ExecuteAsync(
        IDeviceConnection connection,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
