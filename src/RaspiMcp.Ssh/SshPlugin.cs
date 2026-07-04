using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaspiMcp.Core.Configuration;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Ssh.Services;
using RaspiMcp.Ssh.Tools;

namespace RaspiMcp.Ssh;

/// <summary>Plugin entry point that wires up all SSH services and registers MCP tools.</summary>
public class SshPlugin : IMcpPlugin
{
    public string Name => "Ssh";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SshPluginOptions>(configuration.GetSection("Ssh"));

        // Register HostManager as both its concrete type and the IHostManager interface
        // so that SshService can depend on the concrete HostManager directly (avoiding
        // a circular dependency through ISshService ↔ IHostManager).
        services.AddSingleton<HostManager>();
        services.AddSingleton<IHostManager>(sp => sp.GetRequiredService<HostManager>());

        services.AddSingleton<ISshService, SshService>();
        services.AddSingleton<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<ICommandValidator, CommandValidator>();
    }

    public void RegisterTools(IMcpToolRegistry registry) =>
        registry.Register<SshTools>();
}
