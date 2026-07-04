using RaspiMcp.Core.Models;

namespace RaspiMcp.Core.Interfaces;

/// <summary>Safety gate that rejects dangerous commands before execution.</summary>
public interface ICommandValidator
{
    ValidationResult Validate(string command);
}
