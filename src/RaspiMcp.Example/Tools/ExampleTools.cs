using System.ComponentModel;
using ModelContextProtocol.Server;

namespace RaspiMcp.Example.Tools;

/// <summary>Example tools demonstrating how a plugin exposes MCP capabilities.</summary>
[McpServerToolType]
public class ExampleTools
{
    [McpServerTool, Description("Returns a greeting. Use this to verify the plugin system is working.")]
    public string hello(
        [Description("Name to greet (optional, defaults to 'World').")] string name = "World") =>
        $"Hello, {name}! The RaspiMcp plugin system is working correctly.";
}
