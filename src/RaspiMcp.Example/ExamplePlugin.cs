using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Example.Tools;

namespace RaspiMcp.Example;

/// <summary>
/// Minimal example plugin showing how to create a third-party extension.
/// Reference RaspiMcp.Core and implement IMcpPlugin to get started.
/// </summary>
public class ExamplePlugin : IMcpPlugin
{
    public string Name => "Example";

    public void Register(IServiceCollection services, IConfiguration configuration) { }

    public void RegisterTools(IMcpToolRegistry registry) =>
        registry.Register<ExampleTools>();
}
