// File: Ogur.Core/Hub/HubClient.cs
// Project: Ogur.Core
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Abstractions.Hub;

namespace Ogur.Core.Hub;

/// <summary>
/// SignalR client for real-time hub connection.
/// </summary>
public sealed class HubClient : IHubClient, IAsyncDisposable
{
    private readonly HubOptions _options;
    private readonly IDeviceFingerprintProvider _fingerprintProvider;
    private readonly ILogger<HubClient> _logger;
    private HubConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="HubClient"/> class.
    /// </summary>
    /// <param name="options">Hub configuration options.</param>
    /// <param name="fingerprintProvider">Device fingerprint provider.</param>
    /// <param name="logger">Logger instance.</param>
    public HubClient(
        IOptions<HubOptions> options,
        IDeviceFingerprintProvider fingerprintProvider,
        ILogger<HubClient> logger)
    {
        _options = options.Value;
        _fingerprintProvider = fingerprintProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <inheritdoc />
    public async Task<HubConnectionResult> ConnectAsync(CancellationToken ct)
    {
        if (!_options.EnableSignalR)
        {
            _logger.LogInformation("SignalR is disabled in configuration");
            return HubConnectionResult.Failure("SignalR disabled");
        }

        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_connection is not null && _connection.State == HubConnectionState.Connected)
            {
                _logger.LogDebug("Already connected to hub");
                return HubConnectionResult.Success(_connection.ConnectionId ?? string.Empty);
            }

            var fingerprint = await _fingerprintProvider.GetFingerprintAsync(ct);
            
            _connection = new HubConnectionBuilder()
                .WithUrl($"{_options.HubUrl}/hubs/devices", options =>
                {
                    options.Headers.Add("X-Api-Key", _options.ApiKey);
                    options.Headers.Add("X-Application-Name", _options.ApplicationName);
                    options.Headers.Add("X-Device-Hwid", fingerprint.Hwid);
                    options.Headers.Add("X-Device-Guid", fingerprint.Guid);
                })
                .WithAutomaticReconnect(new[] 
                { 
                    TimeSpan.FromSeconds(_options.ReconnectDelaySeconds),
                    TimeSpan.FromSeconds(_options.ReconnectDelaySeconds * 2),
                    TimeSpan.FromSeconds(_options.ReconnectDelaySeconds * 4)
                })
                .Build();

            _connection.Closed += async (error) =>
            {
                _logger.LogWarning(error, "SignalR connection closed");
                await Task.CompletedTask;
            };

            _connection.Reconnecting += async (error) =>
            {
                _logger.LogWarning(error, "SignalR reconnecting");
                await Task.CompletedTask;
            };

            _connection.Reconnected += async (connectionId) =>
            {
                _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
                await Task.CompletedTask;
            };

            await _connection.StartAsync(ct);
            
            _logger.LogInformation("Connected to hub: {ConnectionId}", _connection.ConnectionId);
            return HubConnectionResult.Success(_connection.ConnectionId ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to hub");
            return HubConnectionResult.Failure(ex.Message);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken ct)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_connection is not null)
            {
                await _connection.StopAsync(ct);
                await _connection.DisposeAsync();
                _connection = null;
                _logger.LogInformation("Disconnected from hub");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HubCommand> ListenForCommandsAsync(CancellationToken ct)
    {
        if (_connection is null || _connection.State != HubConnectionState.Connected)
        {
            _logger.LogWarning("Cannot listen for commands: not connected");
            yield break;
        }

        var channel = System.Threading.Channels.Channel.CreateUnbounded<HubCommand>();

        _connection.On<HubCommand>("ReceiveCommand", command =>
        {
            _logger.LogInformation("Received command: {Type} (ID: {CommandId})", command.Type, command.CommandId);
            channel.Writer.TryWrite(command);
        });

        await foreach (var command in channel.Reader.ReadAllAsync(ct))
        {
            yield return command;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
        _connectionLock.Dispose();
    }
}