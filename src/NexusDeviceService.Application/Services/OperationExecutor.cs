using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Production-grade operation executor managing the complete lifecycle state machine,
/// pre-flight capability gating, cancellation token propagation, per-device mutual exclusion,
/// and structured execution reporting.
/// </summary>
public sealed class OperationExecutor : IOperationExecutor
{
    private readonly IOperationRegistry _registry;
    private readonly ILogger<OperationExecutor> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _deviceLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<OperationExecutionReport> _history = new();
    private readonly object _historyLock = new();

    private CancellationTokenSource? _activeCts;
    private readonly object _activeLock = new();

    public OperationExecutionReport? ActiveExecution { get; private set; }

    public IReadOnlyList<OperationExecutionReport> History
    {
        get
        {
            lock (_historyLock)
            {
                return _history.ToList();
            }
        }
    }

    public event EventHandler<OperationExecutionReport>? ExecutionUpdated;

    public OperationExecutor(
        IOperationRegistry registry,
        ILogger<OperationExecutor> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OperationExecutionReport> ExecuteAsync(
        OperationExecutionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var executionId = Guid.NewGuid().ToString("N");
        var startTime = DateTime.UtcNow;
        var sm = new OperationStateMachine(OperationLifecycleState.Created);

        var deviceKey = request.TargetDevice != null
            ? (request.TargetDevice.Identifier.SerialNumber ?? request.TargetDevice.Identifier.PortName ?? "unknown-device")
            : "system-wide";

        var opHandler = _registry.GetOperation(request.OperationId);
        var displayName = opHandler?.Descriptor.DisplayName ?? request.OperationId;

        // 1. Initial State: Created
        var currentReport = new OperationExecutionReport
        {
            ExecutionId = executionId,
            OperationId = request.OperationId,
            DisplayName = displayName,
            ModuleId = FindModuleIdForOperation(request.OperationId),
            TargetDeviceSerialOrPort = deviceKey,
            StartTime = startTime,
            State = sm.CurrentState,
            ProgressPercentage = 0,
            StatusText = "Operation created; entering pre-flight validation."
        };

        UpdateReport(currentReport);

        // 2. State: Validating
        sm.TransitionTo(OperationLifecycleState.Validating);
        currentReport = currentReport with
        {
            State = sm.CurrentState,
            StatusText = "Validating operation requirements and device capabilities..."
        };
        UpdateReport(currentReport);

        if (opHandler == null)
        {
            sm.TransitionTo(OperationLifecycleState.Failed);
            currentReport = currentReport with
            {
                State = sm.CurrentState,
                EndTime = DateTime.UtcNow,
                ErrorCode = "ERR_OP_NOT_FOUND",
                ErrorMessage = $"Operation with ID '{request.OperationId}' was not found in the module registry."
            };
            return FinalizeReport(currentReport);
        }

        var descriptor = opHandler.Descriptor;

        // Pre-flight check: Device required?
        if (descriptor.RequiresDeviceConnection && request.TargetDevice == null)
        {
            sm.TransitionTo(OperationLifecycleState.Failed);
            currentReport = currentReport with
            {
                State = sm.CurrentState,
                EndTime = DateTime.UtcNow,
                ErrorCode = "ERR_DEVICE_REQUIRED",
                ErrorMessage = "This operation requires an active device connection. No target device was selected."
            };
            return FinalizeReport(currentReport);
        }

        // Pre-flight check: Capabilities check
        if (request.TargetDevice != null && descriptor.RequiredCapabilities != DeviceCapability.None)
        {
            var required = descriptor.RequiredCapabilities;
            if (!request.TargetDevice.Supports(required))
            {
                sm.TransitionTo(OperationLifecycleState.Failed);
                currentReport = currentReport with
                {
                    State = sm.CurrentState,
                    EndTime = DateTime.UtcNow,
                    ErrorCode = "ERR_CAPABILITY_NOT_SUPPORTED",
                    ErrorMessage = $"Target device does not support required capability flags: declared [{required}], device has [{request.TargetDevice.SupportedCapabilities}]."
                };
                return FinalizeReport(currentReport);
            }
        }

        // Pre-flight check: User confirmation required?
        if (descriptor.RequiresUserConfirmation && !request.UserConfirmed)
        {
            sm.TransitionTo(OperationLifecycleState.RequiresAttention);
            currentReport = currentReport with
            {
                State = sm.CurrentState,
                StatusText = "Operation requires explicit technician confirmation before execution.",
                ErrorCode = "ERR_CONFIRMATION_REQUIRED",
                ErrorMessage = "Technician confirmation is required for this operation."
            };
            UpdateReport(currentReport);

            // Cannot proceed without confirmation
            sm.TransitionTo(OperationLifecycleState.Failed);
            currentReport = currentReport with
            {
                State = sm.CurrentState,
                EndTime = DateTime.UtcNow
            };
            return FinalizeReport(currentReport);
        }

        // Check cancellation before lock
        if (cancellationToken.IsCancellationRequested)
        {
            sm.TransitionTo(OperationLifecycleState.Cancelled);
            currentReport = currentReport with
            {
                State = sm.CurrentState,
                EndTime = DateTime.UtcNow,
                StatusText = "Operation cancelled prior to locking device channel."
            };
            return FinalizeReport(currentReport);
        }

        // 3. State: Waiting (per-device mutual exclusion)
        sm.TransitionTo(OperationLifecycleState.Waiting);
        currentReport = currentReport with
        {
            State = sm.CurrentState,
            StatusText = $"Waiting for exclusive lock on device channel [{deviceKey}]..."
        };
        UpdateReport(currentReport);

        var deviceLock = _deviceLocks.GetOrAdd(deviceKey, _ => new SemaphoreSlim(1, 1));
        var lockAcquired = false;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (_activeLock)
        {
            _activeCts = linkedCts;
        }

        try
        {
            await deviceLock.WaitAsync(linkedCts.Token);
            lockAcquired = true;

            // 4. State: Running
            sm.TransitionTo(OperationLifecycleState.Running);
            currentReport = currentReport with
            {
                State = sm.CurrentState,
                StatusText = "Executing operation workflow on device...",
                ProgressPercentage = 5
            };
            UpdateReport(currentReport);

            var reporter = new OperationProgressReporter();
            reporter.ProgressChanged += (s, p) =>
            {
                currentReport = currentReport with
                {
                    ProgressPercentage = p.Percentage,
                    StatusText = p.StatusText,
                    StepDescription = p.StepDescription
                };
                UpdateReport(currentReport);
                progress?.Report(p);
            };

            var context = new OperationContext
            {
                OperationId = request.OperationId,
                CorrelationId = executionId,
                Device = request.TargetDevice,
                Parameters = request.Parameters,
                CancellationToken = linkedCts.Token
            };

            var result = await opHandler.ExecuteAsync(context, reporter);

            if (result.IsSuccess)
            {
                sm.TransitionTo(OperationLifecycleState.Succeeded);
                currentReport = currentReport with
                {
                    State = sm.CurrentState,
                    EndTime = DateTime.UtcNow,
                    ProgressPercentage = 100,
                    StatusText = result.Message,
                    LogEntries = result.LogLines
                };
            }
            else
            {
                sm.TransitionTo(OperationLifecycleState.Failed);
                currentReport = currentReport with
                {
                    State = sm.CurrentState,
                    EndTime = DateTime.UtcNow,
                    ErrorCode = "ERR_EXECUTION_FAULT",
                    ErrorMessage = result.ErrorDetails ?? result.Message,
                    LogEntries = result.LogLines
                };
            }
        }
        catch (OperationCanceledException)
        {
            sm.TransitionTo(OperationLifecycleState.Cancelled);
            currentReport = currentReport with
            {
                State = sm.CurrentState,
                EndTime = DateTime.UtcNow,
                StatusText = "Operation was cancelled by user request."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during operation execution: {OpId}", request.OperationId);
            sm.TransitionTo(OperationLifecycleState.Failed);
            currentReport = currentReport with
            {
                State = sm.CurrentState,
                EndTime = DateTime.UtcNow,
                ErrorCode = "ERR_UNHANDLED_EXCEPTION",
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            if (lockAcquired)
            {
                deviceLock.Release();
            }

            lock (_activeLock)
            {
                if (_activeCts == linkedCts)
                {
                    _activeCts = null;
                }
            }
        }

        return FinalizeReport(currentReport);
    }

    public Task<bool> CancelActiveExecutionAsync()
    {
        lock (_activeLock)
        {
            if (_activeCts != null && !_activeCts.IsCancellationRequested)
            {
                _logger.LogInformation("Cancellation requested for active operation.");
                _activeCts.Cancel();
                return Task.FromResult(true);
            }
        }

        return Task.FromResult(false);
    }

    private void UpdateReport(OperationExecutionReport report)
    {
        ActiveExecution = report;
        ExecutionUpdated?.Invoke(this, report);
    }

    private OperationExecutionReport FinalizeReport(OperationExecutionReport report)
    {
        UpdateReport(report);
        lock (_historyLock)
        {
            _history.Add(report);
        }
        return report;
    }

    private string? FindModuleIdForOperation(string operationId)
    {
        foreach (var mod in _registry.RegisteredModules)
        {
            if (mod.Operations.Any(o => string.Equals(o.Descriptor.OperationId, operationId, StringComparison.OrdinalIgnoreCase)))
            {
                return mod.ModuleId;
            }
        }
        return null;
    }
}
