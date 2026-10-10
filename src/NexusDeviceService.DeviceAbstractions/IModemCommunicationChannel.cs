using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// High-level serialized communication channel for standard 3GPP and V.25ter modem commands.
/// Enforces serialized mutex access, timeout policies, cancellation, structured logging, and response parsing.
/// </summary>
public interface IModemCommunicationChannel : IAsyncDisposable
{
    bool IsConnected { get; }
    string? CurrentPort { get; }

    Task ConnectAsync(string portName, int baudRate = 115200, CancellationToken cancellationToken = default);
    Task DisconnectAsync();

    Task<string> ExecuteCommandAsync(string command, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    Task<ModemIdentityInfo> QueryIdentityAsync(CancellationToken cancellationToken = default);
    Task<ModemSignalInfo> QuerySignalQualityAsync(CancellationToken cancellationToken = default);
    Task<SimPinState> QuerySimStatusAsync(CancellationToken cancellationToken = default);
    Task<ModemOperatorInfo> QueryOperatorAsync(CancellationToken cancellationToken = default);
}
