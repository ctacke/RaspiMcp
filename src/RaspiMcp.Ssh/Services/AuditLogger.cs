using Microsoft.Extensions.Logging;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Core.Models;

namespace RaspiMcp.Ssh.Services;

/// <summary>Writes structured audit records for every executed command.</summary>
public class AuditLogger : IAuditLogger
{
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(ILogger<AuditLogger> logger) => _logger = logger;

    public void LogCommand(AuditEntry entry) =>
        _logger.LogInformation(
            "[AUDIT] Timestamp={Timestamp} Host={Host} ExitCode={ExitCode} Duration={Duration}ms Command={Command}",
            entry.Timestamp.ToString("O"),
            entry.HostAlias,
            entry.ExitCode,
            entry.Duration.TotalMilliseconds.ToString("F1"),
            entry.Command);
}
