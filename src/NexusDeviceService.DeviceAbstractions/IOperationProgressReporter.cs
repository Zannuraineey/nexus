namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Progress reporting abstraction allowing operation handlers to publish structured
/// incremental updates to observing orchestrators, background queues, and ViewModels.
/// </summary>
public interface IOperationProgressReporter
{
    void ReportProgress(double percentage, string statusText, string? stepDescription = null);
    event EventHandler<OperationProgress>? ProgressChanged;
}

/// <summary>
/// Thread-safe standard implementation of IOperationProgressReporter.
/// </summary>
public sealed class OperationProgressReporter : IOperationProgressReporter
{
    public event EventHandler<OperationProgress>? ProgressChanged;

    public void ReportProgress(double percentage, string statusText, string? stepDescription = null)
    {
        var clamped = Math.Clamp(percentage, 0.0, 100.0);
        var payload = new OperationProgress(clamped, statusText, stepDescription);
        ProgressChanged?.Invoke(this, payload);
    }
}
