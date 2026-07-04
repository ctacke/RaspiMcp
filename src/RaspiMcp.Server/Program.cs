using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RaspiMcp.Server;

var builder = Host.CreateApplicationBuilder(args);

// All logging goes to stderr so it never corrupts the stdio MCP protocol
builder.Logging.AddConsole(options =>
    options.LogToStandardErrorThreshold = LogLevel.Trace);

// Register MCP server with stdio transport
var mcpBuilder = builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

// Bootstrap a temporary logger for the plugin loader (before Build())
using var loggerFactory = LoggerFactory.Create(b =>
    b.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace));
var logger = loggerFactory.CreateLogger<PluginLoader>();

var registry = new McpToolRegistry(mcpBuilder);
var loader = new PluginLoader(registry, builder.Services, builder.Configuration, logger);
loader.LoadPlugins(AppContext.BaseDirectory);

await builder.Build().RunAsync();
