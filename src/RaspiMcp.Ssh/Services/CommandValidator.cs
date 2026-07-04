using System.Text.RegularExpressions;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Core.Models;

namespace RaspiMcp.Ssh.Services;

/// <summary>
/// Rejects shell commands that could cause irreversible damage.
/// Extend by subclassing and overriding <see cref="BlockedPatterns"/>.
/// </summary>
public class CommandValidator : ICommandValidator
{
    protected virtual IReadOnlyList<string> BlockedPatterns { get; } =
    [
        @"rm\s+-rf",
        @"\bmkfs\b",
        @"\bdd\b",
        @"\bshutdown\b",
        @"\breboot\b",
        @"\bpoweroff\b",
        @"\bhalt\b",
        @"\binit\s+0\b",
        @"\bsystemctl\s+(poweroff|reboot|halt)\b"
    ];

    public ValidationResult Validate(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return new ValidationResult(false, "Command cannot be empty.");

        foreach (var pattern in BlockedPatterns)
        {
            if (Regex.IsMatch(command, pattern, RegexOptions.IgnoreCase))
                return new ValidationResult(false,
                    $"Command rejected: matches blocked pattern '{pattern}'. This command could cause irreversible damage.");
        }

        return new ValidationResult(true);
    }
}
