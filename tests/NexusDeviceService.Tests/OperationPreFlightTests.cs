using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;
using Xunit;

namespace NexusDeviceService.Tests;

public class OperationPreFlightTests
{
    private sealed class MockConnection : IDeviceConnection
    {
        public DeviceIdentifier Identifier { get; set; } = new()
        {
            VendorId = "0x1234",
            ProductId = "0x5678",
            SerialNumber = "SIM-001"
        };

        public ConnectionState State { get; set; } = ConnectionState.Connected;

        public event EventHandler<ConnectionState>? StateChanged;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Connected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            State = ConnectionState.Disconnected;
            StateChanged?.Invoke(this, State);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class MockOperation : IDeviceOperation
    {
        public string OperationId => "op.sim.unlock";
        public string DisplayName => "SIM Unlock Test";
        public string Description => "Tests SIM Unlock capability verification";
        public DeviceCapability RequiredCapability => DeviceCapability.AuthorizedSimUnlock;

        public Task<IOperationResult> ExecuteAsync(
            IDeviceConnection connection,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IOperationResult>(OperationResult.Success("Completed mock unlock"));
        }
    }

    [Fact]
    public async Task Orchestrator_RejectsOperation_WhenCapabilityNotSupported()
    {
        var orchestrator = new OperationOrchestrator(NullLogger<OperationOrchestrator>.Instance);
        var connection = new MockConnection();

        // Profile only has InformationReading, NOT AuthorizedSimUnlock
        var profile = new DeviceProfile
        {
            ProfileId = "prof.modem.test",
            Manufacturer = "TestCorp",
            ModelName = "TestModem-1",
            ChipsetFamily = "TestChip",
            SupportedCapabilities = DeviceCapability.InformationReading
        };

        var session = new DeviceSession(profile, connection);
        var operation = new MockOperation();

        var result = await orchestrator.RunOperationAsync(session, operation);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Contains("does not support required capability", result.ErrorDetails);
    }

    [Fact]
    public async Task Orchestrator_RejectsOperation_WhenConnectionDisconnected()
    {
        var orchestrator = new OperationOrchestrator(NullLogger<OperationOrchestrator>.Instance);
        var connection = new MockConnection { State = ConnectionState.Disconnected };

        var profile = new DeviceProfile
        {
            ProfileId = "prof.modem.test",
            Manufacturer = "TestCorp",
            ModelName = "TestModem-1",
            ChipsetFamily = "TestChip",
            SupportedCapabilities = DeviceCapability.AuthorizedSimUnlock
        };

        var session = new DeviceSession(profile, connection);
        var operation = new MockOperation();

        var result = await orchestrator.RunOperationAsync(session, operation);

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Contains("Device communication channel is not connected", result.ErrorDetails);
    }

    [Fact]
    public async Task Orchestrator_ExecutesOperation_WhenValidAndConnected()
    {
        var orchestrator = new OperationOrchestrator(NullLogger<OperationOrchestrator>.Instance);
        var connection = new MockConnection { State = ConnectionState.Connected };

        var profile = new DeviceProfile
        {
            ProfileId = "prof.modem.test",
            Manufacturer = "TestCorp",
            ModelName = "TestModem-1",
            ChipsetFamily = "TestChip",
            SupportedCapabilities = DeviceCapability.AuthorizedSimUnlock
        };

        var session = new DeviceSession(profile, connection);
        var operation = new MockOperation();

        var result = await orchestrator.RunOperationAsync(session, operation);

        Assert.True(result.IsSuccess);
        Assert.Equal(OperationStatus.Completed, result.Status);
    }
}
