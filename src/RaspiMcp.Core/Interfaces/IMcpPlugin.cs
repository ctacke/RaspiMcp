using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RaspiMcp.Core.Interfaces;

/// <summary>Implemented by every plugin to register services and tools with the server.</summary>
public interface IMcpPlugin
{
    string Name { get; }
    void Register(IServiceCollection services, IConfiguration configuration);
    void RegisterTools(IMcpToolRegistry registry);
}
