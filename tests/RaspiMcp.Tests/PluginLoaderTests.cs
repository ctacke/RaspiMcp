using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Server;
using Xunit;

namespace RaspiMcp.Tests;

public class PluginLoaderTests
{
    [Fact]
    public void LoadPlugins_ValidPlugin_RegistersServicesAndTools()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var registry = new Mock<IMcpToolRegistry>();
        var loader = new PluginLoader(registry.Object, services, config,
            NullLogger<PluginLoader>.Instance);

        // Load only the Example plugin assembly which has no external deps at test time
        var assembly = typeof(RaspiMcp.Example.ExamplePlugin).Assembly;
        loader.LoadFromAssembly(assembly, "test");

        registry.Verify(r => r.Register<RaspiMcp.Example.Tools.ExampleTools>(), Times.Once);
    }

    [Fact]
    public void LoadPlugins_NonExistentPluginsDir_DoesNotThrow()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var registry = new Mock<IMcpToolRegistry>();
        var loader = new PluginLoader(registry.Object, services, config,
            NullLogger<PluginLoader>.Instance);

        // Should not throw even if plugins/ directory doesn't exist
        var ex = Record.Exception(() => loader.LoadPlugins(Path.GetTempPath()));
        Assert.Null(ex);
    }
}
