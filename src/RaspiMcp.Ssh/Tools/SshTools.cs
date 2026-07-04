using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using RaspiMcp.Core.Configuration;
using RaspiMcp.Core.Interfaces;

namespace RaspiMcp.Ssh.Tools;

/// <summary>MCP tools for SSH access to a Raspberry Pi target host.</summary>
[McpServerToolType]
public class SshTools
{
    private readonly IHostManager _hostManager;
    private readonly ICommandExecutor _executor;
    private readonly IOptions<SshPluginOptions> _options;
    private readonly ILogger<SshTools> _logger;

    public SshTools(
        IHostManager hostManager,
        ICommandExecutor executor,
        IOptions<SshPluginOptions> options,
        ILogger<SshTools> logger)
    {
        _hostManager = hostManager;
        _executor = executor;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Converts any failure (validation rejection, SSH auth/connection errors, or
    /// unexpected bugs) into the same structured JSON payload, so a failure always
    /// carries a message the MCP client can read and relay — instead of an
    /// unhandled exception with no client-visible detail.
    /// </summary>
    private string HandleError(Exception ex)
    {
        _logger.LogWarning(ex, "Tool call failed");
        return JsonSerializer.Serialize(new
        {
            error = ex.Message,
            errorType = ex.GetType().Name,
            rejected = true
        });
    }

    [McpServerTool, Description("Returns the currently active SSH host: alias, IP address, and username.")]
    public string get_current_host()
    {
        var info = _hostManager.GetCurrentHost();
        return JsonSerializer.Serialize(new { info.Alias, info.Host, info.Username });
    }

    [McpServerTool, Description("Lists all configured host aliases. Credentials are never returned.")]
    public string list_hosts()
    {
        var aliases = _hostManager.GetHostAliases();
        return JsonSerializer.Serialize(new { hosts = aliases });
    }

    [McpServerTool,
     Description("Switches the active SSH host. REQUIRES USER APPROVAL — this changes the target of all subsequent commands. The alias must exist in configuration.")]
    public async Task<string> switch_host(
        [Description("The host alias to switch to, as defined in configuration.")] string hostAlias,
        CancellationToken ct = default)
    {
        try
        {
            var info = await _hostManager.SwitchHostAsync(hostAlias, ct);
            return JsonSerializer.Serialize(new
            {
                message = $"Switched to host '{info.Alias}'",
                info.Alias,
                info.Host,
                info.Username
            });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description("Executes a shell command on the current SSH host. Returns stdout, stderr, and exit code.")]
    public async Task<string> execute(
        [Description("The shell command to run on the remote host.")] string command,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync(command, ct);
            return JsonSerializer.Serialize(new
            {
                stdout = result.Stdout,
                stderr = result.Stderr,
                exitCode = result.ExitCode,
                durationMs = result.Duration.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description("Returns the contents of a file on the remote host.")]
    public async Task<string> read_file(
        [Description("Absolute path to the file on the remote host.")] string path,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync($"cat {EscapePath(path)}", ct);
            return FormatResult(result);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description("Lists directory contents with permissions, owner, size, and modification date (ls -lah).")]
    public async Task<string> list_directory(
        [Description("Absolute path to the directory on the remote host.")] string path,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync($"ls -lah {EscapePath(path)}", ct);
            return FormatResult(result);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description("Returns stat information for a path on the remote host.")]
    public async Task<string> stat(
        [Description("Absolute path to the file or directory.")] string path,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync($"stat {EscapePath(path)}", ct);
            return FormatResult(result);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description("Returns true if the path exists on the remote host, false otherwise.")]
    public async Task<string> file_exists(
        [Description("Absolute path to check.")] string path,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync(
                $"test -e {EscapePath(path)} && echo true || echo false", ct);
            var exists = result.Stdout.Trim() == "true";
            return JsonSerializer.Serialize(new { path, exists });
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description("Returns the last N lines of a log file (tail -n).")]
    public async Task<string> tail_log(
        [Description("Absolute path to the log file.")] string path,
        [Description("Number of lines to return (default 50).")] int lines = 50,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync($"tail -n {lines} {EscapePath(path)}", ct);
            return FormatResult(result);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description("Returns the systemctl status of a service.")]
    public async Task<string> systemctl_status(
        [Description("The systemd service name (e.g. nginx, sshd).")] string service,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync($"systemctl status {EscapeArg(service)}", ct);
            return FormatResult(result);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [McpServerTool, Description("Returns journal log entries for a service (journalctl -u).")]
    public async Task<string> journal(
        [Description("The systemd service name.")] string service,
        [Description("Number of log lines to return (default 100).")] int lines = 100,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _executor.ExecuteAsync(
                $"journalctl -u {EscapeArg(service)} -n {lines} --no-pager", ct);
            return FormatResult(result);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static string FormatResult(Core.Models.CommandResult result) =>
        JsonSerializer.Serialize(new
        {
            stdout = result.Stdout,
            stderr = result.Stderr,
            exitCode = result.ExitCode
        });

    private static string EscapePath(string path) => $"'{path.Replace("'", "'\\''")}'";
    private static string EscapeArg(string arg) => $"'{arg.Replace("'", "'\\''")}'";
}
