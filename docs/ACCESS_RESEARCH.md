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
**Script:** [`forensics/ulo_probe.py`](../forensics/ulo_probe.py) - see [`forensics/`](../forensics/README.md) for the rest of the analysis scripts
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
