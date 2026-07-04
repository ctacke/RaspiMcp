namespace RaspiMcp.Core.Models;

/// <summary>Outcome of command safety validation.</summary>
public record ValidationResult(bool IsValid, string? RejectionReason = null);
