# Web application source analysis

The camera's built-in web application ships with its **source map** (`/build/main.js.map`, 6.1 MB,
served without authentication), from which the complete original TypeScript source — 173 vendor
files, 419 KB — was extracted. This document records what that source reveals about the device, the
API, and the attack surface. Everything here was read from the source and, where noted, confirmed
against live hardware.

The extracted source sits in [`firmware/10.1308/webapp/src/`](../firmware/10.1308/webapp/src/) and
[`firmware/06.0601/webapp/src/`](../firmware/06.0601/webapp/src/) (identical on both firmware
versions — same build).

---

## 1. Firmware version structure

The update logic in `device-state.ts` parses the version string and reveals its format:

```
10.1308
│  ││└─ STM  = 08   (STM32 head/motor firmware, the .bin files under firmware/)
│  └┘── APK  = 13   (application layer — the web server and Android app)
└────── APQ  = 10   (Qualcomm APQ Android system image)
```

The updater compares each component independently (`installSTM`, `installAPQ`, `installAPK`) and
installs only the parts that changed. This means a firmware like `10.1308` is not one monolithic
image but **three layers**: the Android system, the application, and the head microcontroller.

## 2. Undocumented WebSocket endpoint — `/api/v1/rtsp`

```typescript
// base.service.ts:638
this.appwebsocket = new WebSocket(
    this.getUrl((location.protocol == 'https:' ? 'wss:' : 'ws:')) + "/rtsp",
    'mudesign.ulo.rtsp');
this.appwebsocket.binaryType = 'arraybuffer';
```

A **third** WebSocket endpoint, in addition to the documented `/api/v1` (events, sub-protocol
`mudesign.ulo.json`) and `/api/v1/live` (fMP4 video, sub-protocol `mudesign.ulo.mp4`). Its
sub-protocol is `mudesign.ulo.rtsp` and it carries binary data.

**Confirmed working** on both firmware versions — WebSocket upgrade succeeds on ports 80, 8080, 443
and 8443. Its purpose in the source is unclear (used alongside the RTSP stream URL `rtsp://<host>:8901/live`)
and may be a tunnelling mechanism for environments where raw RTSP is blocked.

Not yet added to the endpoint registry because its data format is not understood.

## 3. Three WebSocket sub-protocols

| Endpoint       | Sub-protocol        | Mode          | Purpose                                               |
|----------------|---------------------|---------------|-------------------------------------------------------|
| `/api/v1`      | `mudesign.ulo.json` | text (JSON)   | Push events: mode changes, orientation, motion, power |
| `/api/v1/live` | `mudesign.ulo.mp4`  | binary (fMP4) | Live H.264 video fragments                            |
| `/api/v1/rtsp` | `mudesign.ulo.rtsp` | binary        | Unknown — possibly RTSP tunnelling                    |

## 4. Auth token leakage in URLs

The source passes the bearer token as a **URL query parameter** in several places:

```typescript
// base.service.ts:323 — generic helper
return this.getUrl() + pathParams + '?token=' + this.authToken;

// users.service.ts:177 — file download
url: self.baseService.getUrl() + ApiPaths.USERS + basePath + '/' + file
+ '?token=' + self.baseService.authToken

// home.ts:168 — media download
window.location.href = "http://" +
...
+"/media?filesname=" +
...
+"&header=" + encodeURIComponent(this.baseService.authToken)
+ "&userstatus=" +
...
+"&userid=" +
...

// today.ts:290 — RTSP link
window.location.href = "rtsp://" +
...
+":8901/live?header="
+ encodeURIComponent(this.baseService.authToken);
```

Tokens in URLs are logged by proxies, appear in browser history and referrer headers, and on this
device they travel in the clear over HTTP. This is a design choice in the vendor's app, not a bug.

**Relevance to ULO Manager:** our app sends the token only in the `Authorization` header, never in
the URL. This is deliberate.

## 5. Demo mode and shipped mock data

`base.service.ts` contains:

```typescript
public
isDemo = false;           // hardcoded, no external toggle
private
_authToken = "000000";   // default before login
```

When `isDemo` is true, every API call short-circuits and returns mock data from the `providers/mocks/`
directory. Demo mode **cannot be activated at runtime** — the Angular debug tools are stripped from
the bundle, so the service instance is unreachable from the browser console.

The mock data itself **is** shipped in every camera's JavaScript bundle:

| File             | What it contains                                                                                                                                                                                         |
|------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `mock-config.ts` | Device name "Fake Ulo", WiFi SSID "Livebox-0000" with password **"1234ABCD"**, email login "john@gmail.com" with password **"azerty"**, Access Everywhere account **"Ulo56941268"** / **"nbvcxwmlkjhg"** |
| `mock-user.ts`   | Admin user "john" with hashed password, two paired devices ("Paul's iPhone", "Paul's Galaxy Tab") with push tokens                                                                                       |
| `mock-users.ts`  | Three accounts: john (admin), mary (user), steve (guest) — passwords, emails, notification settings, paired devices                                                                                      |
| `mock-files.ts`  | Fake media file listings                                                                                                                                                                                 |
| `mock.state.ts`  | Battery, plug state, config mode                                                                                                                                                                         |

These are obviously test values, not production credentials. The security concern is that they are
readable by anyone on the network — and the Access Everywhere account structure
(`accountId` / `accountPassword`) documents how that feature authenticates, which is not described
anywhere else.

## 6. Voice commands

The configuration models list seven voice commands the camera understands:

| Command       | What it does               |
|---------------|----------------------------|
| `wakeUp`      | Wake the camera from sleep |
| `goToSleep`   | Put the camera to sleep    |
| `alertOn`     | Enable alert mode          |
| `alertOff`    | Disable alert mode         |
| `takePicture` | Take a snapshot            |
| `startVideo`  | Begin recording            |
| `stopVideo`   | Stop recording             |

Each can be enabled or disabled per recording mode (standard, spy, alert, battery) through
`PUT /api/v1/config/voice`.

## 7. What this tells us about gaining deeper access

**Nothing in the web app source reveals a shell, a debug port, or direct system access.**

The attack surface exposed by the source is the same as what was already documented:

| Vector                    | What the source confirms                                                                                                                                                                                                                                                                              |
|---------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **FOTA update**           | The app downloads firmware from a cloud endpoint, then installs it. Hijacking the download (MITM) would give code execution, but the cloud host is dead and the download is initiated by the camera, not pushed. Already documented in [SECURITY.md §3.4](SECURITY.md#34-s4--the-cloud-update-check). |
| **SD card import**        | `POST /api/v1/import` with a `{name: "filename"}` body restores a settings backup from the SD card. If a crafted backup could inject commands, this would be an entry point — but the backup is likely serialised configuration, not executable code. Requires physical SD card access.               |
| **System log**            | `/logs/system.txt` on 10.1308 is the full Android logcat (22 MB, no auth). It contains process names, kernel messages, and WiFi credentials, but no shell access.                                                                                                                                     |
| **The web server itself** | Mongoose/Civetweb family, no `Server` header, no known remote code execution path from the probing done. Directory traversal is blocked.                                                                                                                                                              |

The source does **not** contain: any reference to ADB, telnet, SSH, a debug shell, a UART
configuration endpoint, or a way to write to the filesystem beyond the media and backup paths.

## 8. What helps ULO Manager

| Finding                                      | How it helps                                                                                                                                                                                                                    |
|----------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Firmware version = APQ.APKSTM**            | We can show the three components separately in the status display, and the endpoint registry can match on each layer.                                                                                                           |
| **`/api/v1/rtsp` WebSocket**                 | A potential additional video source if the fMP4 live stream has issues — worth investigating its data format.                                                                                                                   |
| **Token in URL is how media downloads work** | Confirms that for downloading media files, the camera accepts `?token=` as an alternative to the `Authorization` header. Our app already uses the header, which is safer, but this is a fallback if the header path ever fails. |
| **Voice command list**                       | Could be exposed in the UI so users know what the camera listens for, and the voice config could be made editable.                                                                                                              |
| **Import from SD card**                      | The flow (`GET /api/v1/import` → list, `POST /api/v1/import` → restore) could be added to the Setup tab for restoring a backup from a card.                                                                                     |
| **Three account types**                      | admin, user, guest — the app can surface this distinction more clearly.                                                                                                                                                         |

## 9. Related documents

* [`firmware/10.1308/webapp/`](../firmware/10.1308/webapp/) — the extracted source and source maps
* [API reference](API.md) — the endpoint table built partly from this source
* [Security assessment](SECURITY.md) — the risk analysis that this source confirms
* [Easter eggs](EASTER_EGGS.md) — the Bollywood video and other oddities found alongside
* [Access research](ACCESS_RESEARCH.md) — ongoing attempts to reach beyond the web server
