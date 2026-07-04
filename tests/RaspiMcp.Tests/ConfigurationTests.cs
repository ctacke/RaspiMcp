using Microsoft.Extensions.Configuration;
using RaspiMcp.Core.Configuration;
using Xunit;

namespace RaspiMcp.Tests;

public class ConfigurationTests
{
    [Fact]
    public void SshPluginOptions_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ssh:CurrentHost"] = "my-pi",
                ["Ssh:CommandTimeoutSeconds"] = "60",
                ["Ssh:Hosts:my-pi:Host"] = "192.168.1.99",
                ["Ssh:Hosts:my-pi:Username"] = "pi",
                ["Ssh:Hosts:my-pi:PrivateKey"] = "/home/user/.ssh/id_ed25519"
            })
            .Build();

        var opts = new SshPluginOptions();
        config.GetSection("Ssh").Bind(opts);

        Assert.Equal("my-pi", opts.CurrentHost);
        Assert.Equal(60, opts.CommandTimeoutSeconds);
        Assert.True(opts.Hosts.ContainsKey("my-pi"));
        Assert.Equal("192.168.1.99", opts.Hosts["my-pi"].Host);
        Assert.Equal("pi", opts.Hosts["my-pi"].Username);
        Assert.Equal("/home/user/.ssh/id_ed25519", opts.Hosts["my-pi"].PrivateKey);
    }

    [Fact]
    public void SshPluginOptions_PasswordAuth_BindsCorrectly()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ssh:CurrentHost"] = "vehicle-pi",
                ["Ssh:Hosts:vehicle-pi:Host"] = "192.168.1.43",
                ["Ssh:Hosts:vehicle-pi:Username"] = "pi",
                ["Ssh:Hosts:vehicle-pi:Password"] = "secretpassword"
            })
            .Build();

        var opts = new SshPluginOptions();
        config.GetSection("Ssh").Bind(opts);

        Assert.Equal("secretpassword", opts.Hosts["vehicle-pi"].Password);
        Assert.Null(opts.Hosts["vehicle-pi"].PrivateKey);
    }

    [Fact]
    public void SshPluginOptions_Defaults_AreReasonable()
    {
        var opts = new SshPluginOptions();
        Assert.Equal(30, opts.CommandTimeoutSeconds);
        Assert.Equal(3, opts.MaxReconnectAttempts);
        Assert.Empty(opts.Hosts);
    }
}
