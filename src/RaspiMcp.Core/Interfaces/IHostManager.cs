using RaspiMcp.Core.Models;

namespace RaspiMcp.Core.Interfaces;

/// <summary>Manages the active SSH target host.</summary>
public interface IHostManager
{
    HostInfo GetCurrentHost();
    IReadOnlyList<string> GetHostAliases();
    Task<HostInfo> SwitchHostAsync(string alias, CancellationToken ct = default);
}
