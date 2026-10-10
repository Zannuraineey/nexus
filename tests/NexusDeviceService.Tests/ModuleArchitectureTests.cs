using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Extensions;
using NexusDeviceService.Infrastructure.Simulated;
using Xunit;

namespace NexusDeviceService.Tests;

public class ModuleArchitectureTests
{
    private sealed class TestModule : IDeviceModule
    {
        public string ModuleId { get; init; } = "module.test.basic";
        public string DisplayName { get; init; } = "Test Basic Module";
        public string Version { get; init; } = "1.0.0";
        public DeviceCapability Capabilities { get; init; } = DeviceCapability.InformationReading;
        public IReadOnlyList<string> RequiredDependencyModuleIds { get; init; } = Array.Empty<string>();
        public ModuleAvailabilityStatus AvailabilityStatus { get; init; } = ModuleAvailabilityStatus.Available;
        public ModuleCompatibilityInfo Compatibility { get; init; } = new();
        public IReadOnlyList<IOperationHandler> Operations { get; init; } = Array.Empty<IOperationHandler>();
    }

    private sealed class TestOperationHandler : IOperationHandler
    {
        public OperationDescriptor Descriptor { get; }

        public TestOperationHandler(string opId, DeviceCapability requiredCapabilities = DeviceCapability.InformationReading)
        {
            Descriptor = new OperationDescriptor
            {
                OperationId = opId,
                DisplayName = $"Test Op ({opId})",
                RequiredCapabilities = requiredCapabilities,
                Risk = OperationRiskClassification.Safe
            };
        }

        public Task<IOperationResult> ExecuteAsync(IOperationContext context, IOperationProgressReporter progressReporter)
        {
            progressReporter.ReportProgress(100, "Done");
            return Task.FromResult<IOperationResult>(OperationResult.Success("Completed", TimeSpan.FromMilliseconds(10)));
        }
    }

    [Fact]
    public void OperationRegistry_CanRegisterModulesIndependently()
    {
        var registry = new OperationRegistry(Enumerable.Empty<IDeviceModule>(), NullLogger<OperationRegistry>.Instance);

        var module1 = new TestModule { ModuleId = "module.one", DisplayName = "Module One" };
        var module2 = new TestModule { ModuleId = "module.two", DisplayName = "Module Two" };

        registry.RegisterModule(module1);
        registry.RegisterModule(module2);

        Assert.Equal(2, registry.RegisteredModules.Count);
        Assert.NotNull(registry.GetModule("module.one"));
        Assert.NotNull(registry.GetModule("module.two"));
        Assert.Null(registry.GetModule("module.nonexistent"));
    }

    [Fact]
    public void OperationRegistry_RejectsDuplicateModuleIds_Clearly()
    {
        var registry = new OperationRegistry(Enumerable.Empty<IDeviceModule>(), NullLogger<OperationRegistry>.Instance);
        var moduleA = new TestModule { ModuleId = "module.duplicate.id", DisplayName = "Module First" };
        var moduleB = new TestModule { ModuleId = "module.duplicate.id", DisplayName = "Module Second" };

        registry.RegisterModule(moduleA);

        var ex = Assert.Throws<InvalidOperationException>(() => registry.RegisterModule(moduleB));
        Assert.Contains("Duplicate module registration rejected", ex.Message);
        Assert.Contains("module.duplicate.id", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OperationRegistry_RejectsInvalidModuleId(string? invalidId)
    {
        var registry = new OperationRegistry(Enumerable.Empty<IDeviceModule>(), NullLogger<OperationRegistry>.Instance);
        var module = new TestModule { ModuleId = invalidId! };

        Assert.ThrowsAny<ArgumentException>(() => registry.RegisterModule(module));
    }

    [Fact]
    public void OperationRegistry_ReportsMissingDependencies()
    {
        var dependentModule = new TestModule
        {
            ModuleId = "module.dependent",
            DisplayName = "Dependent Module",
            RequiredDependencyModuleIds = new[] { "module.missing.core" }
        };

        var registry = new OperationRegistry(new[] { dependentModule }, NullLogger<OperationRegistry>.Instance);
        var issues = registry.ValidateRegistry();

        Assert.Single(issues);
        Assert.Equal("module.dependent", issues[0].ModuleId);
        Assert.Equal("module.missing.core", issues[0].MissingDependencyId);
        Assert.True(issues[0].IsFatal);
        Assert.Contains("Required dependency 'module.missing.core' is not registered", issues[0].Message);
    }

    [Fact]
    public void OperationRegistry_DetectsDuplicateOperationIdsWithinModule()
    {
        var duplicateOpModule = new TestModule
        {
            ModuleId = "module.dupe.ops",
            Operations = new[]
            {
                new TestOperationHandler("op.same.id"),
                new TestOperationHandler("op.same.id")
            }
        };

        var registry = new OperationRegistry(new[] { duplicateOpModule }, NullLogger<OperationRegistry>.Instance);
        var issues = registry.ValidateRegistry();

        Assert.Single(issues);
        Assert.Contains("duplicate operation ID 'op.same.id'", issues[0].Message);
    }

    [Fact]
    public void OperationRegistry_PreventsOfferingOperationsFromBrokenModules()
    {
        var brokenModule = new TestModule
        {
            ModuleId = "module.broken",
            RequiredDependencyModuleIds = new[] { "module.missing.dep" },
            Operations = new[] { new TestOperationHandler("op.broken.one") }
        };

        var registry = new OperationRegistry(new[] { brokenModule }, NullLogger<OperationRegistry>.Instance);
        var ops = registry.GetAvailableOperations();

        Assert.Empty(ops);
    }

    [Fact]
    public void OperationRegistry_Gating_PreventsUnsupportedOperationsForDevice()
    {
        var opInfo = new TestOperationHandler("op.info", DeviceCapability.InformationReading);
        var opUnlock = new TestOperationHandler("op.unlock", DeviceCapability.AuthorizedSimUnlock);
        var opAt = new TestOperationHandler("op.at", DeviceCapability.AtCommandSession);

        var module = new TestModule
        {
            ModuleId = "module.suite",
            Operations = new[] { opInfo, opUnlock, opAt }
        };

        var registry = new OperationRegistry(new[] { module }, NullLogger<OperationRegistry>.Instance);

        // Device only supports InformationReading and AtCommandSession, NOT AuthorizedSimUnlock
        var limitedDevice = new DeviceDescriptor
        {
            ProviderId = "test.provider",
            Identifier = new DeviceIdentifier
            {
                VendorId = "0x05C6",
                ProductId = "0x9001",
                SerialNumber = "SIM-101",
                PortName = "COM3"
            },
            SupportedCapabilities = DeviceCapability.InformationReading | DeviceCapability.AtCommandSession
        };

        var offeredOps = registry.GetOperationsForDevice(limitedDevice);

        Assert.Equal(2, offeredOps.Count);
        Assert.Contains(offeredOps, op => op.Descriptor.OperationId == "op.info");
        Assert.Contains(offeredOps, op => op.Descriptor.OperationId == "op.at");
        Assert.DoesNotContain(offeredOps, op => op.Descriptor.OperationId == "op.unlock");
    }

    [Fact]
    public void SimulatedModemModule_DeclaresSafeCapabilitiesAndExecutesSimulatedWorkflow()
    {
        var module = new SimulatedModemModule(NullLogger<SimulatedModemModule>.Instance);

        Assert.Equal("module.nexus.simulated.modem", module.ModuleId);
        Assert.True(module.Compatibility.IsSimulationOnly);
        Assert.Equal(ModuleAvailabilityStatus.Available, module.AvailabilityStatus);
        Assert.True(module.Capabilities.HasFlag(DeviceCapability.AtCommandSession));
        Assert.True(module.Capabilities.HasFlag(DeviceCapability.InformationReading));
        Assert.Equal(3, module.Operations.Count);

        var readOp = module.Operations.FirstOrDefault(o => o.Descriptor.OperationId == "op.sim.modem.read_info");
        Assert.NotNull(readOp);
        Assert.Equal(OperationRiskClassification.Safe, readOp.Descriptor.Risk);
        Assert.True(readOp.Descriptor.SupportsCancellation);
    }

    [Fact]
    public async Task SimulatedOperationHandler_ReportsProgressAndReturnsSuccess()
    {
        var module = new SimulatedModemModule(NullLogger<SimulatedModemModule>.Instance);
        var atOp = module.Operations.First(o => o.Descriptor.OperationId == "op.sim.modem.at_query");

        var progressReporter = new OperationProgressReporter();
        var progressReports = new List<OperationProgress>();
        progressReporter.ProgressChanged += (s, e) => progressReports.Add(e);

        var context = new OperationContext
        {
            OperationId = atOp.Descriptor.OperationId,
            Device = new DeviceDescriptor
            {
                ProviderId = "test.provider",
                Identifier = new DeviceIdentifier
                {
                    VendorId = "0x05C6",
                    ProductId = "0x9001",
                    SerialNumber = "SIM-102",
                    PortName = "COM3"
                }
            }
        };

        var result = await atOp.ExecuteAsync(context, progressReporter);

        Assert.True(result.IsSuccess);
        Assert.Equal(OperationStatus.Completed, result.Status);
        Assert.NotEmpty(result.LogLines);
        Assert.NotEmpty(progressReports);
        Assert.Equal(100.0, progressReports.Last().Percentage);
    }

    [Fact]
    public void DI_ResolvesOperationRegistryAndPhysicalModules()
    {
        var services = new ServiceCollection();
        services.AddNexusServices();
        var provider = services.BuildServiceProvider();

        var registry = provider.GetService<IOperationRegistry>();
        Assert.NotNull(registry);

        var flashingModule = registry.GetModule("module.nexus.firmware.flasher");
        Assert.NotNull(flashingModule);
        Assert.NotEmpty(registry.GetAvailableOperations());
    }
}
