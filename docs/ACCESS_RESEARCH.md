# ULO Access Research

Log of attempts to gain deeper access to the ULO camera for the purpose of community firmware
development and continued use of an otherwise abandoned product.

Research started 2026-08-14.

---

## Goal

Mu Design has effectively abandoned the ULO camera — no firmware updates, no support, no open API.
The device is full of security holes (see [SECURITY.md](SECURITY.md)) and will eventually become
e-waste unless the community can take over. The goal is to find a **reproducible, non-destructive
way for any ULO owner to gain full access to their own device**, ideally through a simple script
that does not require IT expertise.

---

## Device access summary

| Access level                     | Status                    | How                                                      |
|----------------------------------|---------------------------|----------------------------------------------------------|
| Unauthenticated video stream     | ✅ Already available       | `ws://<IP>/api/v1/live` — no credentials needed          |
| Unauthenticated media download   | ✅ Already available       | `http://<IP>/media/` — open directory listing            |
| Full API control (authenticated) | ✅ Already available       | Login with account credentials, see [API.md](API.md)     |
| Head MCU firmware flash          | ⚠️ Theoretically possible | CRC-32 only, no signature — see attempt 1                |
| Android-side shell (ADB)         | ❓ Unknown                 | Port 5555 was closed on tested unit — needs more probing |
| Android-side root                | ❓ Unknown                 | Depends on getting a shell first                         |
| Custom Android firmware          | ❌ Not yet attempted       | Requires the APQ-side image, which is not published      |

---

## Attempt 1 — Reconnaissance probe

**Date:** 2026-08-14
**Method:** Network-only, read-only — port scan, endpoint enumeration, directory listing, ADB
check, system log analysis.
**Script:** [`forensics/ulo_probe.py`](../forensics/ulo_probe.py) - see [`forensics/`](../forensics/README.md) for the
rest of the analysis scripts
**Risk:** None (all read-only requests).
**Target users:** Anyone comfortable running a Python script.

### What the probe does

1. Scans ~35 common ports (SSH, Telnet, ADB, HTTP variants, RTSP, etc.)
2. Inspects TLS certificates on HTTPS ports
3. Tests all known API endpoints without credentials (finds what is open)
4. Tests ~80 undocumented/speculative paths (debug consoles, Android paths, traversal)
5. Runs an OPTIONS sweep to discover hidden HTTP methods
6. Enumerates directory listings (`/media/`, `/logs/`, `/firmware/`, etc.)
7. Checks for ADB on ports 5555–5558
8. Downloads and analyses the system log for debug clues (UART, boot, partitions)

### How to run

```
python forensics/ulo_probe.py 192.168.0.10
python forensics/ulo_probe.py 192.168.0.10 --user you@example.com --password yourpass
```

Output goes to `ulo_probe_results.json` — share this file to help the research.

### Status

**Run against live device on 2026-08-14** against the `06.0601` unit on a private LAN. Full results in
`ulo_probe_results.json` (generated locally; not committed, it contains device and network details).

### Results

**Port scan:**

| Port       | Status | Service                                      |
|------------|--------|----------------------------------------------|
| 80         | Open   | HTTP (Mongoose/Civetweb)                     |
| 443        | Open   | HTTPS — TLSv1.2, ECDHE-RSA-AES256-GCM-SHA384 |
| 8080       | Open   | HTTP (mirror of 80)                          |
| 8443       | Open   | HTTPS (mirror of 443)                        |
| 5555       | Closed | ADB — **not available**                      |
| All others | Closed | No SSH, Telnet, or other services            |

**Unauthenticated endpoints (no login needed):**

| Endpoint                                       | Response                                                                                    | Notes                                                              |
|------------------------------------------------|---------------------------------------------------------------------------------------------|--------------------------------------------------------------------|
| `GET /api/v1/state`                            | `{"batteryLevel":100,"config":true,"firmwareStatus":"none","hasAdmin":true,"plugged":true}` | Device state — presence/occupancy signal                           |
| `GET /api/v1/interface/fotaIsInstallAvailable` | `{"isInstall":1}`                                                                           | Firmware update pending!                                           |
| `GET /api/v1/import`                           | `{"backups":[]}`                                                                            | **Settings backup import from SD — unauthenticated, accepts POST** |
| `GET /media/`                                  | Directory listing                                                                           | All recordings — **no auth**                                       |
| `ws://<IP>/api/v1/live`                        | Live H.264 video                                                                            | **No auth** (confirmed in SECURITY.md)                             |

**Web application exposed:**

The entire Ionic 2 / Angular 2 web application is served with **directory listings enabled**:

| Path             | Content                                                         |
|------------------|-----------------------------------------------------------------|
| `/`              | `index.html` — Ionic app shell                                  |
| `/build/`        | Compiled JS/CSS (`main.js` = 6.2 MB, contains all client logic) |
| `/assets/`       | Images, fonts, translations                                     |
| `/manifest.json` | PWA manifest                                                    |

The compiled `main.js` was downloaded and analysed, revealing the complete API path table
(`ApiPaths`), the backup/restore flow, and the FOTA update mechanism.

**All 32 ApiPaths found in the web app:**

```
/admin          /backup         /behaviors      /config
/delete         /directoryCount /eyes           /faces
/files          /fotaInstallFirmware            /fotaIsInstallAvailable
/fotaNumberOfUpdates            /fotaStartDownload (case: /fotastartdownload)
/fotaStatus     /fotaVersion    /import         /live
/login          /logout         /mode           /neighbors
/notifications  /record         /snapshot       /state
/stats          /system/backup  /system/backups /system/reset
/system/restore /time           /users
```

New/undocumented endpoints discovered: `/faces`, `/eyes`, `/fotaVersion` (all 404 on firmware
`06.0601` — may exist on other versions).

**Directory traversal:** `/media/../` and `/media/../../` both normalize back to `/media/` — the
web server blocks traversal.

**ADB:** Not available on any port. No route to a shell via network.

**System log:** Requires authentication — could not read without credentials.

### Key findings

1. **`/api/v1/import` is unauthenticated and accepts POST.** This is the settings backup import
   from SD card. The flow in the web app is:
    * `GET /api/v1/import` → lists backup files on the SD card
    * User selects a backup → `POST /api/v1/system/restore` with `{"name": "<filename>"}`
    * If a crafted backup could be placed on the SD card, it could potentially modify device
      settings without authentication.

2. **`fotaIsInstallAvailable` returns `{"isInstall": 1}`** — the device believes a firmware update
   is available. The FOTA flow needs further investigation.

3. **The entire web app source is downloadable** — any API endpoint the official app uses can be
   discovered and replicated.

4. **No shell access via network.** ADB, SSH, Telnet are all closed. Physical UART/JTAG is the
   only path to a shell.

---

## Attempt 1b — Second camera probe (firmware `08.0904`)

**Date:** 2026-08-14
**Device:** the second unit on the same LAN, firmware version **`08.0904`**
**Method:** Same probe as attempt 1, plus system log analysis.

### Firmware version comparison

| Property                     | Camera 1 (`06.0601`)                           | Camera 2 (`08.0904`)                                                                    |
|------------------------------|------------------------------------------------|-----------------------------------------------------------------------------------------|
| Firmware                     | `06.0601`                                      | `08.0904`                                                                               |
| Web app                      | Ionic 2.0.0 / Angular 2.2.1 (built 2017-12-18) | Ionic 3.9.2 / Angular 5.2.0 (built 2019-02-22)                                          |
| `/logs/` exposed             | **No** (404)                                   | **Yes** — `system.txt` (22.3 MB), `debug.txt` (566 KB), `log.txt` (189 KB), ZIP archive |
| `/logs/system.txt`           | 404                                            | **200 — full Android logcat, unauthenticated!**                                         |
| WiFi passwords in log        | Fixed on this version                          | **Present in cleartext** (S1 confirmed)                                                 |
| `/api/v1/accessEverywhere`   | 404                                            | 401 (exists, needs auth)                                                                |
| `/api/v1/state` extra fields | —                                              | Includes `language` field                                                               |
| `/api/v1/system/log` methods | GET                                            | **GET, POST**                                                                           |
| SELinux                      | Unknown                                        | **Permissive**                                                                          |

### Key intelligence from system log

The 22.3 MB system log is a full Android `logcat` dump, readable without any authentication. Key
findings (**WiFi credentials redacted**):

**Platform details:**

- **SELinux: permissive** — no mandatory access controls. A shell would have unrestricted access.
- **Qualcomm Adreno GPU** — EGL 1.4, OpenGL ES, build date 2016-08-29
- **UART: `ttyHS`** present — Qualcomm high-speed UART, confirms serial console exists on the PCB
- **Partitions:** Standard Android layout (`/system`, `/data`, `mmcblk`)
- **Firmware:** `currentversion: 08.0904`, `cloudversion: 08.0904`

**Application stack:**

- App package: `lu.mudesign.ulo` at `/data/app/lu.mudesign.ulo-1/lib/arm/`
- Native libraries: `libulo-core.so`, `libquazip5.so`
- Qt modules: Qt5Network, Qt5SerialPort, Qt5Xml, Qt5Gui, Qt5AndroidExtras, Qt5Bluetooth
- Speech recognition engine present (`SpeechRecognizer`)
- STM backup files: "1 Backup STM file(s) available"

**WiFi credential exposure (S1 — confirmed, redacted):**
The log contains `Wifi::updateConfiguration(QJsonObject)` lines with **SSID and plaintext
password** for every network the device has joined. This confirms vulnerability S1 from the
security assessment. The log is never rotated and is accessible without authentication.

> ⚠️ **If you sell or discard a ULO running this firmware, anyone who reads
> `http://<IP>/logs/system.txt` gets your WiFi password.**

### Significance for access research

1. **SELinux permissive** means that if we get a shell (via UART, exploit, or any other path),
   there are no kernel-level access controls to bypass. Root = full control.
2. **`ttyHS` (Qualcomm UART)** confirms a serial console exists on the hardware. This is the
   most reliable path to a shell for anyone willing to open the case.
3. **STM backup files** are present — the device keeps head MCU firmware images, which could be
   extracted via UART for bootloader analysis.
4. **The log file itself is a rich source of protocol information** — login events, wake/sleep
   cycles, SSL handshake timing, video event triggers, and MCU communication patterns.

---

## Attempt 2 — Firmware update path (planned)

**Date:** Not yet attempted
**Method:** Use the device's own firmware update mechanism to install a modified head MCU image.
**Risk:** Medium — could brick the head MCU (display/sensors). The Android side would be
unaffected.
**Target users:** Would need to be packaged as a one-click tool.

### Background

The head MCU firmware (STM32 Cortex-M4F) uses **CRC-32 as its only integrity check** — no
cryptographic signature (see [SECURITY.md §4.2.3](SECURITY.md)). The full image format is
documented:

* Load address: `0x08020000`
* Entry point: `0x08020200`
* Image size: 384 KiB (fixed, `0xFF` padded)
* Integrity: STM32 hardware CRC-32 over `image[:-4]`, stored as the last 4 bytes
* The CRC can be recomputed with a few lines of Python

### What a modified image could do

The head MCU controls the **display, sensors, camera signalling and APQ wake/sleep**. A modified
image could:

* Add a diagnostic display mode showing IP address, firmware version, debug info
* Change LED/eye behaviour to signal custom states
* Modify the APQ IPC protocol to enable new commands
* **It cannot directly give a shell** — the MCU has no network stack; all networking is on the
  APQ/Android side

### What is missing

1. **The update delivery mechanism is not fully understood.** The API has
   `fotaStartDownload` and `fotaInstallFirmware`, but we do not know:
    * Where the device expects to download from (the old cloud endpoint is dead)
    * Whether the Android side performs additional validation before passing the image to the MCU
    * Whether the bootloader (`0x08000000`–`0x0801FFFF`, not in this repository) performs any
      additional checks beyond the CRC-32

2. **The bootloader has not been audited.** If it verifies a signature that is not in the image
   format, a modified image will be rejected regardless of the CRC.

3. **No rollback protection exists** — but also no known way to trigger the update from outside
   without understanding step 1.

### Next steps

* Run the probe (attempt 1) to gather more data about the FOTA endpoints
* Capture network traffic during a firmware update attempt to understand the download protocol
* Try serving a known-good firmware image from a local HTTP server and pointing the device at it
* If that works, serve a minimally modified image (e.g. change a display string) as a proof of
  concept

---

## Attempt 3 — Physical access / UART (planned)

**Date:** Not yet attempted
**Method:** Open the device, locate UART/JTAG test pads on the APQ board, connect a serial
adapter.
**Risk:** Low if done carefully (non-destructive; standard embedded development practice).
**Target users:** Not suitable for non-technical users; results would inform a software-only path.

### Why this matters

The APQ/Android side is where the network stack, web server, API and video pipeline live. A UART
console would likely give:

* A Linux/Android shell (possibly root)
* Boot log with partition layout, kernel version, SELinux mode
* Access to the Android filesystem — the APQ-side application, configuration, keys
* The ability to enable ADB permanently (making future access software-only)

### What to look for

* 4-pin header or test pads near the Qualcomm APQ chip (GND, TX, RX, VCC)
* Typical baud rates: 115200, 921600, 1500000
* The system log mentions `VVDN:` prefixed messages — VVDN's reference designs typically expose
  UART

### Equipment needed

* USB-to-UART adapter (FTDI, CP2102, CH340 — ~€5)
* Multimeter to identify GND and TX
* Terminal software (PuTTY, minicom, screen)

---

## Attempt 2 — CVE analysis and exploitation testing

**Date:** 2026-08-15
**Method:** Active probing — directory traversal, command injection, file upload, config injection,
binary protocol analysis, system log deep-dive.
**Risk:** Low (no persistent changes made to device).

### Port 55555 — internal IPC status socket

Both cameras expose port 55555. Extended protocol analysis:

- Sends exactly **6 bytes** on connect: `00 01 00 00 00 01` — then goes completely silent
- **Ignores all input** — tested: echo back, structured commands (0x00–0xFF), payloads, JSON
- **Single-client only** — second simultaneous connection is refused
- No periodic heartbeat — just the initial 6 bytes, holds connection open indefinitely
- Behaviour identical in both standard and setup modes

Decoded as big-endian uint16 triplet: `(1, 0, 1)` — likely `(protocol_version=1, reserved=0, state=1)`.

**Conclusion:** This is an internal IPC status socket. The Qt application (`lu.mudesign.ulo`)
opens this port for a single local process (likely the STM32 communication daemon or a watchdog
service) to connect and receive state notifications. It was never designed for external
interaction and cannot be exploited — it accepts no commands whatsoever.

### Directory traversal — blocked

Tested 11 traversal variants including double-encoding, `/media/../../`, and Android-specific
paths (`/data/user/0/lu.mudesign.ulo/files/`, `/system/build.prop`, `/proc/version`).
All return 404. The web server canonicalises paths before resolving.

### Command injection via backup name — not possible

Tested 13 payloads on both cameras (with storage freed): `test;id`, `test$(id)`,
`` test`id` ``, `test|id`, `test&&id`, `../../../tmp/pwned`, `../../sdcard/pwned`,
`test.sh`, `test.bin`, `a;echo PWNED>/tmp/test`, null bytes, and a normal name.

**Result:** The camera completely ignores the user-supplied `name` field. All attempts produce
the same auto-generated filename `ulo_YYYYMMDD_HHMMSS.zip`. The parameter is decorative —
it is never interpolated into a shell command or filesystem path.

- fw 06.0601: returns "cannot create backup file ulo_YYYYMMDD_HHMMSS.zip" (storage issue)
- fw 10.1308: returns "Backup failed. Please try again..."
- Neither camera creates any backup — filesystem errors persist regardless of input
- No evidence the name reaches any shell or path construction

### File upload (CVE-2020-27304 style) — rejected

```
POST /api/v1/import  Content-Type: multipart/form-data
→ 415 "unexpected content type, expected application/json"
```

### Config injection — strict validation

```
PUT /api/v1/config {"adb": true}     → 422 "unexpected section 'adb'"
PUT /api/v1/config {"debug": true}   → 422 "unexpected section 'debug'"
PUT /api/v1/config {"shell": {...}}  → 422 "unexpected section 'shell'"
```

### WebDAV (PROPFIND/MKCOL) — read-only, web-root-scoped

The HTTP `Allow` header reveals WebDAV methods: `GET, POST, HEAD, CONNECT, PUT, DELETE, OPTIONS, PROPFIND, MKCOL`.

`PROPFIND` with `Depth: 1` returns XML directory listings for web-app directories:

- `/build/` → 8 files (main.js, main.js.map, vendor.js, polyfills.js, css, sw-toolbox, qt_temp files)
- `/assets/` → 6 subdirs (fonts, i18n, icon, img, js, sounds)
- `/assets/sounds/` → camera-click.wav + the Bollywood video
- `/assets/fonts/` → 34 files including Qt temporary lock files

**Write attempts failed:**

- `MKCOL /media/test_dir` → empty response (connection dropped, no directory created)
- `PUT /media/test.txt` → empty response (file not created, verified by GET → 404)
- `PUT /tmp/test.txt` → 401 Unauthorized
- `MKCOL /test_write` → 401 Unauthorized

**Filesystem paths not reachable:** PROPFIND on `/data/`, `/system/`, `/proc/`, `/tmp/`, `/etc/`,
`/mnt/`, `/sdcard/` all return 404. The WebDAV is scoped to the web server's document root only —
no filesystem escape.

**Conclusion:** WebDAV gives read-only file listing within the web app tree. No write capability,
no filesystem traversal beyond what HTTP GET already provided.

### Setup mode testing (device flipped upside-down)

Both cameras tested with `"config": true` (verified via `/api/v1/state`).

**Ports:** Identical to standard mode — 80, 443, 8080, 8443, 55555. No ADB (5555), no SSH (22).

**Setup-specific endpoints that work:**

- `GET /api/v1/config/wifi/networks` → returns visible WiFi SSIDs (blocked in standard mode)
- `GET /api/v1/neighbors` → discovers other ULO cameras on the LAN

**No change observed:**

- Same API surface, same WebDAV behaviour, same port 55555
- Backups still blocked ("switch to Standard mode")
- No debug services enabled
- FOTA status unchanged

**Conclusion:** Setup mode is purely for WiFi provisioning via the mobile app + BLE. It does not
enable any developer or debug access.

### SELinux analysis (from system.txt, 22 MB, no auth on 10.1308)

The ULO app runs under SELinux enforcing mode as `untrusted_app`:

```
scontext=u:r:untrusted_app:s0:c512,c768
```

Observed denials:

- Write to `/tmp` (labelled `shell_data_file`)
- Create lock file `LCK..ttyHSL1` (serial port to STM32)
- Write to dalvik-cache
- Certain socket ioctls

**Implication:** Even a successful web server RCE lands in a sandboxed SELinux domain.
Firmware replacement or ADB enablement would require additional privilege escalation.

### UART confirmed — `ttyHSL1`

The system log shows the app attempting to lock `ttyHSL1` — a Qualcomm High Speed UART port.
This is the serial communication channel between the APQ (Android SoC) and the STM32 head MCU.

On APQ8016-class devices:

- Baud: 115200 8N1
- Level: 3.3V
- Typically exposed on a 4-pin header or test pads (TX, RX, GND, VCC)

This is the **most reliable path to a root shell**. It bypasses all software protections.

### FOTA mechanism (from system.txt logs)

```
VVDN: fota: bsp_version is "8"
VVDN: fota: stm version file exist
Fota: APK major=0, minor=9, patch=0
Fota: cloudSTM checksum and filepath mismatch
Fota: cloudBSP checksum OR bsp filepath NULL
Fota: cloudAPK checksum OR apk filepath NULL
```

The FOTA system checks three components independently (STM, BSP/APQ, APK) against checksums
from a cloud response. The cloud server (`34.232.121.46`) is dead. A MITM attack serving a
fake response could trigger firmware download, but:

- **STM32:** Only CRC-32 verification — replacement trivial
- **APQ/BSP:** Likely Qualcomm secboot — probably signed
- **APK:** Unknown verification — may or may not be signed

### Bluetooth GATT service

```
BluetoothGattServer: addService() - service: a3ceb858-9de1-11e7-abc4-cec278b6b50a
BluetoothGattServer: registerCallback() - UUID=f9aa1392-4af6-4b19-80d1-4920984ddd47
```

Used for initial WiFi provisioning via the mobile app. Characteristics and write permissions
are unknown — requires BLE enumeration with nRF Connect or `gatttool`.

### Applicable CVEs

| CVE            | Target                            | Applicable? | Why                                      |
|----------------|-----------------------------------|-------------|------------------------------------------|
| CVE-2020-27304 | CivetWeb path traversal in upload | ❌           | Server rejects multipart bodies          |
| CVE-2025-55763 | CivetWeb buffer overflow in URI   | ⚠️          | Possible but lands in SELinux sandbox    |
| CVE-2018-20352 | Mongoose use-after-free in CGI    | ❌           | No CGI handlers on device                |
| CVE-2017-11567 | Mongoose CSRF → config → RCE      | ❌           | No admin UI, direct API access available |

### Conclusion

**No purely network-based path to root was found.** The device is well-protected by:

1. Strict API input validation (rejects unknown sections/types)
2. Path canonicalisation (no traversal)
3. SELinux enforcing (web server is sandboxed)
4. No exposed debug services (ADB, SSH, Telnet all closed)

**Viable paths requiring physical access:**

1. **UART** (`ttyHSL1`) — connect serial adapter, get bootloader/shell
2. **JTAG/SWD** on the STM32 — direct MCU programming (requires identifying pins)

**Viable paths requiring network interception:**

1. **FOTA MITM** — ARP-spoof + fake update server (STM32 part only confirmed exploitable)

---

## Attempt 3 — Similar hardware research and manufacturer documentation

**Date:** 2026-08-15
**Method:** Web research — FCC filings, reference hardware documentation, community projects.

### FCC Filing (FCC ID: 2ANJS-ULO1)

The ULO camera has a public FCC filing with:

- **Internal photos** (18 pages) — PCB layout, components, antenna
- **Schematics** (confidential, metadata only)
- **Block diagram** (confidential, metadata only)
- **Operational description** (confidential, metadata only)

URL: https://fccid.io/2ANJS-ULO1/Internal-Photos/Internal-Photos-3564626

The internal photos should reveal UART test pads, boot-select pins, and whether USB data lines
are routed to the micro-USB connector. The confidential schematics would be the definitive
reference but are not publicly available.

### Reference hardware: DragonBoard 410c (same APQ8016 SoC)

The ULO's APQ (Qualcomm Applications Processor) is the APQ8016 (Snapdragon 410 family),
the same chip used in the DragonBoard 410c reference board. Key documented access methods:

#### SD card boot

The APQ8016 supports booting from SD card via hardware strap pins (`MS_BOOT_CONFIG[1:0]`):

- `00` = eMMC (default)
- `01` = SD card

On the DragonBoard 410c, SD card boot is triggered by **holding the Vol- button at power-on**.
The ULO may have a similar mechanism if the button signal is routed to the boot-config GPIO.

**What to try:** Insert a bootable SD card (postmarketOS or custom Linux image built for
APQ8016), power-cycle the ULO while holding the button on its base (if one exists), or while
the camera is flipped upside down (which triggers "setup mode" — this might relate to boot-
config pin state).

#### Qualcomm EDL (Emergency Download) mode — USB 9008

APQ8016 devices can enter EDL mode, which allows full partition read/write via the
`Qualcomm HS-USB QDLoader 9008` USB device. Methods to trigger EDL:

1. **EDL cable** — A modified USB cable with a 910kΩ resistor between D+ and GND. Plug in
   while device is off → forces EDL mode on many Qualcomm devices.
2. **Test points** — Shorting specific EDL test pads on the PCB while connecting USB.
3. **Software** — `adb reboot edl` or `fastboot oem edl` (requires existing ADB/fastboot).

**Prerequisite:** The ULO's micro-USB connector must have data lines connected (not just
power for charging). The FCC internal photos should reveal this.

**If EDL mode works**, tools like QFIL (Qualcomm Flash Image Loader) or open-source
`qdl`/`edl` can read/write all flash partitions, including system, boot, and recovery.

#### Fastboot mode

If the bootloader is accessible (via UART interrupt or button combo at boot), `fastboot`
allows flashing individual partitions. On APQ8016:

- `fastboot flash boot boot.img` — replace kernel/ramdisk
- `fastboot flash system system.img` — replace Android system
- `fastboot oem unlock` — unlock bootloader (if supported)

### Community firmware projects for APQ8016/MSM8916

| Project                       | URL                                                                      | Relevance                           |
|-------------------------------|--------------------------------------------------------------------------|-------------------------------------|
| postmarketOS DragonBoard 410c | https://wiki.postmarketos.org/wiki/Arrow_DragonBoard_410c_(arrow-db410c) | Full Linux on APQ8016               |
| MSM8916 mainlining            | https://wiki.postmarketos.org/wiki/MSM8916_Mainlining                    | Upstream kernel for this SoC family |
| APQ8016E documentation        | https://github.com/pwnall/qualcomm-apq8016e-docs                         | Datasheets, register maps           |
| Qualcomm camera subsystem     | https://www.kernel.org/doc/html/latest/media/qcom_camss.html             | V4L2 driver for camera hardware     |

### SD card firmware auto-flash pattern

Many IoT cameras (Yi, Wyze, Wuuk, etc.) support automatic firmware flashing from SD card:

1. Place firmware file with specific name at SD card root (e.g., `update.bin`, `demo.bin`)
2. Power cycle the device
3. Device detects file, flashes itself automatically

**ULO's SD card is accessible** — we know the camera has an SD slot and the API has
`POST /api/v1/import` for restoring backups from SD. It's possible that:

- A specially named file on the SD card triggers firmware update on boot
- The FOTA system checks the SD card as a firmware source
- The bootloader has an SD card recovery mode

**What to try:** Place files with names like `update.bin`, `ulo_firmware.zip`,
`autoupdate.bin`, `recovery.zip`, `firmware.bin` on an SD card and insert + power cycle.
Monitor the camera's eye display for any unusual boot animation.

### VVDN Technologies (ODM)

VVDN built the hardware platform. They provide camera engineering services including BSP
development. Their SDK/documentation is not public (NDA-protected). However:

- VVDN camera platforms typically use standard Qualcomm BSP (Board Support Package)
- They follow Qualcomm's reference design closely (DragonBoard 410c pattern)
- The `VVDN:` prefix in log messages confirms their software layer

### Key experiment to try next

1. **Download FCC internal photos** — identify UART pads, USB data routing, boot switches
2. **Try EDL cable** (910kΩ D+ to GND) while connecting USB with camera powered off
3. **Try SD card boot** — prepare postmarketOS APQ8016 image on microSD, insert, power cycle
4. **Try SD card auto-flash** — place various firmware filenames on SD card, power cycle
5. **Flip camera upside down during boot** — "setup mode" might change boot-config GPIO state

---

## Ideal end state

The ideal outcome for non-technical ULO owners would be:

1. **A one-command script** that connects to the camera over the network and installs a community
   firmware or patch — no physical access, no soldering, no IT knowledge beyond "run this".
2. **A community firmware** that fixes the security holes, adds modern features (RTSP with auth,
   ONVIF, local storage management, Home Assistant integration) and keeps the owl personality.
3. **OTA updates** served from a community server, replacing the dead vendor infrastructure.

Getting there requires solving the access problem first. Each attempt above moves toward that goal.

---

## How to contribute

If you own a ULO and want to help:

1. **Run the probe script** and share `ulo_probe_results.json` — every unit's results help build
   the picture.
2. **If you are comfortable opening the case**, photograph the PCB (both sides) and look for
   labelled test pads.
3. **If you have a UART adapter**, try connecting to any exposed pads and share the boot log.
4. **If you find anything new**, open an issue or PR on this repository.

---

## References

* [Security assessment](SECURITY.md) — full vulnerability catalogue and attack chain analysis
* [API reference](API.md) — every known endpoint
* [Company background](COMPANY.md) — why the vendor is not coming back
