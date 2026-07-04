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

        services.AddSingleton<HostManager>();
        services.AddSingleton<IHostManager>(sp => sp.GetRequiredService<HostManager>());

        services.AddSingleton<ISshService, SshService>();

        // HostManager and SshService each need the other (HostManager forces a
        // reconnect on switch_host; SshService reads the current host config).
        // Injecting ISshService lazily into HostManager defers resolving it until
        // SwitchHostAsync actually runs, by which point HostManager's own
        // construction is already complete — this breaks the circular
        // dependency the DI container would otherwise detect at startup.
        services.AddSingleton(sp => new Lazy<ISshService>(() => sp.GetRequiredService<ISshService>()));
        services.AddSingleton<ICommandExecutor, CommandExecutor>();
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<ICommandValidator, CommandValidator>();
    }

    public void RegisterTools(IMcpToolRegistry registry) =>
        registry.Register<SshTools>();
}
