# ULO Manager

A .NET 9 application for the ULO camera, in three parts:

| Project           | Output                | Purpose                                                                                      |
|-------------------|-----------------------|----------------------------------------------------------------------------------------------|
| `UloManager.Core` | `UloManager.Core.dll` | API client, models, log parser, media service, event stream, live video, availability checks |
| `UloManager.Cli`  | `ulo` / `ulo.exe`     | Scriptable command line tool — Windows and Linux                                             |
| `UloManager.Gui`  | `UloManager.exe`      | WinForms dashboard — Windows                                                                 |

No NuGet dependencies. The library uses `System.Text.Json` and `ClientWebSocket` only; an external
VLC or ffplay is optional and used solely to display live video.

See the [build guide](BUILDING.md) to compile, and [use cases](USE_CASES.md) for worked examples.

## 1. Command line

```
ulo <command> [arguments] --host <ip> --user <email> [--password <pass>]
```

Connection settings can also come from `ULO_HOST`, `ULO_USER` and `ULO_PASSWORD`. When the password
is omitted entirely the tool prompts for it without echoing.

### 1.1 Connection options

| Option              | Meaning                                                               |
|---------------------|-----------------------------------------------------------------------|
| `--host <ip\|url>`  | Camera address                                                        |
| `--user <email>`    | Account used to log in                                                |
| `--password <pass>` | Password; prompted when omitted                                       |
| `--https`           | Use https. The camera's certificate is self-signed, so it is accepted |
| `--pin-cert <sha1>` | Over https, accept only this certificate thumbprint                   |
| `--trace`           | Print every API call                                                  |
| `--quiet`           | Suppress the connection banner                                        |

The camera serves the API over both HTTP (80, 8080) and HTTPS (443, 8443). HTTP is the default
because it is what every other client uses. `--https` avoids sending the password and token in
clear — and in the Windows application the same switch is the **Use HTTPS** tick box under the
camera list, stored per camera.

**The certificate is never validated, on purpose.** There is nothing to validate against: on
`06.0601` it is self-signed `CN=localhost`, and on `10.1308` it is issued to `CN=*.ulo.camera` by
`Mu Design CA`, a private authority that is in no trust store. On top of that both certificates have
a fixed expiry — 2027-01-18 and 2028-07-07 — that nothing on the device will ever renew, so a client
that honoured expiry would simply stop working on a perfectly healthy camera. Chain, authority, host
name and expiry are therefore all ignored; unverified TLS is still a real gain over plain HTTP,
because it stops a passive observer on the network reading the password, the token and the video.

Add `--pin-cert` with the thumbprint you recorded to reject anything else — a thumbprint does not
expire, so pinning keeps working after the certificate does:

```
ulo status --https --pin-cert F9D58AB359661D967BBBC7285B7D080EC193EE60   # 10.1308
ulo status --https --pin-cert BE483A63136A7680116D8C60A5522D0B97038886   # 06.0601
```

The two firmware versions ship **different certificates and different keys**, so a pin is per
firmware version, not per product. Both were captured from live units and are archived with their
full details in [`firmware/10.1308/device/`](../firmware/10.1308/device/README.md) and
[`firmware/06.0601/device/`](../firmware/06.0601/device/README.md). Because the key appears to be
shared across units, pinning defeats passive capture but not a determined attacker who has extracted
it — see [security assessment §3.7](SECURITY.md#37-s7--credentials-and-tokens-travel-in-the-clear).

Everything runs over TLS when it is switched on, including the event channel and the live video
(`wss://`), all confirmed against a live `10.1308` unit. The TLS version is left to the operating
system's default so the strongest one both ends support is negotiated; nothing is forced down to a
legacy protocol.

### 1.2 Everyday commands

| Command                            | What it does                                                                        |
|------------------------------------|-------------------------------------------------------------------------------------|
| `status`                           | Everything about the camera on one screen                                           |
| `watch`                            | Live view of what the camera is doing (`--interval`, `--state-interval`, `--lines`) |
| `mode [standard\|spy\|alert]`      | Show or change the recording mode                                                   |
| `snapshot`                         | Take a picture now and download it (`--out`, `--store`)                             |
| `live`                             | Live video — `--play`, `--out <file.mp4>`, `--seconds`, `--player <path>`           |
| `record [start\|stop]`             | On-demand recording on the camera                                                   |
| `media [list\|days\|delete <day>]` | Browse recordings (`--type`, `--yes`)                                               |
| `download`                         | Download recordings — see below                                                     |
| `log [show\|tail\|save]`           | Read the camera log (`--lines`, `--interval`, `--out`)                              |
| `storage`                          | Internal memory and SD card usage                                                   |
| `movetocard`                       | Move recordings to the SD card (`--wait`)                                           |
| `time [sync]`                      | Show or synchronise the camera clock                                                |
| `availability`                     | Ping devices, optionally set the mode from the result                               |

`download` accepts three destination kinds:

```
--out D:\ulo\media                     local folder
--out \\nas\ulo\video                  network share  (--dest-user / --dest-password)
--out ftp://nas/ulo/video              FTP server     (--dest-user / --dest-password)
```

with `--type video|snapshot`, `--age <hours>` to limit what is fetched, `--retention <hours>` to
expire old files at the destination, and `--flat` to skip the per-day folders.

### 1.3 Administrator commands

Require an account whose type is `admin`.

| Command                                                                           | What it does                                                 |
|-----------------------------------------------------------------------------------|--------------------------------------------------------------|
| `config [show [section]\|set <section> <json>\|name <name>\|quality <q>]`         | Read and write configuration                                 |
| `wifi [show\|scan\|connect <ssid> <password>]`                                    | Wi-Fi settings                                               |
| `users [list\|show <id>\|add <email> --user-password <p> [--admin]\|delete <id>]` | Accounts                                                     |
| `clean <period>`                                                                  | Delete recordings from the camera                            |
| `backup [list\|create\|restore <name>]`                                           | Settings backups held on the camera                          |
| `firmware`                                                                        | Firmware and over-the-air status (`--download`, `--install`) |
| `reset`                                                                           | Factory reset, with confirmation                             |
| `api <path>`                                                                      | Call any endpoint directly (`--method`, `--body`)            |

### 1.4 Examples

```powershell
ulo status
ulo watch --interval 5
ulo mode alert
ulo download --out \\nas\ulo\video --type video --age 24 --retention 720
ulo availability --hosts 192.168.0.21,192.168.0.22 --if-up standard --if-down alert
ulo live --play
ulo api api/v1/state
```

## 2. Windows application

```powershell
UloManager.exe [--host <ip>] [--user <email>] [--password <pass>] [--connect] [--tab <name>] [--live]
```

`--tab` accepts `dashboard`, `live`, `activity`, `recordings`, `setup` or `api`; `--live` starts the
stream immediately. Without switches every stored camera is connected on start and the same
environment variables apply, so a desktop shortcut can go straight to a running live view.

### 2.1 The camera list

Every camera the application has credentials for is opened at start and kept in the list on the
left, each with its own session. A row shows the camera's name and device ID, the firmware it runs,
whether the session is an administrator or a standard user, whether the camera stands upright
(usage) or upside down (setup), and its recording mode. Hovering a row spells all of that out,
including the battery and the reason a camera is not connected.

The coloured dot is the whole state at a glance, and the same dot sits at the right end of the
status bar for the camera in use:

| Colour | Meaning                                                   |
|--------|-----------------------------------------------------------|
| Grey   | No session — unreachable, no credentials, or disconnected |
| Green  | Connected, camera upright in usage mode                   |
| Purple | Connected, camera upside down in admin/setup mode         |

Selecting a row makes that camera the **active** one. Only the active camera produces live video,
pictures and log output; the others are polled with the two cheapest calls the firmware has
(`/api/v1/state` and `/api/v1/mode`) so a wall of cameras costs almost nothing. Cameras that refuse
a connection are retried in the background, and a camera that drops off Wi-Fi or reboots reconnects
on its own.

**Adding a camera.** Credentials belong to the camera, not to the application — which suits a device
that allows only one session per account. The panel under the list always shows the account of the
camera selected in it:

* **Discover** scans the network and adds everything it finds → pick the new camera → type its user
  name and password → **Connect**.
* **Add…** is for a camera discovery cannot see: type its IP address or host name → **OK** → type
  the account → **Connect**.

**Forget** removes a camera and its stored credentials. Selecting a camera that is not connected
only shows its account and waits for **Connect**; it never dials out on its own.

### 2.2 Tabs

**Dashboard** — status table, mode buttons, an automatically refreshing picture of what the camera
sees, clock synchronisation, move-to-SD-card and log export. Preview pictures are requested with
`savePicture: 0`, so they never land in the camera's recordings.

**Live video** — plays inside the tab (VLC embedded through `--drawable-hwnd`) or in a separate
window, and can save an `.mp4` at the same time. The status line shows bytes streamed, throughput
and elapsed time. Still-picture refresh pauses automatically while the stream runs, because the
camera has a single video pipeline.

**Activity** — what the camera is doing, colour coded, merging its push events, its system log and
polled state. Following the newest entry pauses as soon as you scroll up and resumes at the bottom.
"Load full camera log" pulls the entire buffer, and the view can be exported.

**Recordings** — browse, download (single, selected or all) and delete a day on the camera.

**Setup / Admin** — general settings, Wi-Fi with scan, alert behaviour and exclusion zone, accounts,
settings backup/restore, firmware and over-the-air status, and factory reset. Enabled only for
administrator accounts.

**API console** — call any endpoint directly. All 75 confirmed method/path combinations sit in a
dropdown, and every one that takes a payload comes with a ready-made example, so a request is a
matter of picking it and editing a value.

Three things save you from writing JSON by hand:

* **Example payloads.** Selecting a known endpoint fills the body with a valid example — the field
  names and types the camera actually accepts, verified against the live device.
* **Recorded calls.** Everything the application sends is recorded, so after using the Setup tab
  you can pick the exact request it made and replay or tweak it. Secret-looking fields
  (`password`, `token`, `secret`, `psk`, …) are masked to `***` at any nesting depth before the
  payload is stored, so a recorded call can be shown or copied safely. Login is never recorded.
  If a recorded payload exists for the endpoint you select, it is offered in place of the example.
* **Body from current value.** Reads the path with `GET` and drops the result into the body box,
  turning any readable resource into an editable payload for the matching `PUT`.

Note that the camera never returns secrets on `GET`, so `config/wifi` and `config/email` examples
include a `password` field their `GET` responses do not.

## 3. Behaviour worth knowing

These are deliberate, and each exists because of something the camera does:

* **Sessions are re-established automatically.** The camera keeps one session per account, so any
  other login evicts this one; API calls and file downloads both re-authenticate and retry once.
  A first connection failure is retried three times, because the camera drops off Wi-Fi regularly.
* **The log tail matches a run of 25 trailing lines**, not a single line, because log lines repeat
  constantly and single-line matching silently skipped whole blocks.
* **Live video is fed to the player through a bounded queue** on its own thread. Writing directly
  from the receive loop stalls the socket when the player stops reading, which used to kill the
  stream after a few seconds.
* **Hardware decoding is disabled for the player** (`--avcodec-hw=none`) — D3D11VA cannot allocate
  pictures for a fragmented stream that starts mid-sequence, and the window stays black.
* **Player and stream both restart themselves** if they drop, and the player is re-primed with the
  MP4 initialisation segment so the picture returns.

## 4. Library

`UloManager.Core` can be referenced directly:

```csharp
using var device = new UloDevice(new UloConnectionOptions
{
    Host = "192.168.0.10",
    UserName = "user@example.com",
    Password = "…",
});

var info = await device.ConnectAsync();
Console.WriteLine($"{info.DeviceName}: {info.ModeSummary}");

await device.SetModeAsync(UloMode.Alert);

using var destination = UloDestination.Create(@"\\nas\ulo\video", "nas_user", "…");
await device.Media.DownloadAsync(destination, UloMediaType.Video, TimeSpan.FromHours(24));
```

Main types: `UloClient` (HTTP and session), `UloDevice` (all operations), `UloMediaService`,
`UloLogService`, `UloEventStream` (push channel), `UloLiveVideoStream`, `UloActivityMonitor`,
`UloAvailabilityService`, `UloDestination`, `UloCallRecorder` (records sent payloads, redacted).

## 5. Related documents

* [API reference](API.md) — the endpoints and protocols this implements
* [Use cases](USE_CASES.md) — scheduled sync, presence switching, housekeeping
* [Build guide](BUILDING.md) — Windows, Linux and IDE setup
* [Access research](ACCESS_RESEARCH.md) — device access attempts, hardware analysis, FCC findings
* [FCC filing](fcc/README.md) — internal photos, hardware docs, reference platform
