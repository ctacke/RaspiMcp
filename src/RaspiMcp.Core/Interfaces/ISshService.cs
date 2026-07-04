using RaspiMcp.Core.Models;

namespace RaspiMcp.Core.Interfaces;

/// <summary>Low-level SSH connection with auto-reconnect support.</summary>
public interface ISshService
{
    Task<CommandResult> ExecuteAsync(string command, CancellationToken ct = default);
    Task EnsureConnectedAsync(CancellationToken ct = default);
    void Disconnect();
}
