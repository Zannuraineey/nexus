using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.Modem;
using Xunit;

namespace NexusDeviceService.Tests;

public class DeviceInformationServiceTests
{
    private readonly SensitiveDataRedactor _redactor = new();

    private DeviceDescriptor CreateZtePhysicalDevice(bool isConnected = true)
    {
        return new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier
            {
                VendorId = "0x19D2",
                ProductId = "0x1557",
                SerialNumber = "1234567890ABCDEF",
                PortName = null // No COM port
            },
            Manufacturer = "ZTE Corporation",
            Model = "ZTE MF935 LTE MiFi",
            InterfaceType = DeviceInterfaceType.Usb,
            DetectionTimestamp = DateTime.UtcNow,
            ProviderId = "provider.hardware.windows",
            IsSimulation = false,
            IsConnected = isConnected,
            Interfaces = new List<DeviceInterfaceDescriptor>
            {
                new()
                {
                    InterfaceId = "MI_00",
                    FriendlyName = "Remote NDIS based Internet Sharing Device",
                    InterfaceType = DeviceInterfaceType.Usb,
                    DriverService = "usbrndis6",
                    IsOperational = true
                },
                new()
                {
                    InterfaceId = "MI_02",
                    FriendlyName = "USB Mass Storage Device",
                    InterfaceType = DeviceInterfaceType.Usb,
                    DriverService = "USBSTOR",
                    IsOperational = true
                }
            }
        };
    }

    private DeviceDescriptor CreateSimulatedDevice()
    {
        return new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier
            {
                VendorId = "0x05C6",
                ProductId = "0x9008",
                SerialNumber = "SIM-DEVICE-001",
                PortName = "COM99"
            },
            Manufacturer = "Nexus Simulation Core",
            Model = "Virtual LTE Diagnostic Device",
            InterfaceType = DeviceInterfaceType.VirtualSimulation,
            DetectionTimestamp = DateTime.UtcNow,
            ProviderId = "provider.simulation.virtual",
            IsSimulation = true,
            SupportedCapabilities = DeviceCapability.InformationReading | DeviceCapability.AuthorizedSimUnlock
        };
    }

    private DeviceDescriptor CreateGenericPhysicalDevice()
    {
        return new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier
            {
                VendorId = "0x1234",
                ProductId = "0x5678",
                SerialNumber = "GEN-001",
                PortName = null
            },
            Manufacturer = "Generic Hardware Inc.",
            Model = "Generic USB Controller",
            InterfaceType = DeviceInterfaceType.Usb,
            DetectionTimestamp = DateTime.UtcNow,
            ProviderId = "provider.hardware.windows",
            IsSimulation = false
        };
    }

    private DeviceInformationService CreateServiceWithMockHttp(HttpMessageHandler httpHandler)
    {
        var httpClient = new HttpClient(httpHandler);
        var zteProvider = new ZteDeviceInformationProvider(httpClient, _redactor, NullLogger<ZteDeviceInformationProvider>.Instance);
        var simProvider = new SimulatedDeviceInformationProvider(NullLogger<SimulatedDeviceInformationProvider>.Instance);
        var genericProvider = new GenericDeviceInformationProvider(NullLogger<GenericDeviceInformationProvider>.Instance);

        var providers = new IDeviceInformationProvider[] { zteProvider, simProvider, genericProvider };
        return new DeviceInformationService(providers, NullLogger<DeviceInformationService>.Instance);
    }

    [Fact]
    public void ProviderSelection_ChoosesZteProvider_ForZtePhysicalHardware()
    {
        var handler = new TestHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var zteProvider = new ZteDeviceInformationProvider(httpClient, _redactor, NullLogger<ZteDeviceInformationProvider>.Instance);
        var simProvider = new SimulatedDeviceInformationProvider(NullLogger<SimulatedDeviceInformationProvider>.Instance);
        var genericProvider = new GenericDeviceInformationProvider(NullLogger<GenericDeviceInformationProvider>.Instance);

        var zteDevice = CreateZtePhysicalDevice();
        var simDevice = CreateSimulatedDevice();
        var genericDevice = CreateGenericPhysicalDevice();

        Assert.True(zteProvider.CanHandle(zteDevice));
        Assert.False(simProvider.CanHandle(zteDevice));
        Assert.True(genericProvider.CanHandle(zteDevice));
        Assert.True(zteProvider.Priority > genericProvider.Priority); // ZTE provider has higher priority

        Assert.False(zteProvider.CanHandle(simDevice));
        Assert.True(simProvider.CanHandle(simDevice));

        Assert.False(zteProvider.CanHandle(genericDevice));
        Assert.False(simProvider.CanHandle(genericDevice));
        Assert.True(genericProvider.CanHandle(genericDevice));
    }

    [Fact]
    public async Task ZteDevice_WhenInterfaceUnavailable_ReturnsTruthfulUnavailableResult_WithoutInventingImei()
    {
        // Mock handler that refuses connection (simulating offline RNDIS interface)
        var handler = new TestHttpMessageHandler
        {
            ResponseCallback = req => throw new HttpRequestException("Host unreachable")
        };
        var service = CreateServiceWithMockHttp(handler);
        var zteDevice = CreateZtePhysicalDevice();

        var result = await service.GetDeviceInformationAsync(zteDevice);

        Assert.False(result.IsAvailable);
        Assert.Equal("Device detected — modem information interface unavailable.", result.StatusMessage);
        Assert.Null(result.Imei); // Must NOT invent an IMEI
        Assert.Null(result.MaskedImei);
        Assert.Null(result.FirmwareVersion);
        Assert.Equal(SimNetworkLockStatus.Unsupported, result.LockStatus);
        Assert.Contains("RNDIS", result.TechnicalDetails);
        Assert.Contains("Mass Storage", result.TechnicalDetails);
    }

    [Fact]
    public async Task ZteDevice_WhenHttpGatewayResponds_ParsesLegitimateIdentifiers()
    {
        var jsonResponse = "{\"imei\":\"861234567890123\",\"wa_inner_version\":\"BD_MF935V1.0.0\",\"hardware_version\":\"MF935-HW1\",\"network_provider\":\"Vodafone\",\"sim_lock_status\":\"1\",\"unlock_retry_num\":\"10\"}";
        var handler = new TestHttpMessageHandler
        {
            ResponseCallback = req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            }
        };
        var service = CreateServiceWithMockHttp(handler);
        var zteDevice = CreateZtePhysicalDevice();

        var result = await service.GetDeviceInformationAsync(zteDevice);

        Assert.True(result.IsAvailable);
        Assert.Equal("861234567890123", result.Imei);
        Assert.Equal("***********0123", result.MaskedImei); // Protected IMEI
        Assert.Equal("BD_MF935V1.0.0", result.FirmwareVersion);
        Assert.Equal("MF935-HW1", result.HardwareRevision);
        Assert.Equal(SimNetworkLockStatus.NetworkLocked, result.LockStatus);
        Assert.Equal(10, result.RetriesRemaining);
        Assert.True(result.CanUnlock);
        Assert.Equal("Vodafone", result.CarrierName);
    }

    [Fact]
    public async Task SimulatedDevice_ReturnsSimulatedTelemetry()
    {
        var handler = new TestHttpMessageHandler();
        var service = CreateServiceWithMockHttp(handler);
        var simDevice = CreateSimulatedDevice();

        var result = await service.GetDeviceInformationAsync(simDevice);

        Assert.True(result.IsAvailable);
        Assert.Contains("Simulated hardware telemetry", result.StatusMessage);
        Assert.NotNull(result.Imei);
        Assert.Equal("True", result.ExtendedProperties["SimulationMode"]);
    }

    [Fact]
    public async Task DisconnectedDevice_ReturnsUnavailableStatus()
    {
        var handler = new TestHttpMessageHandler();
        var service = CreateServiceWithMockHttp(handler);
        var disconnectedDevice = CreateZtePhysicalDevice(isConnected: false);

        var result = await service.GetDeviceInformationAsync(disconnectedDevice);

        Assert.False(result.IsAvailable);
        Assert.Equal("Device is disconnected.", result.StatusMessage);

        var unlockResult = await service.ApplyCarrierUnlockCodeAsync(disconnectedDevice, "1234567890");
        Assert.False(unlockResult.Success);
        Assert.Contains("disconnected", unlockResult.SummaryReport, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CarrierUnlock_InputValidation_RejectsInvalidAndDummyCodes()
    {
        var handler = new TestHttpMessageHandler();
        var service = CreateServiceWithMockHttp(handler);
        var zteDevice = CreateZtePhysicalDevice();

        // 1. Empty code
        var emptyRes = await service.ApplyCarrierUnlockCodeAsync(zteDevice, "");
        Assert.False(emptyRes.Success);
        Assert.Contains("cannot be empty", emptyRes.SummaryReport);

        // 2. Non-numeric
        var nonNumericRes = await service.ApplyCarrierUnlockCodeAsync(zteDevice, "ABCD1234EF");
        Assert.False(nonNumericRes.Success);
        Assert.Contains("Invalid code format", nonNumericRes.ErrorMessage);

        // 3. Too short (< 8 digits)
        var shortRes = await service.ApplyCarrierUnlockCodeAsync(zteDevice, "12345");
        Assert.False(shortRes.Success);
        Assert.Contains("Invalid code format", shortRes.ErrorMessage);

        // 4. Reject dummy brute-force guess pattern
        var dummyRes1 = await service.ApplyCarrierUnlockCodeAsync(zteDevice, "00000000");
        Assert.False(dummyRes1.Success);
        Assert.Contains("brute-force", dummyRes1.ErrorMessage);

        var dummyRes2 = await service.ApplyCarrierUnlockCodeAsync(zteDevice, "12345678");
        Assert.False(dummyRes2.Success);
        Assert.Contains("brute-force", dummyRes2.ErrorMessage);
    }

    [Fact]
    public async Task CarrierUnlock_WhenInterfaceUnavailable_AbortsTruthfullyWithoutSending()
    {
        var handler = new TestHttpMessageHandler
        {
            ResponseCallback = req => throw new HttpRequestException("Gateway offline")
        };
        var service = CreateServiceWithMockHttp(handler);
        var zteDevice = CreateZtePhysicalDevice();

        var res = await service.ApplyCarrierUnlockCodeAsync(zteDevice, "876543210987");

        Assert.False(res.Success);
        Assert.Contains("modem information interface unavailable", res.ErrorMessage);
        Assert.Equal(SimNetworkLockStatus.Unsupported, res.FinalLockStatus);
    }

    [Fact]
    public async Task CarrierUnlock_WhenAttemptsExhausted_PermanentlyBlocksSubmissions()
    {
        // Responds with 0 attempts remaining (PermanentlyBlocked)
        var jsonResponse = "{\"imei\":\"861234567890123\",\"wa_inner_version\":\"BD_MF935\",\"hardware_version\":\"MF935-HW1\",\"sim_lock_status\":\"2\",\"unlock_retry_num\":\"0\"}";
        var handler = new TestHttpMessageHandler
        {
            ResponseCallback = req => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            }
        };
        var service = CreateServiceWithMockHttp(handler);
        var zteDevice = CreateZtePhysicalDevice();

        var res = await service.ApplyCarrierUnlockCodeAsync(zteDevice, "987654321012");

        Assert.False(res.Success);
        Assert.Equal(SimNetworkLockStatus.PermanentlyBlocked, res.FinalLockStatus);
        Assert.Contains("permanently exhausted", res.ErrorMessage);
    }

    [Fact]
    public void SensitiveDataRedactor_ProtectsImei_InLogs()
    {
        var logMessage = "Query returned IMEI: 861234567890123 and port COM3";
        var redacted = _redactor.Redact(logMessage);

        Assert.DoesNotContain("861234567890123", redacted);
        Assert.Contains("0123", redacted); // Preserves technician last 4 digits
        Assert.Contains("[REDACTED_IMEI_", redacted);
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage>? ResponseCallback { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (ResponseCallback != null)
            {
                return Task.FromResult(ResponseCallback(request));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
