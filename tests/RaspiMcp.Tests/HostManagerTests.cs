using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RaspiMcp.Core.Configuration;
using RaspiMcp.Core.Interfaces;
using RaspiMcp.Ssh.Services;
using Xunit;

namespace RaspiMcp.Tests;

public class HostManagerTests
{
    private static IOptionsMonitor<SshPluginOptions> CreateOptions(SshPluginOptions opts)
    {
        var monitor = new Mock<IOptionsMonitor<SshPluginOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(opts);
        return monitor.Object;
    }

    private static SshPluginOptions TwoHostOptions() => new()
    {
        CurrentHost = "host-a",
        Hosts = new()
        {
            ["host-a"] = new HostConfig { Host = "10.0.0.1", Username = "user1" },
            ["host-b"] = new HostConfig { Host = "10.0.0.2", Username = "user2" }
        }
    };

    [Fact]
    public void GetCurrentHost_ReturnsInitialHost()
    {
        var ssh = new Mock<ISshService>();
        var manager = new HostManager(CreateOptions(TwoHostOptions()), new Lazy<ISshService>(() => ssh.Object),
            NullLogger<HostManager>.Instance);

        var info = manager.GetCurrentHost();

        Assert.Equal("host-a", info.Alias);
        Assert.Equal("10.0.0.1", info.Host);
        Assert.Equal("user1", info.Username);
    }

    [Fact]
    public async Task SwitchHostAsync_ValidAlias_UpdatesHost()
    {
        var ssh = new Mock<ISshService>();
        ssh.Setup(s => s.EnsureConnectedAsync(default)).Returns(Task.CompletedTask);
        var manager = new HostManager(CreateOptions(TwoHostOptions()), new Lazy<ISshService>(() => ssh.Object),
            NullLogger<HostManager>.Instance);

        var info = await manager.SwitchHostAsync("host-b");

        Assert.Equal("host-b", info.Alias);
        Assert.Equal("10.0.0.2", info.Host);
        Assert.Equal("user2", info.Username);
    }

    [Fact]
    public async Task SwitchHostAsync_InvalidAlias_Throws()
    {
        var ssh = new Mock<ISshService>();
        var manager = new HostManager(CreateOptions(TwoHostOptions()), new Lazy<ISshService>(() => ssh.Object),
            NullLogger<HostManager>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.SwitchHostAsync("nonexistent"));
    }

    [Fact]
    public async Task SwitchHostAsync_DisconnectsBeforeSwitching()
    {
        var ssh = new Mock<ISshService>();
        ssh.Setup(s => s.EnsureConnectedAsync(default)).Returns(Task.CompletedTask);
        var manager = new HostManager(CreateOptions(TwoHostOptions()), new Lazy<ISshService>(() => ssh.Object),
            NullLogger<HostManager>.Instance);

        await manager.SwitchHostAsync("host-b");

        ssh.Verify(s => s.Disconnect(), Times.Once);
    }

    [Fact]
    public void GetHostAliases_ReturnsAllConfiguredAliases()
    {
        var ssh = new Mock<ISshService>();
        var manager = new HostManager(CreateOptions(TwoHostOptions()), new Lazy<ISshService>(() => ssh.Object),
            NullLogger<HostManager>.Instance);

        var aliases = manager.GetHostAliases();

        Assert.Contains("host-a", aliases);
        Assert.Contains("host-b", aliases);
        Assert.Equal(2, aliases.Count);
    }
}
