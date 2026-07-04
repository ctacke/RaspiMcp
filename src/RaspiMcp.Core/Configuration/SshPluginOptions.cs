namespace RaspiMcp.Core.Configuration;

/// <summary>Configuration for the SSH plugin, bound from the "Ssh" appsettings section.</summary>
public class SshPluginOptions
{
    public string CurrentHost { get; set; } = string.Empty;
    public Dictionary<string, HostConfig> Hosts { get; set; } = new();
    public int CommandTimeoutSeconds { get; set; } = 30;
    public int MaxReconnectAttempts { get; set; } = 3;
}
