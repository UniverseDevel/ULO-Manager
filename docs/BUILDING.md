# Build Guide

## 1. Requirements

* **.NET 9 SDK** — <https://dotnet.microsoft.com/download/dotnet/9.0>
* Windows for `UloManager.Gui` (targets `net9.0-windows`); the library and the CLI are portable
* Optional: VLC or ffplay, only to display live video

`global.json` in the repository root pins the build to a 9.0 SDK.

## 2. Quick build

```powershell
.\build.ps1                      # Release
.\build.ps1 -Configuration Debug -Run
```

The script finds a usable SDK by itself, which matters when several are installed. The plain SDK
command works too:

```powershell
dotnet build UloManager.sln -c Release
```

Outputs:

```
src\UloManager.Gui\bin\Release\net9.0-windows\UloManager.exe
src\UloManager.Cli\bin\Release\net9.0\ulo.exe
```

## 3. Solution

One solution, `UloManager.sln`, at the repository root. It holds the three projects —
`UloManager.Core`, `UloManager.Cli` and `UloManager.Gui` — plus the `docs` folder, so the
documentation is editable alongside the code. Open it in an IDE, provided that IDE uses an MSBuild
new enough for .NET 9 — see §5.

## 4. Linux

The library and CLI target `net9.0` and run on Linux; only the GUI is Windows-only.

```bash
dotnet publish src/UloManager.Cli/UloManager.Cli.csproj \
       -c Release -r linux-x64 --self-contained false -o out/ulo
./out/ulo/ulo status --host 192.168.0.10 --user user@example.com
```

Use `--self-contained true` to produce a build that does not need the .NET runtime installed, or
`-r linux-arm64` for a Raspberry Pi or similar.

Two platform notes:

* **Share credentials.** `--dest-user` / `--dest-password` for a `\\server\share` destination are
  Windows-only. On Linux, mount the share (`mount -t cifs`, `mount -t nfs`, autofs, systemd
  `.mount`) and pass the mount point as `--out`. FTP destinations work everywhere.
* **Live video playback** looks for `vlc` or `ffplay` in the usual locations and on `PATH`.
  Recording to a file (`--out`) needs no player at all.

## 5. IDE setup

SDK-style projects need an MSBuild that both carries the .NET SDK resolver **and** is new enough for
the installed SDK: **.NET 9.0.3xx requires MSBuild 17.12 or newer**. Several MSBuild copies usually
exist side by side and not all of them qualify. Ask the build script:

```powershell
.\build.ps1 -Diagnose
```

It lists every MSBuild it can find, marks the usable one, and prints the exact paths to configure.
A typical machine looks like this:

| MSBuild | Location                                                | Verdict                                  |
|---------|---------------------------------------------------------|------------------------------------------|
| 17.3.1  | `%LOCALAPPDATA%\JetBrains\BuildTools\…`                 | too old for .NET 9                       |
| 17.14   | Visual Studio 2022 without the .NET workload            | no `Microsoft.DotNet.MSBuildSdkResolver` |
| 17.12.6 | `<Rider install>\tools\MSBuild\Current\Bin\MSBuild.exe` | works                                    |

### Rider

Settings → Build, Execution, Deployment → **Toolset and Build**:

* *Use MSBuild version* — the copy inside the Rider installation, e.g.
  `<Rider install>\tools\MSBuild\Current\Bin\MSBuild.exe`
* *.NET CLI executable path* — the `dotnet` that owns the SDKs, e.g. `%USERPROFILE%\.dotnet\dotnet.exe`
* then File → Reload All Projects

Rider stores this **per solution** in `<solution>.sln.DotSettings.user`, so set it for each solution
you open.

### Visual Studio

Open the Visual Studio Installer, *Modify*, and add the **.NET desktop development** workload.
Without it, VS ships no .NET SDK resolver and cannot build SDK-style projects at all, wherever the
SDK is installed.

### Symptoms and causes

| Message                                                                         | Cause                                                      |
|---------------------------------------------------------------------------------|------------------------------------------------------------|
| `Version 9.0.xxx of the .NET SDK requires at least version 17.12.0 of MSBuild`  | MSBuild too old                                            |
| `The SDK 'Microsoft.NET.Sdk' specified could not be found`                      | that MSBuild then gives up on the SDK                      |
| `The SDK 'Microsoft.NET.SDK.WorkloadAutoImportPropsLocator' could not be found` | Visual Studio's MSBuild, which lacks the .NET SDK resolver |
| `The current .NET SDK does not support targeting .NET 9.0`                      | an older SDK was selected                                  |
| Thousands of unresolved types while `dotnet build` reports 0 errors             | the IDE could not load the SDK at all                      |

None of these indicate a problem with the code — if `dotnet build` succeeds, the sources are fine.

## 6. Related documents

* [Application guide](APPLICATION.md) — what the built tool does, command by command
* [Use cases](USE_CASES.md) — recipes the finished build is used for
* [API reference](API.md) — the endpoints the library speaks
* [Legal notes](LEGAL.md) — what this repository does and does not redistribute
