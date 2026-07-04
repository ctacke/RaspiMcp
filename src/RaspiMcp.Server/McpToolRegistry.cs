using Microsoft.Extensions.DependencyInjection;
using RaspiMcp.Core.Interfaces;

namespace RaspiMcp.Server;

/// <summary>Bridges plugin tool registration to the MCP SDK builder.</summary>
public class McpToolRegistry : IMcpToolRegistry
{
    private readonly IMcpServerBuilder _builder;

    public McpToolRegistry(IMcpServerBuilder builder) => _builder = builder;

    public void Register<TTool>() where TTool : class =>
        _builder.WithTools<TTool>();
}
