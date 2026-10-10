using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Desktop.ViewModels.Pages;

public sealed partial class OperationsViewModel : ObservableObject, IDisposable
{
    private readonly IOperationRegistry _registry;
    private readonly IOperationExecutor _executor;
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly ISimUnlockService _simUnlockService;
    private readonly ILogger<OperationsViewModel> _logger;

    [ObservableProperty]
    private string _gateNotice = "Pre-flight capability validation strictly enforced by OperationExecutor.";

    [ObservableProperty]
    private DeviceDescriptor? _selectedDevice;

    [ObservableProperty]
    private IOperationHandler? _selectedOperation;

    [ObservableProperty]
    private bool _isExecuting = false;

    [ObservableProperty]
    private bool _canCancel = false;

    [ObservableProperty]
    private double _progressPercentage = 0;

    [ObservableProperty]
    private string _statusText = "Ready. Select a target device and operation to execute.";

    [ObservableProperty]
    private string? _stepDescription;

    [ObservableProperty]
    private OperationLifecycleState _lifecycleState = OperationLifecycleState.Created;

    [ObservableProperty]
    private bool _confirmationRequired = false;

    [ObservableProperty]
    private bool _userConfirmed = false;

    [ObservableProperty]
    private string? _lastErrorMessage;

    [ObservableProperty]
    private OperationExecutionReport? _lastReport;

    [ObservableProperty]
    private SimUnlockEligibilityResult? _simEligibility;

    [ObservableProperty]
    private bool _isCheckingSimEligibility = false;

    public ObservableCollection<DeviceDescriptor> DiscoveredDevices { get; } = new();
    public ObservableCollection<IOperationHandler> AvailableOperations { get; } = new();
    public ObservableCollection<OperationExecutionReport> ExecutionHistory { get; } = new();
    public ObservableCollection<SimUnlockAuditRecord> SimAuditRecords { get; } = new();

    public OperationsViewModel(
        IOperationRegistry registry,
        IOperationExecutor executor,
        IDeviceDiscoveryService discoveryService,
        ISimUnlockService simUnlockService,
        ILogger<OperationsViewModel> logger)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _simUnlockService = simUnlockService ?? throw new ArgumentNullException(nameof(simUnlockService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _executor.ExecutionUpdated += OnExecutionUpdated;
        _discoveryService.DevicesDiscovered += OnDevicesDiscovered;

        RefreshDevices();
        RefreshOperations();
    }

    private void OnDevicesDiscovered(object? sender, IReadOnlyList<DeviceDescriptor> devices)
    {
        if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(RefreshDevices);
        }
        else
        {
            RefreshDevices();
        }
    }

    private void OnExecutionUpdated(object? sender, OperationExecutionReport report)
    {
        if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => HandleExecutionUpdated(report));
        }
        else
        {
            HandleExecutionUpdated(report);
        }
    }

    private void HandleExecutionUpdated(OperationExecutionReport report)
    {
        LifecycleState = report.State;
        ProgressPercentage = report.ProgressPercentage;
        StatusText = report.StatusText;
        StepDescription = report.StepDescription;
        LastReport = report;

        if (report.State == OperationLifecycleState.Running || report.State == OperationLifecycleState.Waiting)
        {
            IsExecuting = true;
            CanCancel = SelectedOperation?.Descriptor.SupportsCancellation ?? true;
        }
        else
        {
            IsExecuting = false;
            CanCancel = false;
        }

        if (report.ErrorMessage != null)
        {
            LastErrorMessage = report.ErrorMessage;
        }
    }

    public void RefreshDevices()
    {
        DiscoveredDevices.Clear();
        var discovered = _discoveryService.LastDiscoveredDevices;
        foreach (var dev in discovered)
        {
            DiscoveredDevices.Add(dev);
        }

        if (SelectedDevice == null && DiscoveredDevices.Count > 0)
        {
            SelectedDevice = DiscoveredDevices[0];
        }

        RefreshOperations();
    }

    partial void OnSelectedDeviceChanged(DeviceDescriptor? value)
    {
        RefreshOperations();
    }

    partial void OnSelectedOperationChanged(IOperationHandler? value)
    {
        ConfirmationRequired = value?.Descriptor.RequiresUserConfirmation ?? false;
        UserConfirmed = false;
        LastErrorMessage = null;
    }

    public void RefreshOperations()
    {
        AvailableOperations.Clear();
        var ops = SelectedDevice != null
            ? _registry.GetOperationsForDevice(SelectedDevice)
            : _registry.GetAvailableOperations();

        foreach (var op in ops)
        {
            AvailableOperations.Add(op);
        }

        if (SelectedOperation == null && AvailableOperations.Count > 0)
        {
            SelectedOperation = AvailableOperations[0];
        }
    }

    [RelayCommand]
    public async Task ExecuteOperationAsync()
    {
        if (SelectedOperation == null || IsExecuting)
        {
            return;
        }

        LastErrorMessage = null;
        IsExecuting = true;
        CanCancel = SelectedOperation.Descriptor.SupportsCancellation;
        StatusText = "Initiating operation dispatch...";
        ProgressPercentage = 0;

        try
        {
            var request = new OperationExecutionRequest
            {
                OperationId = SelectedOperation.Descriptor.OperationId,
                TargetDevice = SelectedDevice,
                UserConfirmed = UserConfirmed
            };

            var report = await _executor.ExecuteAsync(request);
            LastReport = report;

            ExecutionHistory.Insert(0, report);
            GateNotice = $"Execution completed with status '{report.State}' @ {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error dispatching operation {OpId}", SelectedOperation.Descriptor.OperationId);
            LastErrorMessage = ex.Message;
            StatusText = $"Dispatch failed: {ex.Message}";
        }
        finally
        {
            IsExecuting = false;
            CanCancel = false;
        }
    }

    [RelayCommand]
    public async Task CancelActiveOperationAsync()
    {
        if (!CanCancel)
        {
            return;
        }

        StatusText = "Requesting cancellation of active operation...";
        await _executor.CancelActiveExecutionAsync();
    }

    [RelayCommand]
    public void InspectCapabilities()
    {
        var count = AvailableOperations.Count;
        var dev = SelectedDevice?.DisplayName ?? "No device";
        GateNotice = $"Validated capabilities for {dev}. {count} operation(s) permitted @ {DateTime.Now:HH:mm:ss}.";
        _logger.LogInformation("Inspected operation capabilities for {Device}", dev);
    }

    [RelayCommand]
    public async Task CheckSimEligibilityAsync()
    {
        if (SelectedDevice == null)
        {
            StatusText = "Please select a target device to evaluate SIM unlock eligibility.";
            return;
        }

        IsCheckingSimEligibility = true;
        StatusText = "Querying baseband facility lock status (Phase 1)...";

        try
        {
            var mockTransport = new NexusDeviceService.Infrastructure.Modem.MockModemTransport();
            await mockTransport.OpenAsync("COM_SIM_TEMP");
            var redactor = new NexusDeviceService.Infrastructure.Logging.SensitiveDataRedactor();
            var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<NexusDeviceService.Infrastructure.Modem.ModemCommunicationChannel>.Instance;
            await using var channel = new NexusDeviceService.Infrastructure.Modem.ModemCommunicationChannel(mockTransport, redactor, logger);

            SimEligibility = await _simUnlockService.CheckEligibilityAsync(SelectedDevice, channel);
            StatusText = $"Eligibility check: {SimEligibility.LockStatus}. {SimEligibility.GuidanceMessage}";
            await LoadSimAuditHistoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query SIM unlock eligibility for {Device}", SelectedDevice.DisplayName);
            StatusText = $"Eligibility check error: {ex.Message}";
        }
        finally
        {
            IsCheckingSimEligibility = false;
        }
    }

    [RelayCommand]
    public async Task LoadSimAuditHistoryAsync()
    {
        try
        {
            var records = await _simUnlockService.GetAuditHistoryAsync(SelectedDevice?.Identifier.ToString());
            SimAuditRecords.Clear();
            foreach (var r in records)
            {
                SimAuditRecords.Add(r);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SIM audit history.");
        }
    }

    public void Dispose()
    {
        _executor.ExecutionUpdated -= OnExecutionUpdated;
        _discoveryService.DevicesDiscovered -= OnDevicesDiscovered;
    }
}
