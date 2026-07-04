using RaspiMcp.Core.Models;

namespace RaspiMcp.Core.Interfaces;

/// <summary>Validates, executes, and audits commands on the current SSH host.</summary>
public interface ICommandExecutor
{
    Task<CommandResult> ExecuteAsync(string command, CancellationToken ct = default);
}
