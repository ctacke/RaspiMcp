namespace RaspiMcp.Core.Models;

/// <summary>Public host information returned to callers — never includes credentials.</summary>
public record HostInfo(string Alias, string Host, string Username);
