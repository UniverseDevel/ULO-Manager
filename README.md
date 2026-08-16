# ULO Controller

Tooling for the **ULO camera** by Mu Design — a documented client for its undocumented HTTP API,
plus the research behind it.

The camera was never opened up as promised, so everything here was worked out from the outside: the
API by watching the camera's own web application and sweeping the device, and the firmware by static
analysis of the images it ships.

## Documentation

| Document                                 | Contents                                                                                                                             |
|------------------------------------------|--------------------------------------------------------------------------------------------------------------------------------------|
| [Security assessment](docs/SECURITY.md)  | **Read this before putting ULO on your network.** What the device runs, device and firmware findings, chained risks, recommendations |
| [API reference](docs/API.md)             | Every known endpoint, authentication, both WebSocket protocols, firmware version differences, device quirks                          |
| [Application guide](docs/APPLICATION.md) | ULO Manager: the command line tool, the Windows application and the library                                                          |
| [Use cases](docs/USE_CASES.md)           | Scheduled sync to a share or FTP, presence-based mode switching, snapshots, live video, housekeeping                                 |
| [Build guide](docs/BUILDING.md)          | Building on Windows and Linux, IDE setup, troubleshooting                                                                            |
| [Easter eggs](docs/EASTER_EGGS.md)       | Oddities found on the device: the Bollywood music video in the firmware, and certificate absurdities                                 |
| [Source analysis](docs/SOURCE_ANALYSIS.md)| What the vendor's own source code reveals: version format, undocumented endpoints, leaked tokens, demo mode, voice commands          |
| [Legal notes](docs/LEGAL.md)             | What this repository deliberately does not redistribute, and why - copyright, personal data, security research                      |

## What is here

**`UloManager/`** — the tooling, .NET 9, no third-party dependencies.

* `ulo` — command line tool, Windows and Linux
* `UloManager.exe` — Windows dashboard with live video, activity feed and the full setup surface
* `UloManager.Core` — the client library

**`firmware/`** - the analysis of each firmware version: what the unit serves, the device
certificates, and the findings behind the [security assessment](docs/SECURITY.md). The vendor's own
binaries and reconstructed source are **not** redistributed - see [legal notes](docs/LEGAL.md) for
what is excluded and how to obtain it from your own device.

## Quick start

```powershell
.\UloManager\build.ps1

$env:ULO_HOST     = '192.168.0.10'
$env:ULO_USER     = 'user@example.com'
$env:ULO_PASSWORD = '…'

ulo status
ulo watch
ulo download --out D:\ulo\media --type video --age 24
```

On Linux, publish the CLI with `dotnet publish -r linux-x64` — see
[build guide §4](docs/BUILDING.md#4-linux).

## Background

The goal was a tool that could pull video off the camera onto network storage and set its mode, as
an open source project for anyone to use. Once it turned out the files were reachable from a
browser, it followed that they were reachable programmatically too, and the camera's own web
interface revealed the API calls it made in the background.

What that revealed:

* The camera uses OAuth-style authentication — user name and password are exchanged for a token that
  authorises every later call until logout or timeout.
* The API is REST-ish and speaks JSON.
* The media files are also reachable through a plain directory index — on firmware `06.0601`
  with no session and no credentials at all, which is why the
  [security assessment](docs/SECURITY.md) opens the way it does.

The resulting library grew into a full solution for downloading, storing and maintaining the
camera's media, with upload to a network share or FTP as well as the local filesystem, and retention
to expire old files.

One problem drove much of it: **the camera turns alert mode off by itself.** Support confirmed this
happens when it reboots but had no other reports, said they would look into it, and the fix never
came — it still happens. The workaround was to use the API to set the correct mode based on whether
a phone was reachable on the network, driven by the Windows scheduler. That workflow is
[still supported](docs/USE_CASES.md#5-keep-the-camera-in-the-right-mode).

## Disclaimer

This is not a professional product and there is no warranty that it will work. Use it entirely at
your own risk. This project is in no way affiliated with Mu Design Sàrl.

It is also not aimed at beginners — expect to need some knowledge of C#, APIs, networking and the
command line.

ULO and Mu Design are the marks of their owner and are used here only to identify the product this
tooling works with. No vendor code, firmware, application or documentation is redistributed - see
[legal notes](docs/LEGAL.md). Security findings come from hardware the author owns, tested on the
author's own network; do not apply them to a device you do not own.
All user names, passwords and IP addresses in this repository are examples, and should not be
considered default or safe.
