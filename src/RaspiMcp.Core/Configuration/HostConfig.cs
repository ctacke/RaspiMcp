namespace RaspiMcp.Core.Configuration;

/// <summary>Connection settings for one SSH target host.</summary>
public class HostConfig
{
    public string Host { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? PrivateKey { get; set; }
    public string? Password { get; set; }
}
