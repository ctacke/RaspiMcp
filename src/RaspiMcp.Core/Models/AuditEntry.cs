namespace RaspiMcp.Core.Models;

/// <summary>Immutable audit record for a single command execution.</summary>
public record AuditEntry(
    DateTimeOffset Timestamp,
    string HostAlias,
    string Command,
    int ExitCode,
    TimeSpan Duration);
