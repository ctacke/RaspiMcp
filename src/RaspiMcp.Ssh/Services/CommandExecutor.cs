using System.Diagnostics;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Core.Models;

namespace RaspiMcp.Ssh.Services;

/// <summary>Validates, executes, and audits every command on the active SSH host.</summary>
public class CommandExecutor : ICommandExecutor
{
    private readonly ISshService _sshService;
    private readonly ICommandValidator _validator;
    private readonly IAuditLogger _auditLogger;
    private readonly IHostManager _hostManager;

    public CommandExecutor(
        ISshService sshService,
        ICommandValidator validator,
        IAuditLogger auditLogger,
        IHostManager hostManager)
    {
        _sshService = sshService;
        _validator = validator;
        _auditLogger = auditLogger;
        _hostManager = hostManager;
    }

    public async Task<CommandResult> ExecuteAsync(string command, CancellationToken ct = default)
    {
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.RejectionReason ?? "Command rejected.");

        var result = await _sshService.ExecuteAsync(command, ct);

        _auditLogger.LogCommand(new AuditEntry(
            DateTimeOffset.UtcNow,
            _hostManager.GetCurrentHost().Alias,
            command,
            result.ExitCode,
            result.Duration));

        return result;
    }
}
