using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Pre-flight capability checker and executor for all device operations.
/// </summary>
public sealed class OperationOrchestrator : IOperationOrchestrator
{
    private readonly ILogger<OperationOrchestrator> _logger;
    private readonly SemaphoreSlim _executionGate = new(1, 1);
    private CancellationTokenSource? _activeCts;

    public bool IsBusy => _executionGate.CurrentCount == 0;
    public IDeviceOperation? ActiveOperation { get; private set; }

    public OperationOrchestrator(ILogger<OperationOrchestrator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IOperationResult> RunOperationAsync(
        IDeviceSession session,
        IDeviceOperation operation,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(operation);

        if (!await _executionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Another device operation is currently running.");
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCts = linkedCts;
        ActiveOperation = operation;

        var stopwatch = Stopwatch.StartNew();
        var logs = new List<string>();

        void Log(string message)
        {
            var formatted = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}";
            logs.Add(formatted);
            _logger.LogInformation("{Message}", formatted);
        }

        try
        {
            Log($"Initiating operation '{operation.DisplayName}' (Id: {operation.OperationId})...");

            // 1. Pre-flight Capability Verification
            if (!session.Profile.Supports(operation.RequiredCapability))
            {
                var error = $"Device '{session.Profile.ModelName}' does not support required capability '{operation.RequiredCapability}'.";
                Log($"[PRE-FLIGHT ERROR] {error}");
                _logger.LogWarning("Capability check failed for {Operation} on {Profile}", operation.OperationId, session.Profile.ProfileId);
                return OperationResult.Failure(
                    message: "Capability validation failed.",
                    errorDetails: error,
                    elapsed: stopwatch.Elapsed,
                    logLines: logs);
            }

            // 2. Connection State Verification
            if (!session.Connection.IsConnected)
            {
                var error = "Device communication channel is not connected.";
                Log($"[CONNECTION ERROR] {error}");
                return OperationResult.Failure(
                    message: "Device is disconnected.",
                    errorDetails: error,
                    elapsed: stopwatch.Elapsed,
                    logLines: logs);
            }

            progress?.Report(new OperationProgress(0, "Starting operation...", "Pre-flight checks passed"));

            // 3. Operation Execution
            var result = await operation.ExecuteAsync(session.Connection, progress, linkedCts.Token).ConfigureAwait(false);
            stopwatch.Stop();

            Log($"Operation completed with status: {result.Status} in {stopwatch.ElapsedMilliseconds}ms");
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            Log("Operation was cancelled.");
            return OperationResult.Cancelled(
                message: "Operation was cancelled by user.",
                elapsed: stopwatch.Elapsed,
                logLines: logs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Log($"[CRITICAL EXCEPTION] {ex.Message}");
            _logger.LogError(ex, "Unexpected error executing operation {OperationId}", operation.OperationId);
            return OperationResult.Failure(
                message: "Operation failed due to an unexpected error.",
                errorDetails: ex.ToString(),
                elapsed: stopwatch.Elapsed,
                logLines: logs);
        }
        finally
        {
            ActiveOperation = null;
            _activeCts = null;
            _executionGate.Release();
        }
    }

    public void CancelActiveOperation()
    {
        try
        {
            _activeCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
    }
}
