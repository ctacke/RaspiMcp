using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RaspiMcp.Core.Interfaces;

namespace RaspiMcp.Server;

/// <summary>
/// Discovers and loads IMcpPlugin implementations from built-in assemblies
/// and external DLLs in the plugins/ drop folder.
/// </summary>
public class PluginLoader
{
    private readonly IMcpToolRegistry _registry;
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PluginLoader> _logger;

    public PluginLoader(
        IMcpToolRegistry registry,
        IServiceCollection services,
        IConfiguration configuration,
        ILogger<PluginLoader> logger)
    {
        _registry = registry;
        _services = services;
        _configuration = configuration;
        _logger = logger;
    }

    public void LoadPlugins(string baseDirectory)
    {
        // Built-in plugins loaded by assembly reference
        var builtIn = new Assembly[]
        {
            typeof(RaspiMcp.Ssh.SshPlugin).Assembly,
            typeof(RaspiMcp.Example.ExamplePlugin).Assembly,
        };

        foreach (var assembly in builtIn)
            LoadFromAssembly(assembly, source: "built-in");

        // External plugins from plugins/ drop folder
        var pluginsDir = Path.Combine(baseDirectory, "plugins");
        if (!Directory.Exists(pluginsDir)) return;

        foreach (var dll in Directory.GetFiles(pluginsDir, "RaspiMcp.*.dll"))
        {
            try
            {
                var assembly = Assembly.LoadFrom(dll);
                LoadFromAssembly(assembly, source: dll);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugin assembly '{Path}' — skipping", dll);
            }
        }
    }

    public void LoadFromAssembly(Assembly assembly, string source)
    {
        var pluginTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IMcpPlugin).IsAssignableFrom(t));

        foreach (var type in pluginTypes)
        {
            try
            {
                var plugin = (IMcpPlugin)Activator.CreateInstance(type)!;
                plugin.Register(_services, _configuration);
                plugin.RegisterTools(_registry);
                _logger.LogInformation("Plugin '{Name}' loaded from {Source}", plugin.Name, source);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Plugin '{Type}' from '{Source}' failed to load — skipping",
                    type.Name, source);
            }
        }
    }
}
