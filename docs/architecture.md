# Architecture

## Solution Overview

```
RaspiMcp.sln
├── src/
│   ├── RaspiMcp.Core        — Interfaces, models, and configuration. No external deps.
│   ├── RaspiMcp.Ssh         — SSH plugin: connection, validation, audit, MCP tools.
│   ├── RaspiMcp.Example     — Minimal reference plugin for third-party authors.
│   └── RaspiMcp.Server      — Host process: loads plugins, starts MCP stdio server.
├── tests/
│   └── RaspiMcp.Tests       — xUnit tests for Core, Ssh, and Server.
├── plugins/                 — Runtime drop zone for external plugin DLLs.
└── plugins-src/              — Source for real external plugins (not built-in, not referenced by Server).
    └── RaspiMcp.Gpio        — BCM GPIO / RGB LED control via pinctrl over SSH.
```

### Project Responsibilities

| Project | Responsibility |
|---------|---------------|
| `RaspiMcp.Core` | Contracts only — interfaces, record types, and option classes. No infrastructure. |
| `RaspiMcp.Ssh` | Implements Core interfaces using SSH.NET. Owns connection lifecycle. |
| `RaspiMcp.Example` | Copy-paste starting point for plugin authors. |
| `RaspiMcp.Server` | Host process. Loads plugins, wires DI, runs the MCP stdio loop. |
| `RaspiMcp.Gpio` *(plugins-src/, not built-in)* | Real drop-in plugin example — GPIO/RGB LED tools built on `ICommandExecutor`, no `RaspiMcp.Ssh` reference. |

---

## Plugin Lifecycle

### Discovery

`PluginLoader.LoadPlugins(baseDirectory)` runs at startup before `Host.Build()`:

1. Scans the built-in assemblies (`RaspiMcp.Ssh`, `RaspiMcp.Example`).
2. Scans `{baseDirectory}/plugins/*.dll` for external plugins.
3. For each assembly, reflects over public non-abstract classes that implement `IMcpPlugin`.

### Registration Phase

For each `IMcpPlugin` found:

```
plugin.Register(IServiceCollection, IConfiguration)
    → plugin adds its own services to the DI container
    → services.Configure<MyOptions>(config.GetSection("MySection"))

plugin.RegisterTools(IMcpToolRegistry)
    → calls registry.Register<MyToolClass>()
    → which calls builder.WithTools<MyToolClass>() on the MCP SDK builder
```

### MCP SDK Tool Discovery

`builder.WithTools<TToolType>()` (from `ModelContextProtocol` 1.4.x):
- Reflects over all public and non-public instance/static methods on `TToolType`
- Finds methods decorated with `[McpServerTool]`
- Registers one `McpServerTool` per method
- Constructs the tool class via DI on each invocation (constructor injection supported)

---

## SSH Service Auto-Reconnect Flow

```
CommandExecutor.ExecuteAsync(command)
  └─ validator.Validate(command)          ← rejected? throw immediately
  └─ sshService.ExecuteAsync(command)
       └─ EnsureConnectedAsync()          ← connect if not already connected
       └─ ExecuteWithRetryAsync()
            └─ sshCommand.ExecuteAsync()
                 ┌─ SshConnectionException? (first attempt)
                 │    └─ dispose client, reconnect, retry once
                 └─ SshConnectionException? (retry)
                      └─ throw InvalidOperationException
```

Connection is guarded by a `SemaphoreSlim(1,1)` to prevent concurrent connect/disconnect races.

---

## Command Validation Pipeline

```
ICommandExecutor.ExecuteAsync(command)
  └─ ICommandValidator.Validate(command)
       └─ foreach pattern in BlockedPatterns:
            if Regex.IsMatch(command, pattern, IgnoreCase) → return ValidationResult(false, reason)
       └─ return ValidationResult(true)
  └─ if !valid → throw InvalidOperationException(reason)
  └─ ISshService.ExecuteAsync(command)
```

Default blocked patterns: `rm -rf`, `mkfs`, `dd`, `shutdown`, `reboot`, `poweroff`, `halt`, `init 0`, `systemctl poweroff/reboot/halt`.

Extend by subclassing `CommandValidator` and overriding `BlockedPatterns`.

---

## Audit Logging

Every successfully submitted command (post-validation) is recorded:

```
[AUDIT] Timestamp=2024-01-15T10:30:00.000Z Host=raspi-dev ExitCode=0 Duration=123.4ms Command=ls -la /home/pi
```

Log entries use structured logging (`ILogger`) at `Information` level. Redirect to a file, Seq, or any ILogger sink via standard `Microsoft.Extensions.Logging` configuration.

---

## Configuration Structure

```json
{
  "Ssh": {
    "CurrentHost": "my-pi",
    "CommandTimeoutSeconds": 30,
    "MaxReconnectAttempts": 3,
    "Hosts": {
      "my-pi": {
        "Host": "192.168.1.42",
        "Username": "pi",
        "PrivateKey": "/home/user/.ssh/id_ed25519"
      },
      "vehicle-pi": {
        "Host": "192.168.1.43",
        "Username": "pi",
        "Password": "secret"
      }
    }
  }
}
```

`SshPluginOptions` is bound from `IOptionsMonitor<SshPluginOptions>` so changes to `appsettings.json` are picked up at runtime without restart (host list changes take effect on the next `switch_host` call).
