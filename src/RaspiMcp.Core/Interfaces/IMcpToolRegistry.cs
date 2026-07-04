namespace RaspiMcp.Core.Interfaces;

/// <summary>Abstraction over the MCP SDK builder used by plugins to register tool types.</summary>
public interface IMcpToolRegistry
{
    void Register<TTool>() where TTool : class;
}
