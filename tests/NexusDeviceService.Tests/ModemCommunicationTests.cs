using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.Modem;
using Xunit;

namespace NexusDeviceService.Tests;

public class ModemCommunicationTests
{
    [Fact]
    public async Task ConnectAndDisconnect_ManagesLifecycleCorrectly()
    {
        var mockTransport = new MockModemTransport();
        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        Assert.False(channel.IsConnected);

        await channel.ConnectAsync("COM3", 115200);

        Assert.True(channel.IsConnected);
        Assert.Equal("COM3", channel.CurrentPort);
        Assert.Contains("AT", mockTransport.CommandHistory);

        await channel.DisconnectAsync();

        Assert.False(channel.IsConnected);
    }

    [Fact]
    public async Task QueryIdentityAsync_ParsesQuectelIdentityAndRedactsImei()
    {
        var mockTransport = new MockModemTransport();
        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        await channel.ConnectAsync("COM3");

        var identity = await channel.QueryIdentityAsync();

        Assert.NotNull(identity);
        Assert.Equal("Quectel", identity.Manufacturer);
        Assert.Equal("EC25", identity.Model);
        Assert.Equal("EC25EFAR06A03M4G", identity.FirmwareVersion);
        // Full IMEI 867890041234567 must be masked by redactor
        Assert.DoesNotContain("867890041234567", identity.Imei);
        Assert.Equal("[REDACTED_IMEI_****4567]", identity.Imei);
    }

    [Fact]
    public async Task QuerySignalQualityAsync_ParsesRssiAndDbmAccurately()
    {
        var mockTransport = new MockModemTransport();
        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        await channel.ConnectAsync("COM3");

        var signal = await channel.QuerySignalQualityAsync();

        Assert.Equal(24, signal.Rssi);
        Assert.Equal(-65, signal.Dbm); // -113 + (24 * 2) = -65 dBm
        Assert.Equal(99, signal.BitErrorRate);
        Assert.Equal("Excellent", signal.QualityDescription);
    }

    [Fact]
    public async Task QuerySimStatusAsync_ParsesCpinStatus()
    {
        var mockTransport = new MockModemTransport();
        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        await channel.ConnectAsync("COM3");

        var simStatus = await channel.QuerySimStatusAsync();
        Assert.Equal(SimPinState.Ready, simStatus);

        // Test PIN Required status
        mockTransport.SetResponse("AT+CPIN?", "\r\n+CPIN: SIM PIN\r\n\r\nOK\r\n");
        var pinStatus = await channel.QuerySimStatusAsync();
        Assert.Equal(SimPinState.PinRequired, pinStatus);
    }

    [Fact]
    public async Task QueryOperatorAsync_ParsesNetworkName()
    {
        var mockTransport = new MockModemTransport();
        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        await channel.ConnectAsync("COM3");

        var oper = await channel.QueryOperatorAsync();
        Assert.Equal("Vodafone UK", oper.OperatorName);
        Assert.Equal(0, oper.Mode);
        Assert.Equal(0, oper.Format);
    }

    [Fact]
    public async Task ExecuteCommandAsync_ThrowsNotSupportedException_WhenErrorReturned()
    {
        var mockTransport = new MockModemTransport();
        mockTransport.SimulateErrorForCommand = "AT+CMEE=2";

        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        await channel.ConnectAsync("COM3");

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            await channel.ExecuteCommandAsync("AT+CMEE=2");
        });
    }

    [Fact]
    public async Task ExecuteCommandAsync_SerializesAccess_PreventingOverlappingCommands()
    {
        var mockTransport = new MockModemTransport();
        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        await channel.ConnectAsync("COM3");

        // Fire multiple concurrent command queries
        var tasks = Enumerable.Range(0, 10).Select(i => channel.ExecuteCommandAsync("AT")).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(10, results.Length);
        Assert.All(results, res => Assert.Contains("OK", res));
    }

    [Fact]
    public async Task ExecuteCommandAsync_TimesOut_WhenTransportDoesNotRespond()
    {
        var mockTransport = new MockModemTransport
        {
            SimulateTimeout = true
        };

        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await channel.ConnectAsync("COM3");
        });
    }

    [Fact]
    public async Task ExecuteCommandAsync_RespectsCancellationToken()
    {
        var mockTransport = new MockModemTransport();
        var redactor = new SensitiveDataRedactor();
        var channel = new ModemCommunicationChannel(mockTransport, redactor, NullLogger<ModemCommunicationChannel>.Instance);

        await channel.ConnectAsync("COM3");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await channel.ExecuteCommandAsync("AT", cancellationToken: cts.Token);
        });
    }
}
