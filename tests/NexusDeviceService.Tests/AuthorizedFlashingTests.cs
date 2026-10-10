using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Firmware;
using Xunit;

namespace NexusDeviceService.Tests;

public class AuthorizedFlashingTests
{
    private sealed class TestOperationContext : IOperationContext
    {
        public string OperationId { get; init; } = "op.nexus.firmware.flash";
        public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
        public IDeviceConnection? Connection { get; init; }
        public DeviceDescriptor? Device { get; init; }
        public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
        public CancellationToken CancellationToken { get; init; } = CancellationToken.None;
    }

    private sealed class TestProgressReporter : IOperationProgressReporter
    {
        public List<double> Percentages { get; } = new();
        public List<string> Messages { get; } = new();
        public event EventHandler<OperationProgress>? ProgressChanged;

        public void ReportProgress(double percentage, string statusText, string? stepDescription = null)
        {
            Percentages.Add(percentage);
            Messages.Add(statusText);
            ProgressChanged?.Invoke(this, new OperationProgress(percentage, statusText, stepDescription));
        }
    }

    [Fact]
    public async Task ExecuteAsync_SucceedsOnAuthorizedQuectelEc25Device()
    {
        var handler = new AuthorizedFlashingModule.AuthorizedFirmwareFlashHandler(NullLogger.Instance);
        var reporter = new TestProgressReporter();

        var device = new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x2C7C", ProductId = "0x0125", SerialNumber = "EC25_001" },
            Manufacturer = "Quectel",
            Model = "EC25-E LTE Cat 4 Modem",
            HardwareRevision = "R02",
            ProviderId = "test"
        };

        var context = new TestOperationContext { Device = device };

        var result = await handler.ExecuteAsync(context, reporter);

        Assert.True(result.IsSuccess);
        Assert.Contains(reporter.Percentages, p => p >= 90);
        Assert.Contains(reporter.Messages, m => m.Contains("completed successfully", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_RejectsUnauthorizedDeviceModel()
    {
        var handler = new AuthorizedFlashingModule.AuthorizedFirmwareFlashHandler(NullLogger.Instance);
        var reporter = new TestProgressReporter();

        var unauthorizedDevice = new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x05C6", ProductId = "0x9001", SerialNumber = "UNAUTH_001" },
            Manufacturer = "Generic",
            Model = "Unknown Phone",
            ProviderId = "test"
        };

        var context = new TestOperationContext { Device = unauthorizedDevice };

        var result = await handler.ExecuteAsync(context, reporter);

        Assert.False(result.IsSuccess);
        Assert.Contains("not an authorized", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsWhenTargetDeviceIsNull()
    {
        var handler = new AuthorizedFlashingModule.AuthorizedFirmwareFlashHandler(NullLogger.Instance);
        var reporter = new TestProgressReporter();

        var context = new TestOperationContext { Device = null };

        var result = await handler.ExecuteAsync(context, reporter);

        Assert.False(result.IsSuccess);
        Assert.Contains("No target device", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_HonorsCancellation_DuringPreflightPhase()
    {
        var handler = new AuthorizedFlashingModule.AuthorizedFirmwareFlashHandler(NullLogger.Instance);
        var reporter = new TestProgressReporter();

        var device = new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x2C7C", ProductId = "0x0125", SerialNumber = "EC25_002" },
            Manufacturer = "Quectel",
            Model = "EC25",
            ProviderId = "test"
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately in pre-flight

        var context = new TestOperationContext
        {
            Device = device,
            CancellationToken = cts.Token
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await handler.ExecuteAsync(context, reporter);
        });
    }

    [Fact]
    public async Task ExecuteAsync_ProtectsHardware_ByCompletingWhenCancelledDuringIrreversibleWrite()
    {
        var handler = new AuthorizedFlashingModule.AuthorizedFirmwareFlashHandler(NullLogger.Instance);
        var reporter = new TestProgressReporter();

        var device = new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x2C7C", ProductId = "0x0125", SerialNumber = "EC25_003" },
            Manufacturer = "Quectel",
            Model = "EC25-A",
            ProviderId = "test"
        };

        using var cts = new CancellationTokenSource();

        var context = new TestOperationContext
        {
            Device = device,
            CancellationToken = cts.Token
        };

        // Trigger cancellation during Stage 3 (after Stage 1 & 2 pre-flight completes)
        _ = Task.Run(async () =>
        {
            await Task.Delay(600);
            cts.Cancel();
        });

        // The operation enters the irreversible write barrier and must NOT brick the device
        var result = await handler.ExecuteAsync(context, reporter);

        Assert.True(result.IsSuccess, "Irreversible write protection should complete to preserve flash integrity.");
    }
}
