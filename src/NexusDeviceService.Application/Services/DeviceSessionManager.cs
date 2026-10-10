using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Events;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Manages connection lifetime and session lifecycle for connected hardware/simulated devices.
/// </summary>
public sealed class DeviceSessionManager : IDeviceSessionManager
{
    private readonly ILogger<DeviceSessionManager> _logger;
    private IDeviceSession? _currentSession;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public IDeviceSession? CurrentSession => _currentSession;
    public event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

    public DeviceSessionManager(ILogger<DeviceSessionManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IDeviceSession> OpenSessionAsync(
        IDeviceProvider provider,
        DeviceIdentifier identifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(identifier);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentSession != null)
            {
                _logger.LogInformation("Closing existing session for {Device} before opening new session", _currentSession.Identifier);
                await CloseCurrentSessionInternalAsync(cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Creating connection for {Device} via provider {Provider}", identifier, provider.DisplayName);
            var connection = await provider.CreateConnectionAsync(identifier, cancellationToken).ConfigureAwait(false);
            var profile = await provider.GetDeviceProfileAsync(identifier, cancellationToken).ConfigureAwait(false);

            await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

            var session = new DeviceSession(profile, connection);
            _currentSession = session;

            connection.StateChanged += OnConnectionStateChanged;
            SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(session, connection.State));

            _logger.LogInformation("Successfully opened session for {Device}", identifier);
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CloseCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CloseCurrentSessionInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CloseCurrentSessionInternalAsync(CancellationToken cancellationToken)
    {
        if (_currentSession == null)
        {
            return;
        }

        var session = _currentSession;
        _currentSession = null;

        session.Connection.StateChanged -= OnConnectionStateChanged;
        try
        {
            await session.Connection.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error occurred while closing session for {Device}", session.Identifier);
        }
        finally
        {
            SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(null, ConnectionState.Disconnected));
        }
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(_currentSession, state));
    }
}
