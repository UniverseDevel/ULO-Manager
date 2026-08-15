# ULO HTTP API Reference

Everything known about the camera's API, verified against firmware **06.0601** unless stated
otherwise. Endpoint availability differs between firmware versions — see [§6](#6-firmware-version-differences).

Addresses below use `<ULO_IP>` as a placeholder.

## 1. Authentication

OAuth-style: exchange credentials for a bearer token.

```http
POST http://<ULO_IP>/api/v1/login
Authorization: Basic <base64 of user:password>
Content-Type: application/json

{ "iOSAgent": false }
```

```json
{
  "expiresIn": 3599,
  "token": "…",
  "userId": 1
}
```

Every later call sends `Authorization: Bearer <token>`. `POST /api/v1/logout` invalidates it.

Three things to know:

* **One session per account.** Logging in again with the same account silently invalidates the
  previous token; the next call then fails with
  `{"error":"Session does not exist, please authenticate again to continue!!"}`. Use a dedicated
  account per tool.
* **The camera rejects `Content-Type: application/json; charset=utf-8`** with `415 Unsupported
  Media Type`. Send the media type without parameters.
* **HTTP by default, HTTPS available.** The same server answers on **80** and **8080** (plain) and on
  **443** and **8443** (TLS), serving identical content. Every known client uses plain HTTP, so
  credentials and tokens normally travel unencrypted. TLS works — the full API was exercised over it
  on `06.0601` — but the certificate is self-signed `CN=localhost`, shipped in the image rather than
  generated per device, so it authenticates nothing on its own. `ulo --https --pin-cert <sha1>` pins
  it, which removes passive interception; see the
  [security assessment §3.7](SECURITY.md#37-s7--credentials-and-tokens-travel-in-the-clear) and the
  archived certificate in [`firmware/06.0601/device/`](../firmware/06.0601/device/README.md).
* **No `Server` header.** The web server is of the Mongoose/Civetweb family rather than Apache; see
  [security assessment §2](SECURITY.md#2-the-platform-underneath).

## 2. Endpoints

Confirmed by sweeping the device with `OPTIONS` (the camera answers with
`Access-Control-Allow-Methods`) and by reading the `ApiPaths` table out of the camera's own web
application.

### 2.1 Session and state

| Method          | Path             | Purpose                                                                              |
|-----------------|------------------|--------------------------------------------------------------------------------------|
| POST            | `/api/v1/login`  | Log in                                                                               |
| POST            | `/api/v1/logout` | Log out                                                                              |
| GET             | `/api/v1/state`  | Battery, mains power, admin present, setup-mode flag. **No authentication required** |
| GET, PUT, PATCH | `/api/v1/mode`   | Recording mode: `standard`, `spy`, `alert`                                           |
| GET, PUT, PATCH | `/api/v1/time`   | Camera clock                                                                         |

`GET /api/v1/mode` → `{ "mode": "alert" }`; set it with the same shape.

`GET /api/v1/state` → `{ "batteryLevel": 100, "config": false, "firmwareStatus": "none",
"hasAdmin": true, "plugged": true }`. The `config` flag is the **setup-mode** indicator, described
in [§4](#4-camera-modes).

### 2.2 Camera and recording

| Method          | Path               | Purpose                                                    |
|-----------------|--------------------|------------------------------------------------------------|
| POST            | `/api/v1/snapshot` | Take a picture now; returns `{ "filename": "media/…jpg" }` |
| GET, PUT, PATCH | `/api/v1/record`   | On-demand recording — `{ "running": true }`                |

`POST /api/v1/snapshot` with `{ "savePicture": 0 }` takes the picture **without** adding it to the
camera's stored recordings, which keeps its internal memory free when the picture is only a preview.
An empty body `{}` stores it. On both firmware versions tested the file is written to
`/media/{yyyyMMdd}/snapshot_{yyyyMMdd}_{HHmmss}.jpg` either way, stamped with the **camera's** clock.

> **Firmware 10.1308 breaks this response in two ways.** It answers with a bare `success` line inside
> the header block — a header without a colon, which is not valid HTTP — and its `filename` field is
> the truncated `"media/"` instead of the file it just wrote:
>
> ```
> HTTP/1.1 201 Created
> Content-Type: application/json; charset=utf-8
> Content-Length: 29
> success
>
> { "filename": "media/" }
> ```
>
> Standard HTTP clients refuse the whole response (.NET reports
> `Received an invalid header line: 'success'`), so the picture can never be read even though the
> camera took it. Firmware 06.0601 is well formed and additionally returns
> `Location: /api/v1/files/media/…`. A client therefore needs a tolerant parser for this one endpoint
> and, when no usable `filename` comes back, has to find the picture itself in
> `GET /api/v1/files/media/{day}` — comparing the timestamp in the file name against
> `GET /api/v1/time` so an older picture is not served instead.

### 2.3 Configuration

| Method          | Path                                | Purpose                                        |
|-----------------|-------------------------------------|------------------------------------------------|
| GET, PUT, PATCH | `/api/v1/config`                    | Whole configuration tree                       |
| GET, PUT, PATCH | `/api/v1/config/{section}`          | One section                                    |
| GET             | `/api/v1/config/wifi/networks`      | Wi-Fi scan (see the note below)                |
| GET             | `/api/v1/config/time/countries`     | Countries with their time zones                |
| POST            | `/api/v1/config/time/zones`         | Time zones for `{ "code": "SK" }`              |
| GET             | `/api/v1/config/language/languages` | Available languages                            |
| GET, PUT, PATCH | `/api/v1/config/reset`              | Advertised by `OPTIONS`, but `GET` answers 404 |

Sections: `access`, `alert`, `device`, `email`, `exclusion`, `eyes`, `face`, `firmware`,
`language`, `time`, `video`, `voice`, `wifi`.

> **The Wi-Fi scan is normally empty.** The camera only fills that list while it runs its own
> ad-hoc setup network; once connected it always answers `{"networks":[]}`. The camera's own web
> app never calls the endpoint at all — its Wi-Fi page is just an SSID box and a password box. Set
> the network by writing `{ "ssid": "…", "password": "…" }` to `/api/v1/config/wifi`.

### 2.4 Accounts

| Method                  | Path                               | Purpose                  |
|-------------------------|------------------------------------|--------------------------|
| GET, POST               | `/api/v1/users`                    | List and create accounts |
| GET, PUT, PATCH, DELETE | `/api/v1/users/{id}`               | Manage one account       |
| PUT                     | `/api/v1/users/{id}/notifications` | Notification matrix      |
| GET, POST               | `/api/v1/users/{id}/devices`       | Paired phones            |

An account is an administrator when its `account` field is `admin` rather than `user`.
`GET /api/v1/users` also returns the **push notification tokens** of every paired phone.

### 2.5 Recordings and storage

| Method          | Path                                | Purpose                                                         |
|-----------------|-------------------------------------|-----------------------------------------------------------------|
| GET, DELETE     | `/api/v1/files/media`               | All recordings; `?type=video` or `?type=snapshot` to filter     |
| GET, DELETE     | `/api/v1/files/media/{day}`         | One day, `yyyyMMdd`                                             |
| GET             | `/api/v1/files/media/{day}/count`   | Number of files that day                                        |
| GET             | `/api/v1/files/directoryCount`      | Number of recording folders                                     |
| GET             | `/api/v1/files/stats`               | Internal and SD card usage                                      |
| GET, PUT, PATCH | `/api/v1/files/backup`              | Move recordings to the SD card; `{ "running": true }` starts it |
| DELETE          | `/api/v1/files/delete?removeType=N` | Purge recordings                                                |

`removeType`: `0` oldest day, `1` oldest week, `2` oldest year, `3` last day, `4` last week,
`5` last year, `6` all. Requires an administrator account.

Files themselves are fetched from the plain path returned in the listings, for example
`GET http://<ULO_IP>/media/20260813/video_20260813_211805.mp4`.

> **The media tree is served without authentication.** `http://<ULO_IP>/media/` answers with a full
> directory index to anyone on the network, even when no session exists. The JSON API above
> correctly requires a token; the static file tree does not.

### 2.6 System and firmware

| Method | Path                                       | Purpose                                                      |
|--------|--------------------------------------------|--------------------------------------------------------------|
| GET    | `/api/v1/system/log`                       | System log, plain text                                       |
| GET    | `/api/v1/system/backups`                   | Settings backups held on the camera                          |
| POST   | `/api/v1/system/backup`                    | Create a settings backup                                     |
| POST   | `/api/v1/system/restore`                   | Restore a settings backup                                    |
| POST   | `/api/v1/system/reset`                     | Factory reset                                                |
| GET    | `/api/v1/interface/fotaStatus`             | `{ "isDownload": -1, "percentageDownload": 0 }`, `-1` = idle |
| GET    | `/api/v1/interface/fotaNumberOfUpdates`    | `{ "downloadCount": 0 }`                                     |
| GET    | `/api/v1/interface/fotaIsInstallAvailable` | `{ "isInstall": 1 }`                                         |
| GET    | `/api/v1/interface/fotaStartDownload`      | Start the over-the-air download                              |
| POST   | `/api/v1/interface/fotaInstallFirmware`    | Install a downloaded image                                   |

> **Checking for updates does not contact anything.** All four `fota*` read endpoints report state
> the camera already holds; none makes it query a server. On this firmware there is no endpoint that
> triggers a cloud check — the one that used to, `interface/CheckVersionOnCloud`, is gone. Only
> `fotaStartDownload` would generate outbound traffic.

### 2.7 Present in the firmware, used by no known client

`GET, POST /api/v1/behaviors` · `GET /api/v1/neighbors` · `POST /api/v1/admin` ·
`GET, POST /api/v1/import`

These answer to `OPTIONS` but appear in neither the camera's web application nor any documented
client. Their payloads are unknown.

## 3. WebSocket protocols

The camera's own web and mobile applications **do not poll**. They open two WebSockets.

### 3.1 Event channel — `ws://<ULO_IP>/api/v1`

* Sub-protocol **`mudesign.ulo.json`** — the handshake is refused without it.
* First frame must be `{"token":"<session token>"}`.
* The camera then pushes `{"event":"…","data":…}` messages.

State changes arrive as `state:<field>`, for example `{"event":"state:mode","data":"spy"}` when the
recording mode changes and `{"event":"state:config","data":true}` when the camera is turned upside
down. An invalid token is answered with
`{"event":"failure","data":"Session does not exist, please authenticate again to continue!!"}`;
a valid one is accepted silently. The official app reconnects every two seconds after a drop.

### 3.2 Live video — `ws://<ULO_IP>/api/v1/live`

* Sub-protocol **`mudesign.ulo.mp4`**.
* Binary frames carrying **fragmented MP4**: an initialisation segment (`ftyp` + `moov`) followed by
  `moof`/`mdat` fragments, so the bytes can be written straight to a playable `.mp4`.
* Measured: **H.264 1280x720 with AAC audio**, roughly 2.2 Mbit/s (~10 MB per minute).

> **This channel does not authenticate.** Opening it with no token frame at all still yields video.
> The event channel validates the token; the video channel does not.

`rtsp://<ULO_IP>:8901/live` is also documented as an unsecured live stream.

## 4. Camera modes

Two different things are both called "mode".

**Recording mode** — `standard` (awake, not recording), `spy` (awake, recording), `alert` (asleep,
recording on movement). Set through `/api/v1/mode`.

**Device mode** — reported by `state.config`, and driven by the camera's **physical orientation**:

| Position    | `state.config` | Meaning                                                                  |
|-------------|----------------|--------------------------------------------------------------------------|
| Upside down | `true`         | Admin/setup mode — when the camera's own app shows administrator screens |
| Upright     | `false`        | Normal usage mode                                                        |

This can change at any moment, so anything showing it should re-check regularly; the camera also
pushes `state:config` over the event channel. Configuration writes are accepted by the API in either
position — the orientation decides what the official app shows, not what the API allows.

## 5. Device quirks

* An unintended reboot resets the recording mode to `standard`. The camera does this on its own,
  which is why re-applying the intended mode on a schedule is worthwhile.
* The clock resets to `01/01/70` on every boot until NTP succeeds.
* System log timestamps are `dd/MM/yy` once the clock is synchronised, and `01/01/70` before that.
* The log is a single rolling buffer of roughly 600 lines, not strictly chronological, with no
  rotated files. Lines repeat constantly (`MCU NotifyPlugState=1` appears hundreds of times), so
  following it reliably means matching a run of trailing lines rather than a single line.
* `notifyVideoEvent - event=3` accompanies a new recording and `event=2` its end — derived by
  correlating log entries with the files produced at the same moment.
* The camera has **one video pipeline**: requesting a still picture while the live stream runs cuts
  the stream off. Do not do both at once.
* The camera drops off Wi-Fi from time to time ("Unexpectedly disconnected from network" in its own
  log), so calls can fail for a few seconds. Retry rather than treat it as fatal.
* `GET /api/v1/files/media/{day}/snapshotCount` answers `403` on 06.0601; use `…/count`.
* **The device identifier is only served from 08.0904 onwards.** `GET /api/v1/accessEverywhere`
  returns `"trimmedMac": "ab12"`, which the apps show as `ulo_ab12`; on 06.0601 the endpoint answers
  `404`. The value is simply the last four hex digits of the camera's MAC address, so on the local
  network the same identifier can be derived from ARP — verified on a live pair, where the camera at
  MAC `00-50-c2-bd-ab-12` is exactly the one reporting `trimmedMac: ab12`.
* **Some operations require the `standard` recording mode.** In `alert` (and `spy`) mode firmware
  10.1308 answers `GET /api/v1/system/backups` — and the other settings-backup calls — with `404`
  and `{"error": "Please switch to Standard mode to do this operation."}`. The `404` is misleading:
  the endpoint exists, the camera is just busy watching. Firmware 06.0601 serves it in any mode.
  Treat this as a temporary refusal, not a missing endpoint, and never make it part of a connect
  sequence.
* **The camera keeps one session per account.** Any other login with the same credentials (phone
  app, web UI, a second copy of a tool) silently invalidates the token, and every later call answers
  `401 Session does not exist`. Measured on 10.1308, an idle session died within five seconds of a
  second client signing in. Use a dedicated account per client and re-login transparently on `401`.
* **`POST /api/v1/snapshot` on 10.1308 emits an invalid HTTP header line** (`success`) and no file
  name — see [§2.2](#22-camera-and-recording).

## 6. Firmware version differences

Endpoint availability is **not** the same across firmware versions. The rows below marked
*measured* were tested directly against live `10.1308` and `06.0601` units; `08.0904` rows come from
the earlier research. `10.1308` is the newest firmware known (the version list of the original
controller reads `01.0101, 06.0601, 08.0803, 08.0804, 08.0904, 10.1308`), so **nothing carries an
upper version limit today**: a limit is recorded only once a newer firmware has been confirmed to
have *lost* the endpoint, in which case everything above it is hidden. "Not tested on a newer build"
is never written down as a limit.
| Endpoint | 10.1308 | 08.0904 | 06.0601 | Note |
|----------|---------|---------|---------|------|| `/api/v1/accessEverywhere` | **200** (measured) | **401** (exists) |
**404** (measured) | Present from 08.0904 onwards; source of the `ulo_xxxx` device ID |
| `/api/v1/backgroundImage` | **201** (measured) — returns `media/loginPicture.jpg` | untested | **404** (measured) |
*Not* legacy: it exists on the newest firmware and is missing on the oldest. It stores the picture as the login
background |
| `/api/v1/interface/CheckVersionOnCloud` | **200** `{"status":"success"}` (measured) | untested | **404** (measured) |
Still routed on 10.1308; only the cloud host behind it is dead |
| `/api/v1/interface/fotaVersion` | **404** (measured) | untested | **404** (measured) | In the web app's path table but
not routed on any tested firmware |
| `/api/v1/eyes`, `/api/v1/faces` | **404** (measured) | untested | **404** (measured) | Same — listed by the web app,
never routed |
| `/api/v1/files/media/{day}/snapshotCount` | **200** (measured) | untested | **403** (measured) | Use `…/count` on
06.0601 |
| `/api/v1/config/time/zones` | `POST` **200**, `GET` **405** (measured) | untested | `POST` **200**, `GET` **405** (
measured) | `POST` on both — the earlier "GET on 10.1308" claim is wrong |
| `/api/v1/config/time/countries` | **200** (measured) | untested | **200** (measured) | Returns every country **with
its `timeZones`** — 43 KB, the whole table in one call |
| `/api/v1/system/log` | **GET, POST** | **GET, POST** | **GET** only | The original controller switches on
`>= 08.0000` |
| `/api/v1/system/backups` | **404** in alert/spy mode (measured) | untested | **200** in any mode (measured) | 10.1308
requires standard mode — see [§5](#5-device-quirks) |
| `/api/v1/state` | untested | Returns `language` field | No `language` field | Extra field in 08.0904+ |
| `/api/v1/snapshot` | **invalid `success` header line, `filename` is `"media/"`** | untested | valid response with the
real `filename` and a `Location` header | Confirmed on live units; needs a tolerant parser on 10.1308 |
| `/logs/` directory | untested | **200** (open) | **404** | **System log exposed on 08.0904** |
| `/logs/system.txt` | untested | **200** (22 MB, no auth) | **404** | S1 — WiFi passwords in cleartext |

**Static file exposure by firmware:**

| Path          | 06.0601                                              | 08.0904                                                      |
|---------------|------------------------------------------------------|--------------------------------------------------------------|
| `/` (web app) | 200 — Ionic 2.0.0 / Angular 2.2.1 (built 2017-12-18) | 200 — Ionic 3.9.2 / Angular 5.2.0 (built 2019-02-22)         |
| `/media/`     | 200 — open directory listing, no auth                | 200 — same                                                   |
| `/build/`     | 200 — JS/CSS directory listing                       | 200 — JS/CSS directory listing                               |
| `/assets/`    | 200 — images, fonts                                  | 200 — images, fonts                                          |
| `/logs/`      | **404**                                              | **200** — `system.txt`, `debug.txt`, `log.txt`, ZIP archives |

**Unauthenticated endpoints (confirmed on both firmware versions):**

| Endpoint                                       | 06.0601 | 08.0904    |
|------------------------------------------------|---------|------------|
| `GET /api/v1/state`                            | ✓       | ✓          |
| `GET /api/v1/interface/fotaIsInstallAvailable` | ✓       | ✓          |
| `GET /api/v1/import`                           | ✓       | ✓          |
| `GET /media/`                                  | ✓       | ✓          |
| `ws://.../api/v1/live`                         | ✓       | ✓          |
| `GET /logs/system.txt`                         | ✗ (404) | ✓ **(S1)** |

**Complete API path table** extracted from the compiled web application (`build/main.js`):

```
ApiPaths.ADMIN              = "/admin"
ApiPaths.BACKUP             = "/backup"
ApiPaths.BEHAVIORS          = "/behaviors"
ApiPaths.CONFIG             = "/config"
ApiPaths.DELETE             = "/delete"
ApiPaths.DIRECTORYCOUNT     = "/directoryCount"
ApiPaths.EYES               = "/eyes"           (404 on 06.0601)
ApiPaths.FACES              = "/faces"          (404 on 06.0601)
ApiPaths.FILES              = "/files"
ApiPaths.FOTADOWNLOAD       = "/fotastartdownload"
ApiPaths.FOTAINSTALLAVAILABLE = "/fotaIsInstallAvailable"
ApiPaths.FOTAINSTALLFIRMWARE = "/fotaInstallFirmware"
ApiPaths.FOTANUPDATES       = "/fotaNumberOfUpdates"
ApiPaths.FOTASTATUS         = "/fotaStatus"
ApiPaths.FOTAVERSION        = "/fotaVersion"    (404 on 06.0601)
ApiPaths.IMPORT             = "/import"
ApiPaths.LIVE               = "/live"
ApiPaths.LOGIN              = "/login"
ApiPaths.LOGOUT             = "/logout"
ApiPaths.MODE               = "/mode"
ApiPaths.NEIGHBORS          = "/neighbors"
ApiPaths.NOTIFICATIONS      = "/notifications"
ApiPaths.RECORD             = "/record"
ApiPaths.SNAPSHOT           = "/snapshot"
ApiPaths.STATE              = "/state"
ApiPaths.STATS              = "/stats"
ApiPaths.SYSTEM_BACKUP      = "/system/backup"
ApiPaths.SYSTEM_BACKUPS     = "/system/backups"
ApiPaths.SYSTEM_RESET       = "/system/reset"
ApiPaths.SYSTEM_RESTORE     = "/system/restore"
ApiPaths.TIME               = "/time"
ApiPaths.USERS              = "/users"
```

All paths are relative to `/api/v1`. The base URL is constructed as
`http://<host>/api/v1` + path. The `FOTA*` paths are prefixed with `/interface/` in the
HTTP layer (e.g. `GET /api/v1/interface/fotaStatus`).

> **Absence from the 10.1308 list is not evidence of absence on that firmware.** That list was
> gathered by watching browser traffic and is explicitly partial — its author noted "there are many,
> many more when using ULO upside down". Only the four `404`/`403`/`405` rows above are genuine
> negative results, because those were actively tested on 06.0601.

If you have a unit on another firmware, the quickest way to enumerate it is an `OPTIONS` sweep: the
camera answers every known path with its allowed methods, and unknown paths with `404`.

## 7. Appendix — the original scraped endpoint list

Preserved from the project's first API notes, gathered by watching browser traffic against a
`10.1308` unit. It is explicitly partial ("there are many, many more when using ULO upside down")
and is reproduced here as the historical record; §2 above is the verified reference.

| Endpoint                                     | Method        | Original note                                                                                                                      |
|----------------------------------------------|---------------|------------------------------------------------------------------------------------------------------------------------------------|
| `/api/v1/login`                              | POST          | Login — creates the token used for all other calls                                                                                 |
| `/api/v1/time`                               | PUT           | ULO's current time                                                                                                                 |
| `/api/v1/state`                              | GET           | Power information                                                                                                                  |
| `/api/v1/accessEverywhere`                   | GET           | ULO device information                                                                                                             |
| `/api/v1/backgroundImage`                    | POST          | Current snapshot, to be used as a background                                                                                       |
| `/api/v1/config`                             | GET           | List of configured parameters                                                                                                      |
| `/api/v1/config/language/languages`          | GET           | Available languages                                                                                                                |
| `/api/v1/users`                              | GET           | All users                                                                                                                          |
| `/api/v1/config/time/countries`              | GET           | Available countries                                                                                                                |
| `/api/v1/config/time/zones`                  | GET           | Available time zones                                                                                                               |
| `/api/v1/config/wifi/networks`               | GET           | Available WiFi networks                                                                                                            |
| `/api/v1/system/log`                         | GET           | System log                                                                                                                         |
| `/api/v1/users/1`                            | GET           | User with ID 1, usually the administrator                                                                                          |
| `/api/v1/files/stats`                        | GET           | Storage statistics                                                                                                                 |
| `/api/v1/interface/CheckVersionOnCloud`      | POST          | Initiate an update check. Contacted `34.232.121.46`, which traces to `ec2-34-232-121-46.compute-1.amazonaws.com`, no longer active |
| `/api/v1/files/media`                        | GET           | All media files in all directories                                                                                                 |
| `/api/v1/files/media?type=snapshot`          | GET           | Filtered to snapshots                                                                                                              |
| `/api/v1/files/media?type=video`             | GET           | Filtered to video                                                                                                                  |
| `/api/v1/files/media/20190623`               | GET           | Media files for one day                                                                                                            |
| `/api/v1/files/media/20190623/snapshotCount` | GET           | Number of snapshots in that path                                                                                                   |
| `/api/v1/files/delete?removeType=6`          | DELETE        | Delete files on local storage — 0 oldest day, 1 oldest week, 2 oldest year, 3 last day, 4 last week, 5 last year, 6 all time       |
| `ws://<ULO_IP>/api/v1/live`                  | binary stream | Live feed, unsupported by the original controller                                                                                  |
| `rtsp://<ULO_IP>:8901/live`                  | RTSP          | Live stream, unsecured                                                                                                             |
| `/api/v1/logout`                             | POST          | Logout, invalidating the token                                                                                                     |

## 8. Related documents

* [Application guide](APPLICATION.md) — the tool that implements all of the above
* [Use cases](USE_CASES.md) — scheduled sync, presence-based mode switching, backups
* [Security assessment](SECURITY.md) — what is exposed, and what to do about it
