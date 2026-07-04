using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RaspiMcp.Core.Configuration;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Core.Models;

namespace RaspiMcp.Ssh.Services;

/// <summary>Thread-safe manager for the active SSH target host.</summary>
public class HostManager : IHostManager
{
    private readonly IOptionsMonitor<SshPluginOptions> _options;
    private readonly Lazy<ISshService> _sshService;
    private readonly ILogger<HostManager> _logger;
    private readonly object _lock = new();
    private string _currentAlias;

    public HostManager(
        IOptionsMonitor<SshPluginOptions> options,
        Lazy<ISshService> sshService,
        ILogger<HostManager> logger)
    {
        _options = options;
        _sshService = sshService;
        _logger = logger;
        _currentAlias = options.CurrentValue.CurrentHost;
    }

    public HostInfo GetCurrentHost()
    {
        lock (_lock)
        {
            var alias = _currentAlias;
            var config = GetHostConfig(alias);
            return new HostInfo(alias, config.Host, config.Username);
        }
    }

    public IReadOnlyList<string> GetHostAliases() =>
        _options.CurrentValue.Hosts.Keys.ToList().AsReadOnly();

    public async Task<HostInfo> SwitchHostAsync(string alias, CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Hosts.TryGetValue(alias, out var config))
            throw new InvalidOperationException(
                $"Unknown host alias: '{alias}'. Available: {string.Join(", ", opts.Hosts.Keys)}");

        string previousAlias;
        lock (_lock)
        {
            previousAlias = _currentAlias;
            _currentAlias = alias;
        }

        _sshService.Value.Disconnect();
        await _sshService.Value.EnsureConnectedAsync(ct);

        _logger.LogInformation("Host switched from '{Previous}' to '{New}' ({Host})",
            previousAlias, alias, config.Host);

        return new HostInfo(alias, config.Host, config.Username);
    }

    /// <summary>Gets the current alias (used internally by SshService).</summary>
    public string CurrentAlias
    {
        get { lock (_lock) return _currentAlias; }
    }

    /// <summary>Gets the full config for the current host (used internally by SshService).</summary>
    internal HostConfig GetCurrentHostConfig()
    {
        lock (_lock) return GetHostConfig(_currentAlias);
    }

    private HostConfig GetHostConfig(string alias)
    {
        var opts = _options.CurrentValue;
        if (!opts.Hosts.TryGetValue(alias, out var config))
            throw new InvalidOperationException($"Unknown host alias: '{alias}'");
        return config;
    }
}
