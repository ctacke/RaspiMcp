# RaspiMcp

A production-quality .NET 10 MCP (Model Context Protocol) server for controlling Raspberry Pi devices over SSH. Connect Claude Code or any MCP-compatible AI assistant directly to your Pi fleet.

## What is RaspiMcp?

RaspiMcp exposes SSH operations as MCP tools, allowing an AI assistant to:
- Execute shell commands on a remote Raspberry Pi
- Read files and inspect directory contents
- Tail log files and query systemd services
- Switch between multiple configured Pi targets at runtime

All commands pass through a safety validator that blocks destructive operations (`rm -rf`, `mkfs`, `shutdown`, etc.) and every execution is audit-logged.

## Capabilities

| Capability | What it does | Safety notes |
|---|---|---|
| Shell command execution | Runs any shell command on the active Pi and returns stdout/stderr/exit code | Denylist-based, **not** a read-only sandbox — only a handful of destructive patterns (`rm -rf`, `mkfs`, `dd`, `shutdown`, `reboot`, `poweroff`, `halt`, `init 0`, `systemctl poweroff/reboot/halt`) are blocked. Anything else, including writes and deletes, is allowed. |
| Filesystem inspection | Read files, list directories, stat paths, check existence | Read-only helpers built on `cat` / `ls -lah` / `stat` / `test` |
| Log & service inspection | Tail log files, check systemd service status, query journal entries | Read-only helpers built on `tail` / `systemctl status` / `journalctl` |
| Host management | List configured Pi aliases, view the active host, switch targets at runtime | Switching hosts requires explicit user approval |
| Plugin extensibility | Load additional tool sets from DLLs dropped into `plugins/` | Plugins run fully trusted — no sandboxing between plugin code and the host process |
| Audit logging | Every executed command is recorded with timestamp, host, exit code, and duration | Structured `[AUDIT]` entries via standard `ILogger` — route to a file/Seq/etc. as needed |

See [Available MCP Tools](#available-mcp-tools) below for the exact tool-by-tool breakdown.

## Prerequisites

- SSH access to one or more Raspberry Pi devices
- An SSH key pair (recommended) or password credentials
- [.NET 10 runtime/SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — **only** if installing via `dotnet tool install` or building from source. The self-contained release binary needs nothing but the Pi's SSH access.

## Installation

### Recommended: install as a global tool

```bash
dotnet tool install -g RaspiMcp.Server
```

Published to [nuget.org](https://www.nuget.org/packages/RaspiMcp.Server),
so no source configuration or authentication is needed — it's the .NET
SDK's default package source. This installs a `raspi-mcp` command on your
`PATH`. Configure your Pi's connection details directly in your Claude Code
`mcp.json` entry — see [Claude Code Integration](#claude-code-integration)
below. Upgrade later with `dotnet tool update -g RaspiMcp.Server`.

### Alternative: download a release binary

No .NET install required at all. Grab the archive for your OS from the
[Releases page](../../releases) and extract it. See
[docs/deployment.md](docs/deployment.md) for details.

## Running from Source

For contributing to RaspiMcp itself:

1. **Clone and build**
   ```
   git clone https://github.com/ctacke/RaspiMcp
   cd RaspiMcp
   dotnet build RaspiMcp.slnx
   ```

2. **Run the server**
   ```
   dotnet run --project src/RaspiMcp.Server
   ```
   Pi connection details come from your `mcp.json` entry's `env` block (see
   below) the same as any other install method.

## Claude Code Integration

Register RaspiMcp with the `claude mcp add` CLI — this is the supported way
to add a server at user scope (available across all your projects); there
isn't a user-level config file meant to be hand-edited. Environment
variables carry the Pi's connection details straight through: `.NET`'s
config pipeline layers environment variables over `appsettings.json`
automatically, using `__` (double underscore) to express nesting, so there's
no separate config file to manage.

If installed as a global tool:

```
claude mcp add --scope user raspi-mcp --env Ssh__CurrentHost=orleans2 --env Ssh__Hosts__orleans2__Host=orleans2.local --env Ssh__Hosts__orleans2__Username=pi --env Ssh__Hosts__orleans2__Password=secret -- raspi-mcp
```

If using the downloaded release binary:

```
claude mcp add --scope user raspi-mcp --env Ssh__CurrentHost=orleans2 --env Ssh__Hosts__orleans2__Host=orleans2.local --env Ssh__Hosts__orleans2__Username=pi --env Ssh__Hosts__orleans2__Password=secret -- F:/tools/raspi-mcp/RaspiMcp.Server.exe
```

If running from source:

```
claude mcp add --scope user raspi-mcp --env Ssh__CurrentHost=orleans2 --env Ssh__Hosts__orleans2__Host=orleans2.local --env Ssh__Hosts__orleans2__Username=pi --env Ssh__Hosts__orleans2__Password=secret -- dotnet run --project F:/repos/ctacke/RaspiMcp/src/RaspiMcp.Server --no-build
```

**After running `claude mcp add`, restart your `claude` session** (exit and
relaunch) — a server added while a session is already running won't appear
in `/mcp` until you do.

Use a private key instead of a password by setting
`--env Ssh__Hosts__<alias>__PrivateKey=<path>` instead of `...__Password`.
Want it shared with your team via git instead of just your machine? Use
`--scope project` instead of `--scope user` — that writes to `.mcp.json` in
the repo root. Prefer multiple predefined Pi hosts for `switch_host` without
a wall of `--env` flags? See the `appsettings.json` alternative in
[docs/deployment.md](docs/deployment.md#installing-a-release).

## Using RaspiMcp from Non-.NET Clients

RaspiMcp is a standard MCP server speaking JSON-RPC over stdio — any
MCP-compatible client can talk to it, regardless of what language the client
is written in. You don't need the .NET SDK to *use* the server: download the
self-contained binary from the [Releases page](../../releases) (see
[docs/deployment.md](docs/deployment.md)) and point your client at the
executable. The .NET SDK is only required if you're building from source.

### Example: Python client

Install the official MCP Python SDK:

```bash
pip install mcp
```

Then launch the RaspiMcp binary as a subprocess and call its tools:

```python
import asyncio
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client

async def main():
    server_params = StdioServerParameters(
        command="./RaspiMcp.Server",  # or RaspiMcp.Server.exe on Windows
        args=[],
    )
    async with stdio_client(server_params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()

            tools = await session.list_tools()
            print([t.name for t in tools.tools])

            result = await session.call_tool("execute", {"command": "uptime"})
            print(result.content)

asyncio.run(main())
```

This works the same way from any language with an MCP client library (or a
hand-rolled JSON-RPC-over-stdio client) — Claude Code is just one possible
frontend.

## Available MCP Tools

| Tool | Description |
|------|-------------|
| `get_current_host` | Returns the active SSH target (alias, host, username) |
| `list_hosts` | Lists all configured host aliases |
| `switch_host` | Switches the active SSH target — requires user approval |
| `execute` | Runs a shell command on the current host |
| `read_file` | Returns the contents of a remote file |
| `list_directory` | Lists a directory with `ls -lah` output |
| `stat` | Returns `stat` output for a remote path |
| `file_exists` | Returns true/false for a remote path |
| `tail_log` | Returns the last N lines of a log file |
| `systemctl_status` | Returns systemd service status |
| `journal` | Returns journalctl entries for a service |
| `hello` | Example plugin greeting tool (verify plugin system works) |

## Deployment

To build and release RaspiMcp as a downloadable binary or NuGet tool, see
[docs/deployment.md](docs/deployment.md). Pushing a `vX.X` or `vX.X.X` tag (e.g. `v1.0`, `v0.9.1`)
triggers an automated GitHub Actions release.

## Plugin Development

RaspiMcp is extensible. See [docs/plugin-development.md](docs/plugin-development.md) for a complete guide on writing and deploying custom plugins.

The short version:
1. Create a .NET class library referencing `RaspiMcp.Core`
2. Implement `IMcpPlugin`
3. Create a `[McpServerToolType]` class with `[McpServerTool]`-attributed methods
4. Drop the compiled DLL into the `plugins/` folder next to the server binary

## Security Notes

- **Never commit `appsettings.json`** — it contains credentials. It is gitignored by default.
- **If you register RaspiMcp with `--scope project` (writes to `.mcp.json` in the repo root), don't commit that file** — prefer `--scope user` so Pi credentials passed via `--env` never end up in git history.
- The command validator blocks: `rm -rf`, `mkfs`, `dd`, `shutdown`, `reboot`, `poweroff`, `halt`, `init 0`, and `systemctl poweroff/reboot/halt`.
- All executed commands are audit-logged via the structured `[AUDIT]` log entries.
- SSH private key paths are read from the local filesystem at connection time; keys never leave the machine.
- Use SSH key authentication rather than passwords where possible.

## Running Tests

```
dotnet test tests/RaspiMcp.Tests/RaspiMcp.Tests.csproj
```
