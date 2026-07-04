# Deployment

This guide covers building and releasing RaspiMcp manually. Two distribution
paths are supported, and a release publishes both:

- **GitHub Release binary** — a self-contained executable attached to the
  repo's Releases page. No .NET SDK or authentication needed to download and
  run it.
- **NuGet.org package (`dotnet tool`)** — a NuGet package published to
  [nuget.org](https://www.nuget.org), installed with `dotnet tool install -g`.
  Requires the .NET SDK, but no authentication to install — nuget.org is the
  default package source and doesn't gate reads of public packages the way
  GitHub Packages does. Gives easy upgrades via `dotnet tool update -g`.

Both are built and published automatically by
[`.github/workflows/release.yml`](../.github/workflows/release.yml) whenever
a tag matching `vX.X` or `vX.X.X` (e.g. `v1.0`, `v0.9.1`, `v2.3`) is pushed. The steps below are for
a manual/local release, or for debugging that workflow.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- `gh` CLI (optional — used below to create the Release from the command line)
- A [nuget.org](https://www.nuget.org) account and API key, only if
  publishing the NuGet package manually

---

## Step 1: Build and test

```bash
dotnet restore RaspiMcp.slnx
dotnet build RaspiMcp.slnx -c Release --no-restore
dotnet test tests/RaspiMcp.Tests/RaspiMcp.Tests.csproj -c Release --no-build
```

---

## Step 2: Release binary path

### Publish self-contained binaries

RaspiMcp.Server runs on the machine that SSHes into the Pi, not on the Pi
itself, so binaries are built for common desktop/dev-machine platforms:

```bash
VERSION=1.0
for rid in win-x64 linux-x64 osx-x64 osx-arm64; do
  dotnet publish src/RaspiMcp.Server \
    -c Release \
    -r $rid \
    --self-contained \
    -p:PublishSingleFile=true \
    -p:Version=$VERSION \
    -o publish/$rid
  cp appsettings.json.example publish/$rid/
done
```

Each `publish/<rid>` folder contains a single native executable
(`RaspiMcp.Server.exe` on Windows, `RaspiMcp.Server` elsewhere) with the
built-in `RaspiMcp.Ssh` and `RaspiMcp.Example` plugins already embedded,
plus a copy of `appsettings.json.example` to fill in.

### Archive and attach to a Release

```bash
mkdir -p artifacts
for rid in win-x64 linux-x64 osx-x64 osx-arm64; do
  (cd publish/$rid && zip -r "../../artifacts/RaspiMcp.Server-$VERSION-$rid.zip" .)
done

git tag v$VERSION
git push origin v$VERSION

gh release create v$VERSION \
  artifacts/RaspiMcp.Server-$VERSION-*.zip \
  --title "v$VERSION" \
  --generate-notes
```

> **Tagging convention:** tags use `vX.X` or `vX.X.X` (`v1.0`, `v0.9.1`,
> `v2.3`, `v2.3.10`) — the patch segment is optional. This is what the
> automated release workflow watches for.

---

## Step 3: NuGet.org package path

No `nuget.config` needed — nuget.org is the default NuGet source, so
publishing is just a pack + push with an API key:

```bash
dotnet pack src/RaspiMcp.Server/RaspiMcp.Server.csproj \
  -c Release \
  -p:Version=$VERSION \
  -o artifacts

# Windows (PowerShell): $env:NUGET_API_KEY = "YOUR_NUGET_ORG_API_KEY"
# macOS / Linux:        export NUGET_API_KEY=YOUR_NUGET_ORG_API_KEY
dotnet nuget push artifacts/RaspiMcp.Server.$VERSION.nupkg --source https://api.nuget.org/v3/index.json --api-key $NUGET_API_KEY
```

The package appears at `https://www.nuget.org/packages/RaspiMcp.Server` a
few minutes after indexing completes. Check that the `RaspiMcp.Server`
package ID isn't already taken before your first push — IDs on nuget.org
are first-come, first-served.

---

## Automated releases (recommended)

```bash
git tag v1.1
git push origin v1.1
```

pushing a `vX.X` or `vX.X.X` tag runs [`.github/workflows/release.yml`](../.github/workflows/release.yml), which:

1. Validates the tag matches `vX.X` or `vX.X.X` (rejects `v1`, `v1.0-beta`, etc. before any build runs)
2. Builds and runs the full test suite
3. Publishes self-contained binaries for `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`, zips each, and attaches them to a new GitHub Release
4. Packs the NuGet tool package and pushes it to NuGet.org

No manual steps are required beyond bumping the tag — as long as trusted
publishing is set up (see below). No API key or repository secret is
needed; the workflow authenticates to NuGet.org via GitHub's OIDC token.

### One-time setup: NuGet.org trusted publishing

1. On nuget.org, add a Trusted Publisher policy for the `RaspiMcp.Server`
   package (or reserve the ID first if it doesn't exist yet) with:
   - **Repository owner:** `ctacke`
   - **Repository:** `RaspiMcp`
   - **Workflow:** `release.yml`
   - **Environment:** `production`
2. In the GitHub repo, go to **Settings → Environments → New environment**
   and create one named exactly `production` — it must match what you
   entered on nuget.org. No protection rules are required, but you can add
   required reviewers there if you want a manual approval gate before a
   release publishes.

The workflow's `NuGet/login@v1` step exchanges a short-lived GitHub OIDC
token for a temporary NuGet API key (valid ~1 hour) at push time, so there's
no long-lived secret to rotate or leak.

---

## Installing a release

Both paths below register RaspiMcp with the `claude mcp add` CLI, passing
the Pi connection details as environment variables — this is the supported
way to add a server at user scope (available across all projects); there's
no user-level config file meant to be hand-edited. `.NET`'s config pipeline
layers environment variables over `appsettings.json` automatically, using
`__` (double underscore) to express nesting: `Ssh__Hosts__<alias>__Host`,
`...__Username`, and `...__Password` / `...__PrivateKey`.

After running `claude mcp add`, restart your `claude` session (exit and
relaunch) — a server added while a session is already running won't appear
in `/mcp` until you do.

### Release binary

1. Download the archive for your OS from the
   [Releases page](../../../releases) and extract it.
2. Register it, with the Pi's details as `--env` flags:

   ```
   claude mcp add --scope user raspi-mcp --env Ssh__CurrentHost=orleans2 --env Ssh__Hosts__orleans2__Host=orleans2.local --env Ssh__Hosts__orleans2__Username=pi --env Ssh__Hosts__orleans2__Password=secret -- C:/tools/raspi-mcp/RaspiMcp.Server.exe
   ```

   (Use the platform-appropriate path and drop the `.exe` extension on
   Linux/macOS.)

### NuGet.org package (dotnet tool)

```bash
dotnet tool install -g RaspiMcp.Server
```

No source configuration or authentication needed — nuget.org is the default
package source for the .NET SDK.

```
claude mcp add --scope user raspi-mcp --env Ssh__CurrentHost=orleans2 --env Ssh__Hosts__orleans2__Host=orleans2.local --env Ssh__Hosts__orleans2__Username=pi --env Ssh__Hosts__orleans2__Password=secret -- raspi-mcp
```

If you'd rather predefine multiple Pi hosts for `switch_host` without a wall
of `--env` flags, copy `appsettings.json.example` to `appsettings.json`
instead, place it in a known folder, and pass `--cwd <that folder>` to
`claude mcp add`. `env` values still take precedence over the file when both
are present.

Either way, custom third-party plugins are added by dropping compiled DLLs
into a `plugins/` folder next to the server binary — see
[docs/plugin-development.md](plugin-development.md). First-party plugins
under this repo's own `plugins-src/` (e.g. `RaspiMcp.Gpio`) already ship
there automatically, both in the NuGet tool package and the release
binaries — no manual step needed.
