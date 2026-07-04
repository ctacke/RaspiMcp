namespace RaspiMcp.Core.Models;

/// <summary>Structured result from a remote command execution.</summary>
public record CommandResult(string Stdout, string Stderr, int ExitCode, TimeSpan Duration);
