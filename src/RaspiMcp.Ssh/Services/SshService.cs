using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;
using RaspiMcp.Core.Configuration;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Core.Models;

namespace RaspiMcp.Ssh.Services;

/// <summary>Manages the SSH connection lifecycle with automatic reconnect on failure.</summary>
public class SshService : ISshService, IDisposable
{
    private readonly HostManager _hostManager;
    private readonly IOptionsMonitor<SshPluginOptions> _options;
    private readonly ILogger<SshService> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private SshClient? _client;
    private bool _disposed;

    public SshService(
        HostManager hostManager,
        IOptionsMonitor<SshPluginOptions> options,
        ILogger<SshService> logger)
    {
        _hostManager = hostManager;
        _options = options;
        _logger = logger;
    }

    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            if (_client is { IsConnected: true }) return;

            _client?.Dispose();
            _client = CreateClient(_hostManager.GetCurrentHostConfig());

            _logger.LogInformation("Connecting to SSH host '{Alias}' at {Host}",
                _hostManager.CurrentAlias, _hostManager.GetCurrentHost().Host);

            await _client.ConnectAsync(ct);
            _logger.LogInformation("SSH connection established to '{Alias}'", _hostManager.CurrentAlias);
        }
        catch (SshAuthenticationException ex)
        {
            _logger.LogError(ex, "Authentication failed for host '{Alias}'", _hostManager.CurrentAlias);
            throw new InvalidOperationException(
                $"Authentication failed for host '{_hostManager.CurrentAlias}': {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to connect to host '{Alias}'", _hostManager.CurrentAlias);
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<CommandResult> ExecuteAsync(string command, CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        return await ExecuteWithRetryAsync(command, ct);
    }

    private async Task<CommandResult> ExecuteWithRetryAsync(string command, CancellationToken ct, bool isRetry = false)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var sshCommand = _client!.CreateCommand(command);
            sshCommand.CommandTimeout = TimeSpan.FromSeconds(_options.CurrentValue.CommandTimeoutSeconds);

            await sshCommand.ExecuteAsync(ct);
            sw.Stop();

            return new CommandResult(
                sshCommand.Result ?? string.Empty,
                sshCommand.Error ?? string.Empty,
                sshCommand.ExitStatus ?? 0,
                sw.Elapsed);
        }
        catch (SshConnectionException ex) when (!isRetry)
        {
            _logger.LogWarning(ex, "SSH connection lost, reconnecting and retrying command");
            await _connectionLock.WaitAsync(ct);
            try
            {
                _client?.Dispose();
                _client = null;
            }
            finally
            {
                _connectionLock.Release();
            }
            await EnsureConnectedAsync(ct);
            return await ExecuteWithRetryAsync(command, ct, isRetry: true);
        }
        catch (SshConnectionException ex)
        {
            throw new InvalidOperationException("SSH connection failed after reconnect attempt.", ex);
        }
    }

    public void Disconnect()
    {
        _connectionLock.Wait();
        try
        {
            _client?.Disconnect();
            _client?.Dispose();
            _client = null;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private SshClient CreateClient(HostConfig config)
    {
        ConnectionInfo connectionInfo;

        if (!string.IsNullOrWhiteSpace(config.PrivateKey))
        {
            var keyFile = new PrivateKeyFile(config.PrivateKey);
            var authMethod = new PrivateKeyAuthenticationMethod(config.Username, keyFile);
            connectionInfo = new ConnectionInfo(config.Host, config.Username, authMethod);
        }
        else if (!string.IsNullOrWhiteSpace(config.Password))
        {
            var authMethod = new PasswordAuthenticationMethod(config.Username, config.Password);
            connectionInfo = new ConnectionInfo(config.Host, config.Username, authMethod);
        }
        else
        {
            throw new InvalidOperationException(
                $"Host '{_hostManager.CurrentAlias}' has neither PrivateKey nor Password configured.");
        }

        return new SshClient(connectionInfo);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client?.Dispose();
        _connectionLock.Dispose();
    }
}
