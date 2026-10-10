using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.Modem;
using NexusDeviceService.Infrastructure.SimUnlock;
using Xunit;

namespace NexusDeviceService.Tests;

public sealed class SimUnlockTests
{
    private readonly SensitiveDataRedactor _redactor = new();

    private DeviceDescriptor CreateSupportedQuectelDevice(string model = "EC25")
    {
        return new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier
            {
                VendorId = "2C7C",
                ProductId = "0125",
                SerialNumber = "867890041234567",
                PortName = "COM3"
            },
            Manufacturer = "Quectel",
            Model = model,
            ProviderId = "test.provider",
            SupportedCapabilities = DeviceCapability.ModemCommunication | DeviceCapability.AuthorizedSimUnlock
        };
    }

    private DeviceDescriptor CreateUnsupportedDevice()
    {
        return new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier
            {
                VendorId = "9999",
                ProductId = "8888",
                SerialNumber = "UNKNOWN_DEVICE_1234",
                PortName = "COM9"
            },
            Manufacturer = "UnknownVendor",
            Model = "ProprietaryLockedModel-X",
            ProviderId = "test.provider",
            SupportedCapabilities = DeviceCapability.ModemCommunication
        };
    }

    private sealed class TestProgressReporter : IOperationProgressReporter
    {
        public event EventHandler<OperationProgress>? ProgressChanged;
        public List<(double Pct, string Status, string? Step)> Reports { get; } = new();

        public void ReportProgress(double percentage, string statusText, string? stepDescription = null)
        {
            Reports.Add((percentage, statusText, stepDescription));
            ProgressChanged?.Invoke(this, new OperationProgress(percentage, statusText, stepDescription));
        }
    }

    [Fact]
    public async Task Phase1_CheckEligibility_ReturnsEligible_WhenDeviceLocked()
    {
        // Arrange
        var transport = new MockModemTransport();
        await transport.OpenAsync("COM3");
        transport.SetResponse("AT+CLCK=\"PN\",2", "\r\n+CLCK: 1\r\n\r\nOK\r\n");
        transport.SetResponse("AT+QPINC=\"PN\"", "\r\n+QPINC: \"PN\",10,10\r\n\r\nOK\r\n");

        await using var channel = new ModemCommunicationChannel(transport, _redactor, NullLogger<ModemCommunicationChannel>.Instance);
        var authClient = new MockSimUnlockAuthorizationClient();
        var auditStore = new InMemorySimUnlockAuditStore();
        var service = new SimUnlockService(authClient, auditStore, NullLogger<SimUnlockService>.Instance);

        var device = CreateSupportedQuectelDevice();

        // Act
        var result = await service.CheckEligibilityAsync(device, channel);

        // Assert
        Assert.True(result.IsEligible);
        Assert.Equal(SimNetworkLockStatus.NetworkLocked, result.LockStatus);
        Assert.Equal(10, result.RetriesRemaining);
        Assert.True(result.RequiresServerAuthorization);
        Assert.Contains("Facility 'PN'", result.GuidanceMessage);
    }

    [Fact]
    public async Task Phase1_CheckEligibility_ReturnsNotEligible_WhenAlreadyUnlocked()
    {
        // Arrange
        var transport = new MockModemTransport();
        await transport.OpenAsync("COM3");
        transport.SetResponse("AT+CLCK=\"PN\",2", "\r\n+CLCK: 0\r\n\r\nOK\r\n");

        await using var channel = new ModemCommunicationChannel(transport, _redactor, NullLogger<ModemCommunicationChannel>.Instance);
        var service = new SimUnlockService(new MockSimUnlockAuthorizationClient(), new InMemorySimUnlockAuditStore(), NullLogger<SimUnlockService>.Instance);

        var device = CreateSupportedQuectelDevice();

        // Act
        var result = await service.CheckEligibilityAsync(device, channel);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(SimNetworkLockStatus.Unlocked, result.LockStatus);
        Assert.False(result.RequiresServerAuthorization);
        Assert.Contains("already network-unlocked", result.GuidanceMessage);
    }

    [Fact]
    public async Task Phase1_CheckEligibility_ReturnsPermanentlyBlocked_WhenRetriesExhausted()
    {
        // Arrange
        var transport = new MockModemTransport();
        await transport.OpenAsync("COM3");
        transport.SetResponse("AT+CLCK=\"PN\",2", "\r\n+CLCK: 1\r\n\r\nOK\r\n");
        transport.SetResponse("AT+QPINC=\"PN\"", "\r\n+QPINC: \"PN\",0,10\r\n\r\nOK\r\n");

        await using var channel = new ModemCommunicationChannel(transport, _redactor, NullLogger<ModemCommunicationChannel>.Instance);
        var service = new SimUnlockService(new MockSimUnlockAuthorizationClient(), new InMemorySimUnlockAuditStore(), NullLogger<SimUnlockService>.Instance);

        var device = CreateSupportedQuectelDevice();

        // Act
        var result = await service.CheckEligibilityAsync(device, channel);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(SimNetworkLockStatus.PermanentlyBlocked, result.LockStatus);
        Assert.Equal(0, result.RetriesRemaining);
        Assert.Contains("MEP/PUK service", result.GuidanceMessage);
    }

    [Fact]
    public async Task Phase1_CheckEligibility_ReturnsUnsupported_ForUnrecognizedDeviceFamily()
    {
        // Arrange
        var transport = new MockModemTransport();
        await transport.OpenAsync("COM9");

        await using var channel = new ModemCommunicationChannel(transport, _redactor, NullLogger<ModemCommunicationChannel>.Instance);
        var service = new SimUnlockService(new MockSimUnlockAuthorizationClient(), new InMemorySimUnlockAuditStore(), NullLogger<SimUnlockService>.Instance);

        var device = CreateUnsupportedDevice();

        // Act
        var result = await service.CheckEligibilityAsync(device, channel);

        // Assert
        Assert.False(result.IsEligible);
        Assert.Equal(SimNetworkLockStatus.Unsupported, result.LockStatus);
        Assert.Contains("not supported", result.GuidanceMessage);
        Assert.Contains("mobile network operator", result.GuidanceMessage);
    }

    [Fact]
    public async Task Phase2_UserConsent_EnforcedBeforeAuthorization()
    {
        // Arrange
        var service = new SimUnlockService(new MockSimUnlockAuthorizationClient(), new InMemorySimUnlockAuditStore(), NullLogger<SimUnlockService>.Instance);
        var device = CreateSupportedQuectelDevice();

        var invalidConsent = new SimUnlockConsent
        {
            UserConfirmed = false, // Not accepted!
            TermsAgreementVersion = "1.0",
            AcknowledgedRiskNotice = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RequestAuthorizationAsync(device, invalidConsent, "[REDACTED_IMEI_****4567]"));
    }

    [Fact]
    public async Task Phase3_MockAuthorization_IssuesClearlyFlaggedMockTicket()
    {
        // Arrange
        var authClient = new MockSimUnlockAuthorizationClient();
        var service = new SimUnlockService(authClient, new InMemorySimUnlockAuditStore(), NullLogger<SimUnlockService>.Instance);
        var device = CreateSupportedQuectelDevice();

        var validConsent = new SimUnlockConsent
        {
            UserConfirmed = true,
            TermsAgreementVersion = "2026.1-TEST",
            AcknowledgedRiskNotice = true
        };

        // Act
        var ticket = await service.RequestAuthorizationAsync(device, validConsent, "[REDACTED_IMEI_****4567]");

        // Assert
        Assert.NotNull(ticket);
        Assert.True(ticket.IsMockAuthorization);
        Assert.StartsWith("MOCK-TICKET-", ticket.TicketId);
        Assert.Contains("Simulated", ticket.ProviderName);
        Assert.True(ticket.ExpiresAtUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Phase4And5_ApplyAndVerifyUnlock_TransitionsLockStatus_AndCreatesAudit()
    {
        // Arrange
        var transport = new MockModemTransport();
        await transport.OpenAsync("COM3");
        // Initial state: locked
        transport.SetResponse("AT+CLCK=\"PN\",2", "\r\n+CLCK: 1\r\n\r\nOK\r\n");

        await using var channel = new ModemCommunicationChannel(transport, _redactor, NullLogger<ModemCommunicationChannel>.Instance);
        var auditStore = new InMemorySimUnlockAuditStore();
        var service = new SimUnlockService(new MockSimUnlockAuthorizationClient(), auditStore, NullLogger<SimUnlockService>.Instance);

        var device = CreateSupportedQuectelDevice();
        var progress = new TestProgressReporter();

        var ticket = new SimUnlockAuthorizationTicket
        {
            TicketId = "MOCK-TICKET-TEST12345",
            DeviceId = device.Identifier.ToString(),
            MaskedImei = "[REDACTED_IMEI_****4567]",
            IsMockAuthorization = true,
            NetworkControlKeyToken = "12345678",
            ProviderName = "Mock Carrier Provider"
        };

        // Act
        var outcome = await service.ApplyAndVerifyUnlockAsync(device, channel, ticket, progress);

        // Assert Phase 5
        Assert.True(outcome.IsSuccess);
        Assert.True(outcome.IsMockSuccess);
        Assert.Equal(SimNetworkLockStatus.NetworkLocked, outcome.StatusBefore);
        Assert.Equal(SimNetworkLockStatus.Unlocked, outcome.StatusAfter);

        // Assert Progress reports
        Assert.Contains(progress.Reports, r => r.Pct == 10);
        Assert.Contains(progress.Reports, r => r.Pct == 30);
        Assert.Contains(progress.Reports, r => r.Pct == 70);
        Assert.Contains(progress.Reports, r => r.Pct == 85);
        Assert.Contains(progress.Reports, r => r.Pct == 100);

        // Assert Phase 6: Audit Record created
        var auditRecords = await auditStore.GetRecordsAsync(device.Identifier.ToString());
        Assert.Single(auditRecords);
        var record = auditRecords[0];
        Assert.True(record.IsSuccess);
        Assert.True(record.IsMockSuccess);
        Assert.Equal(ticket.TicketId, record.TicketId);
        Assert.Equal("[REDACTED_IMEI_****4567]", record.MaskedImei);
    }

    [Fact]
    public async Task Phase5_ApplyAndVerifyUnlock_FailsGracefully_WhenPhysicalStatusRemainsLocked()
    {
        // Arrange
        var transport = new MockModemTransport();
        await transport.OpenAsync("COM3");
        // Both before and after remain locked
        transport.SetResponse("AT+CLCK=\"PN\",2", "\r\n+CLCK: 1\r\n\r\nOK\r\n");
        // Simulate unlock command returning OK, but physical state remains locked
        transport.SetResponse("AT+CLCK=\"PN\",0,\"12345678\"", "\r\nOK\r\n");

        await using var channel = new ModemCommunicationChannel(transport, _redactor, NullLogger<ModemCommunicationChannel>.Instance);
        var auditStore = new InMemorySimUnlockAuditStore();
        var service = new SimUnlockService(new MockSimUnlockAuthorizationClient(), auditStore, NullLogger<SimUnlockService>.Instance);

        var device = CreateSupportedQuectelDevice();

        var ticket = new SimUnlockAuthorizationTicket
        {
            TicketId = "MOCK-TICKET-FAILTEST",
            DeviceId = device.Identifier.ToString(),
            MaskedImei = "[REDACTED_IMEI_****4567]",
            IsMockAuthorization = true,
            NetworkControlKeyToken = "12345678",
            ProviderName = "Mock Carrier Provider"
        };

        // Act
        var outcome = await service.ApplyAndVerifyUnlockAsync(device, channel, ticket);

        // Assert
        Assert.False(outcome.IsSuccess);
        Assert.Equal(SimNetworkLockStatus.NetworkLocked, outcome.StatusAfter);
        Assert.Contains("remains active", outcome.GuidanceMessage);

        // Audit store still records the failure
        var audit = await auditStore.GetRecordsAsync();
        Assert.Single(audit);
        Assert.False(audit[0].IsSuccess);
    }

    [Fact]
    public async Task AuthorizedSimUnlockModule_ExecutesViaOperationHandlerContract()
    {
        // Arrange
        var transport = new MockModemTransport();
        await transport.OpenAsync("COM3");
        await using var channel = new ModemCommunicationChannel(transport, _redactor, NullLogger<ModemCommunicationChannel>.Instance);

        var auditStore = new InMemorySimUnlockAuditStore();
        var service = new SimUnlockService(new MockSimUnlockAuthorizationClient(), auditStore, NullLogger<SimUnlockService>.Instance);
        var loggerFactory = NullLoggerFactory.Instance;
        var module = new AuthorizedSimUnlockModule(service, _redactor, loggerFactory, NullLogger<AuthorizedSimUnlockModule>.Instance);

        var handler = module.Operations[0];
        Assert.Equal("op.nexus.sim.unlock", handler.Descriptor.OperationId);
        Assert.Equal(OperationRiskClassification.Service, handler.Descriptor.Risk);
        Assert.Equal(DeviceCapability.AuthorizedSimUnlock, handler.Descriptor.RequiredCapabilities);

        var device = CreateSupportedQuectelDevice();
        var context = new OperationContext
        {
            OperationId = handler.Descriptor.OperationId,
            Device = device,
            Parameters = new Dictionary<string, object?> { ["channel"] = channel }
        };

        var progress = new TestProgressReporter();

        // Act
        var result = await handler.ExecuteAsync(context, progress);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(OperationStatus.Completed, result.Status);

        var audits = await service.GetAuditHistoryAsync(device.Identifier.ToString());
        Assert.Single(audits);
        Assert.True(audits[0].IsMockSuccess);
    }

    [Fact]
    public void SensitiveDataRedactor_RedactsClckFacilityPasswords()
    {
        // Test facility password redaction in command logging
        var rawCmd = "AT+CLCK=\"PN\",0,\"12345678\"";
        var redacted = _redactor.Redact(rawCmd);

        Assert.DoesNotContain("12345678", redacted);
        Assert.Contains("[REDACTED_FACILITY_PASSWORD]", redacted);
    }
}
