using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.ViewModels.Pages;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Simulated;
using Xunit;

namespace NexusDeviceService.Tests;

public class OperationLifecycleTests
{
    private sealed class MockCancellableOperationHandler : IOperationHandler
    {
        public OperationDescriptor Descriptor { get; }

        public MockCancellableOperationHandler(
            string opId = "op.mock.test",
            DeviceCapability capabilities = DeviceCapability.InformationReading,
            bool requiresDevice = true,
            bool requiresConfirmation = false,
            bool supportsCancellation = true)
        {
            Descriptor = new OperationDescriptor
            {
                OperationId = opId,
                DisplayName = "Mock Test Operation",
                RequiredCapabilities = capabilities,
                RequiresDeviceConnection = requiresDevice,
                RequiresUserConfirmation = requiresConfirmation,
                SupportsCancellation = supportsCancellation
            };
        }

        public async Task<IOperationResult> ExecuteAsync(IOperationContext context, IOperationProgressReporter progressReporter)
        {
            progressReporter.ReportProgress(25, "Step 1");
            await Task.Delay(50, context.CancellationToken);

            progressReporter.ReportProgress(75, "Step 2");
            await Task.Delay(50, context.CancellationToken);

            progressReporter.ReportProgress(100, "Done");
            return OperationResult.Success("Completed successfully", TimeSpan.FromMilliseconds(100));
        }
    }

    private sealed class MockFailingOperationHandler : IOperationHandler
    {
        public OperationDescriptor Descriptor { get; } = new()
        {
            OperationId = "op.mock.failing",
            DisplayName = "Mock Failing Operation",
            RequiredCapabilities = DeviceCapability.None,
            RequiresDeviceConnection = false
        };

        public Task<IOperationResult> ExecuteAsync(IOperationContext context, IOperationProgressReporter progressReporter)
        {
            return Task.FromResult<IOperationResult>(
                OperationResult.Failure("Critical hardware query fault", "REG_TIMEOUT", TimeSpan.FromMilliseconds(20)));
        }
    }

    private static DeviceDescriptor CreateTestDevice(DeviceCapability capabilities = DeviceCapability.InformationReading)
    {
        return new DeviceDescriptor
        {
            ProviderId = "test.provider",
            Identifier = new DeviceIdentifier
            {
                VendorId = "0x05C6",
                ProductId = "0x9001",
                SerialNumber = "SN-LIFECYCLE-1",
                PortName = "COM3"
            },
            SupportedCapabilities = capabilities
        };
    }

    [Fact]
    public void StateMachine_EnforcesValidTransitions()
    {
        var sm = new OperationStateMachine();
        Assert.Equal(OperationLifecycleState.Created, sm.CurrentState);

        sm.TransitionTo(OperationLifecycleState.Validating);
        Assert.Equal(OperationLifecycleState.Validating, sm.CurrentState);

        sm.TransitionTo(OperationLifecycleState.Waiting);
        Assert.Equal(OperationLifecycleState.Waiting, sm.CurrentState);

        sm.TransitionTo(OperationLifecycleState.Running);
        Assert.Equal(OperationLifecycleState.Running, sm.CurrentState);

        sm.TransitionTo(OperationLifecycleState.Succeeded);
        Assert.Equal(OperationLifecycleState.Succeeded, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_RejectsIllegalTransitions()
    {
        var sm = new OperationStateMachine(OperationLifecycleState.Created);

        // Cannot jump directly from Created to Succeeded
        Assert.Throws<InvalidStateTransitionException>(() => sm.TransitionTo(OperationLifecycleState.Succeeded));

        // Terminal states cannot transition
        var terminalSm = new OperationStateMachine(OperationLifecycleState.Succeeded);
        Assert.Throws<InvalidStateTransitionException>(() => terminalSm.TransitionTo(OperationLifecycleState.Running));
    }

    [Fact]
    public async Task OperationExecutor_ExecutesSuccessfully_WithProgressAndStructuredReport()
    {
        var handler = new MockCancellableOperationHandler();
        var registry = new OperationRegistry(Enumerable.Empty<IDeviceModule>(), NullLogger<OperationRegistry>.Instance);
        var module = new SimulatedDeviceModule(new[] { handler });
        registry.RegisterModule(module);

        var executor = new OperationExecutor(registry, NullLogger<OperationExecutor>.Instance);
        var device = CreateTestDevice(DeviceCapability.InformationReading);

        var progressPoints = new List<double>();
        var progress = new Progress<OperationProgress>(p => progressPoints.Add(p.Percentage));

        var request = new OperationExecutionRequest
        {
            OperationId = handler.Descriptor.OperationId,
            TargetDevice = device
        };

        var report = await executor.ExecuteAsync(request, progress);

        Assert.NotNull(report);
        Assert.Equal(OperationLifecycleState.Succeeded, report.State);
        Assert.True(report.IsSuccess);
        Assert.Equal(100.0, report.ProgressPercentage);
        Assert.NotNull(report.EndTime);
        Assert.Single(executor.History);
    }

    [Fact]
    public async Task OperationExecutor_RejectsOperation_WhenCapabilitiesMissing()
    {
        var handler = new MockCancellableOperationHandler(capabilities: DeviceCapability.AuthorizedSimUnlock);
        var registry = new OperationRegistry(Enumerable.Empty<IDeviceModule>(), NullLogger<OperationRegistry>.Instance);
        registry.RegisterModule(new SimulatedDeviceModule(new[] { handler }));

        var executor = new OperationExecutor(registry, NullLogger<OperationExecutor>.Instance);
        // Device lacks AuthorizedSimUnlock
        var limitedDevice = CreateTestDevice(DeviceCapability.InformationReading);

        var request = new OperationExecutionRequest
        {
            OperationId = handler.Descriptor.OperationId,
            TargetDevice = limitedDevice
        };

        var report = await executor.ExecuteAsync(request);

        Assert.Equal(OperationLifecycleState.Failed, report.State);
        Assert.False(report.IsSuccess);
        Assert.Equal("ERR_CAPABILITY_NOT_SUPPORTED", report.ErrorCode);
    }

    [Fact]
    public async Task OperationExecutor_RejectsOperation_WhenConfirmationRequiredAndMissing()
    {
        var handler = new MockCancellableOperationHandler(requiresConfirmation: true);
        var registry = new OperationRegistry(Enumerable.Empty<IDeviceModule>(), NullLogger<OperationRegistry>.Instance);
        registry.RegisterModule(new SimulatedDeviceModule(new[] { handler }));

        var executor = new OperationExecutor(registry, NullLogger<OperationExecutor>.Instance);
        var device = CreateTestDevice();

        var request = new OperationExecutionRequest
        {
            OperationId = handler.Descriptor.OperationId,
            TargetDevice = device,
            UserConfirmed = false // NOT confirmed
        };

        var report = await executor.ExecuteAsync(request);

        Assert.Equal(OperationLifecycleState.Failed, report.State);
        Assert.Equal("ERR_CONFIRMATION_REQUIRED", report.ErrorCode);
    }

    [Fact]
    public async Task OperationExecutor_SupportsCancellation()
    {
        var handler = new MockCancellableOperationHandler();
        var registry = new OperationRegistry(Enumerable.Empty<IDeviceModule>(), NullLogger<OperationRegistry>.Instance);
        registry.RegisterModule(new SimulatedDeviceModule(new[] { handler }));

        var executor = new OperationExecutor(registry, NullLogger<OperationExecutor>.Instance);
        var device = CreateTestDevice();

        using var cts = new CancellationTokenSource();

        var task = executor.ExecuteAsync(new OperationExecutionRequest
        {
            OperationId = handler.Descriptor.OperationId,
            TargetDevice = device
        }, cancellationToken: cts.Token);

        // Cancel after 20ms
        await Task.Delay(20);
        cts.Cancel();

        var report = await task;

        Assert.Equal(OperationLifecycleState.Cancelled, report.State);
        Assert.False(report.IsSuccess);
    }

    [Fact]
    public async Task OperationsViewModel_FiltersOperationsAndExecutes()
    {
        var handlerInfo = new MockCancellableOperationHandler("op.info", DeviceCapability.InformationReading);
        var handlerUnlock = new MockCancellableOperationHandler("op.unlock", DeviceCapability.AuthorizedSimUnlock);

        var registry = new OperationRegistry(Enumerable.Empty<IDeviceModule>(), NullLogger<OperationRegistry>.Instance);
        registry.RegisterModule(new SimulatedDeviceModule(new[] { handlerInfo, handlerUnlock }));

        var executor = new OperationExecutor(registry, NullLogger<OperationExecutor>.Instance);
        var discoveryProvider = new SimulatedDeviceDiscoveryProvider(NullLogger<SimulatedDeviceDiscoveryProvider>.Instance);
        var discoveryService = new DeviceDiscoveryService(new[] { discoveryProvider }, NullLogger<DeviceDiscoveryService>.Instance);
        var simService = new NexusDeviceService.Infrastructure.SimUnlock.SimUnlockService(
            new NexusDeviceService.Infrastructure.SimUnlock.MockSimUnlockAuthorizationClient(),
            new NexusDeviceService.Infrastructure.SimUnlock.InMemorySimUnlockAuditStore(),
            NullLogger<NexusDeviceService.Infrastructure.SimUnlock.SimUnlockService>.Instance);

        var vm = new OperationsViewModel(registry, executor, discoveryService, simService, NullLogger<OperationsViewModel>.Instance);

        // Select a device with only InformationReading
        var device = CreateTestDevice(DeviceCapability.InformationReading);
        vm.SelectedDevice = device;

        Assert.Single(vm.AvailableOperations);
        Assert.Equal("op.info", vm.AvailableOperations[0].Descriptor.OperationId);

        vm.SelectedOperation = vm.AvailableOperations[0];
        await vm.ExecuteOperationAsync();

        Assert.NotNull(vm.LastReport);
        Assert.Equal(OperationLifecycleState.Succeeded, vm.LastReport.State);
        Assert.Single(vm.ExecutionHistory);
    }

    private sealed class SimulatedDeviceModule : IDeviceModule
    {
        public string ModuleId { get; } = "module.test.simulated";
        public string DisplayName { get; } = "Simulated Module";
        public string Version { get; } = "1.0.0";
        public DeviceCapability Capabilities { get; } = DeviceCapability.InformationReading | DeviceCapability.AuthorizedSimUnlock;
        public IReadOnlyList<string> RequiredDependencyModuleIds { get; } = Array.Empty<string>();
        public ModuleAvailabilityStatus AvailabilityStatus => ModuleAvailabilityStatus.Available;
        public ModuleCompatibilityInfo Compatibility { get; } = new();
        public IReadOnlyList<IOperationHandler> Operations { get; }

        public SimulatedDeviceModule(IReadOnlyList<IOperationHandler> operations)
        {
            Operations = operations;
        }
    }
}
