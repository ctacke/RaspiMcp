# Publishing RaspiMcp

This guide covers how to build, package, and distribute RaspiMcp via
[NuGet.org](https://www.nuget.org) so others can install it as a .NET global
tool with zero authentication required to read/install (only publishing
needs an API key).

> Earlier drafts of this guide targeted GitHub Packages instead. That
> registry requires a PAT to *install* a package even when it's public — a
> real limitation of `nuget.pkg.github.com`, not something specific to this
> project. NuGet.org doesn't have that restriction, so it's the better fit
> for a package meant to be freely installable.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A [nuget.org](https://www.nuget.org) account
- A NuGet API key (nuget.org → your account → API Keys → create one scoped
  to push new packages and new versions of `RaspiMcp.Server`)
- `gh` CLI (optional — useful for creating GitHub Releases)

---

## Step 1: .NET global tool packaging properties

`src/RaspiMcp.Server/RaspiMcp.Server.csproj` already has the properties
needed to pack as a tool:

```xml
<PackAsTool>true</PackAsTool>
<ToolCommandName>raspi-mcp</ToolCommandName>
<PackageId>RaspiMcp.Server</PackageId>
<Version>1.0.0</Version>
<Authors>Chris Tacke</Authors>
<Description>MCP server providing SSH access to a Raspberry Pi for Claude Code.</Description>
<PackageLicenseExpression>MIT</PackageLicenseExpression>
<RepositoryUrl>https://github.com/ctacke/RaspiMcp</RepositoryUrl>
<PackageReadmeFile>README.md</PackageReadmeFile>
```

Before publishing for the first time, check that `RaspiMcp.Server` isn't
already taken on nuget.org — package IDs there are first-come, first-served
and can't be transferred after the fact.

No `nuget.config` is needed to *publish* to nuget.org — it's the default
NuGet source already, so pushing only needs the `--api-key` on the command
line (or a repo secret in CI, see Step 7).

---

## Step 2: Build and test

Always run tests before publishing:

```bash
dotnet build RaspiMcp.slnx -c Release
dotnet test tests/RaspiMcp.Tests/RaspiMcp.Tests.csproj -c Release --no-build
```

---

## Step 4: Pack the NuGet tool

```bash
dotnet pack src/RaspiMcp.Server/RaspiMcp.Server.csproj -c Release -o ./artifacts
```

This produces `artifacts/RaspiMcp.Server.<version>.nupkg`.

---

## Step 5: Publish to NuGet.org

```bash
# Windows (PowerShell)
$env:NUGET_API_KEY = "YOUR_NUGET_ORG_API_KEY"

# macOS / Linux
export NUGET_API_KEY=YOUR_NUGET_ORG_API_KEY

dotnet nuget push artifacts/RaspiMcp.Server.*.nupkg --source https://api.nuget.org/v3/index.json --api-key $env:NUGET_API_KEY
```

The package appears at `https://www.nuget.org/packages/RaspiMcp.Server` a
few minutes after indexing completes.

---

## Step 6: Build self-contained binaries (optional)

For users who don't have .NET installed, publish self-contained single-file executables
and attach them to the GitHub Release:

```bash
dotnet publish src/RaspiMcp.Server -c Release -r win-x64   --self-contained -p:PublishSingleFile=true -o publish/win-x64
dotnet publish src/RaspiMcp.Server -c Release -r linux-x64  --self-contained -p:PublishSingleFile=true -o publish/linux-x64
dotnet publish src/RaspiMcp.Server -c Release -r osx-x64    --self-contained -p:PublishSingleFile=true -o publish/osx-x64
dotnet publish src/RaspiMcp.Server -c Release -r osx-arm64  --self-contained -p:PublishSingleFile=true -o publish/osx-arm64
```

Create a GitHub Release and attach the binaries:

```bash
gh release create v1.0 \
  publish/win-x64/RaspiMcp.Server.exe \
  publish/linux-x64/RaspiMcp.Server \
  publish/osx-x64/RaspiMcp.Server \
  publish/osx-arm64/RaspiMcp.Server \
  --title "v1.0" \
  --notes "Initial release"
```

---

## Step 7: Automate with GitHub Actions

All of the above is already automated in
[`.github/workflows/release.yml`](../.github/workflows/release.yml), which
runs on every `vX.X` tag push (e.g. `v1.0`, `v2.3` — no patch segment). It
validates the tag format, runs the test suite, publishes self-contained
binaries for `win-x64`/`linux-x64`/`osx-x64`/`osx-arm64` and attaches them
to a GitHub Release, then packs and pushes the NuGet tool package to
NuGet.org.

That workflow authenticates to NuGet.org using
[trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
(OIDC) rather than a stored API key — no repository secret needed. This
requires a one-time setup: a Trusted Publisher policy on nuget.org for the
`RaspiMcp.Server` package pointing at `ctacke/RaspiMcp`, workflow
`release.yml`, environment `production`; and a matching GitHub Environment
named `production` created under **Settings → Environments**. See
[docs/deployment.md](deployment.md#one-time-setup-nugetorg-trusted-publishing)
for the exact steps.

See [docs/deployment.md](deployment.md) for the full walkthrough, including
manual steps for testing the pipeline locally.

---

## Versioning

Tags use the two-part `vX.X` form (`v1.0`, `v2.3`, `v2.10`) — no patch
segment. To release a new version:

```bash
git tag v1.1
git push origin v1.1
```

The GitHub Actions workflow triggers automatically on the tag push and
derives the package/binary version from the tag itself — there's no need to
manually bump `<Version>` in the csproj first.

---

## Installing as a user

### Via dotnet tool (recommended)

```bash
dotnet tool install -g RaspiMcp.Server
```

No source configuration or authentication needed — NuGet.org is the default
package source for the .NET SDK.

### Via self-contained binary

Download the appropriate binary from the [Releases page](https://github.com/OWNER/raspi-mcp/releases),
extract it, and place it somewhere on your `PATH`.

---

## Configure Claude Code

After installation, add RaspiMcp to your Claude Code MCP configuration
(`~/.claude/mcp.json` or project `.mcp.json`), passing the Pi's connection
details as environment variables in the same entry — this keeps RaspiMcp
configured the same way as any other MCP server, with no separate config
file to manage:

**If installed as a dotnet tool:**

```json
{
  "mcpServers": {
    "raspi-mcp": {
      "command": "raspi-mcp",
      "env": {
        "Ssh__CurrentHost": "orleans2",
        "Ssh__Hosts__orleans2__Host": "orleans2.local",
        "Ssh__Hosts__orleans2__Username": "pi",
        "Ssh__Hosts__orleans2__Password": "secret"
      }
    }
  }
}
```

**If using the self-contained binary:**

```json
{
  "mcpServers": {
    "raspi-mcp": {
      "command": "C:/tools/RaspiMcp.Server.exe",
      "env": {
        "Ssh__CurrentHost": "orleans2",
        "Ssh__Hosts__orleans2__Host": "orleans2.local",
        "Ssh__Hosts__orleans2__Username": "pi",
        "Ssh__Hosts__orleans2__Password": "secret"
      }
    }
  }
}
```

**If running from source:**

```json
{
  "mcpServers": {
    "raspi-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "src/RaspiMcp.Server/RaspiMcp.Server.csproj", "-c", "Release"],
      "cwd": "C:/path/to/raspi-mcp",
      "env": {
        "Ssh__CurrentHost": "orleans2",
        "Ssh__Hosts__orleans2__Host": "orleans2.local",
        "Ssh__Hosts__orleans2__Username": "pi",
        "Ssh__Hosts__orleans2__Password": "secret"
      }
    }
  }
}
```

Use a private key instead of a password by setting `Ssh__Hosts__<alias>__PrivateKey`
(a filesystem path) instead of `...__Password`.

---

## Configuration via environment variables (recommended)

`RaspiMcp.Server` uses the standard .NET Generic Host configuration
pipeline, which layers environment variables on top of `appsettings.json`
automatically — no code or file needed. Because of this, the connection
details for a single Pi host can live directly in the `env` block of the
MCP server's `mcp.json` entry (see examples above), using `__` (double
underscore) to express nesting: `Ssh__Hosts__<alias>__Host`, `...__Username`,
`...__Password` / `...__PrivateKey`.

This is the recommended approach for a single Pi target — it avoids relying
on the process's working directory (which an MCP client controls, not you)
to locate a config file, and keeps all of RaspiMcp's settings in the same
place you already configure every other MCP server.

### Alternative: appsettings.json file

If you want multiple Pi hosts predefined for `switch_host` without a wall of
`env` entries, you can still use a file instead: copy
`appsettings.json.example` to `appsettings.json`, place it in whatever
directory the server process will have as its working directory, and set
`"cwd"` explicitly in the `mcp.json` entry to that directory (don't rely on
the default working directory — it isn't guaranteed to be predictable).
Environment variables set in `mcp.json` still take precedence over the file.
