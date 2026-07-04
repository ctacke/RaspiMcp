using RaspiMcp.Core.Models;

namespace RaspiMcp.Core.Interfaces;

/// <summary>Records every executed command for audit purposes.</summary>
public interface IAuditLogger
{
    void LogCommand(AuditEntry entry);
}
