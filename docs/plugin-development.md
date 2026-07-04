# Plugin Development Guide

This guide shows how to build a custom RaspiMcp plugin and deploy it alongside the server.

## Overview

A plugin is a .NET class library that:
1. References `RaspiMcp.Core` (interfaces and models — no SSH or server dependencies)
2. Implements `IMcpPlugin` to register services and tools
3. Exposes one or more `[McpServerToolType]` classes with `[McpServerTool]`-attributed methods
4. Gets deployed as a DLL into the `plugins/` folder next to the server binary

---

## Step 1: Create the Project

```bash
dotnet new classlib -n MyCompany.RaspiPlugin --framework net10.0
cd MyCompany.RaspiPlugin
dotnet add package ModelContextProtocol
dotnet add reference ../path/to/RaspiMcp.Core/RaspiMcp.Core.csproj
# OR once published to NuGet:
# dotnet add package RaspiMcp.Core
```

---

## Step 2: Implement IMcpPlugin

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaspiMcp.Core.Interfaces;

namespace MyCompany.RaspiPlugin;

public class MyPlugin : IMcpPlugin
{
    public string Name => "MyPlugin";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Bind your own config section
        services.Configure<MyPluginOptions>(configuration.GetSection("MyPlugin"));
        // Register your own services
        services.AddSingleton<IMyService, MyService>();
    }

    public void RegisterTools(IMcpToolRegistry registry) =>
        registry.Register<MyTools>();
}
```

### Configuration convention

Your plugin reads from its own top-level key in `appsettings.json`:

```json
{
  "MyPlugin": {
    "ApiEndpoint": "http://localhost:8080",
    "TimeoutSeconds": 10
  }
}
```

---

## Step 3: Create the Tool Class

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MyCompany.RaspiPlugin;

[McpServerToolType]
public class MyTools
{
    private readonly IMyService _myService;

    // Constructor injection is supported — services registered in Register() are available
    public MyTools(IMyService myService)
    {
        _myService = myService;
    }

    [McpServerTool, Description("Returns the current sensor reading from the Pi GPIO pin.")]
    public async Task<string> read_sensor(
        [Description("GPIO pin number to read.")] int pin,
        CancellationToken ct = default)
    {
        var value = await _myService.ReadPinAsync(pin, ct);
        return System.Text.Json.JsonSerializer.Serialize(new { pin, value });
    }

    [McpServerTool, Description("Returns a static greeting from the MyPlugin plugin.")]
    public string ping() => "MyPlugin is alive!";
}
```

### Tool method conventions

| Rule | Detail |
|------|--------|
| Attribute | `[McpServerTool]` on each method |
| Description | Always include `[Description("...")]` — it appears in the MCP tool list |
| Return type | `string`, `Task<string>`, or any JSON-serializable type |
| CancellationToken | Optional last parameter — automatically injected by the MCP SDK |
| Constructor DI | The tool class is instantiated per-call via DI; inject services freely |
| Error handling | Return a JSON error object rather than throwing, for graceful degradation |

---

## Step 4: Deploy

Build the plugin in Release mode:

```bash
dotnet publish -c Release -o ./publish
```

Copy the plugin DLL (and any non-framework dependencies) into the `plugins/` folder next to the server binary:

```
RaspiMcp.Server.exe
plugins/
  MyCompany.RaspiPlugin.dll
  MyCompany.SomeDependency.dll   ← if not already in the framework
```

The server scans `plugins/RaspiMcp.*.dll` on startup. Rename your output DLL to match that pattern (`RaspiMcp.MyPlugin.dll`) or adjust the glob in `PluginLoader.cs`.

---

## Step 5: Verify

Start the server and use the `hello` tool (from the built-in Example plugin) to confirm the plugin system loads, then call your own tool.

If your plugin fails to load, the server logs an error at startup:

```
[ERR] Plugin 'MyTools' from 'plugins/MyCompany.RaspiPlugin.dll' failed to load — skipping
```

Check the inner exception for DI resolution errors or missing assemblies.

---

## Complete Worked Example

See `src/RaspiMcp.Example/` in this repository for a minimal end-to-end plugin:

- `ExamplePlugin.cs` — implements `IMcpPlugin`
- `Tools/ExampleTools.cs` — `[McpServerToolType]` with a single `hello` tool

It has no external dependencies beyond `RaspiMcp.Core` and `ModelContextProtocol`, making it the ideal copy-paste starting point.

### Real-World External Plugin Example

`plugins-src/RaspiMcp.Gpio/` in this repository is a real (non-hypothetical)
drop-in plugin — it's never referenced by `RaspiMcp.Server.csproj`, never
added to `PluginLoader.cs`'s built-in list, and never added to
`RaspiMcp.slnx`'s `/src/` folder, exactly like a real third-party plugin
would be built and shipped separately. It exposes `set_rgb_led` (a
convenience tool for a 3-pin digital RGB LED) plus generic
`set_gpio_pin`/`get_gpio_pin` tools, all driving BCM GPIO pins over SSH via
`pinctrl`. It demonstrates constructor-injecting `ICommandExecutor` without
ever referencing `RaspiMcp.Ssh`, and a local `HandleError` helper matching
the same structured-error-JSON shape used by the built-in SSH tools.
