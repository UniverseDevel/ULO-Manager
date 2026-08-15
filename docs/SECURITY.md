# ULO Security Assessment

Consolidated security assessment of the ULO camera, covering both sides of the device:

* the **head firmware** shipped in `firmware/` (STM32 MCU driving camera, display, sensors and power)
* the **APQ/Android side** that hosts the HTTP API, the WebSocket streams and the WiFi configuration

| Scope                      | Detail                                                                                                                                     |
|----------------------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| **Firmware analysis date** | 2026-08-13                                                                                                                                 |
| **Device observations**    | 2026-08-13/14, firmware `06.0601`                                                                                                          |
| **Firmware method**        | Static binary analysis only — no device contacted, no code executed on hardware, no emulation                                              |
| **Device method**          | Read-only HTTP/WebSocket/TLS requests and a port scan against a live unit, the device's own system log, and a DNS capture from the network |
| **Tooling**                | Python 3.14, `capstone` disassembler, custom scripts kept outside the repository                                                           |

The individual findings are, taken alone, mostly unremarkable. The risk comes from how they **chain**.

> **Short version:** keep ULO off the internet, keep it off segments shared with untrusted devices, and
> assume anyone who can reach it on the network can watch the camera and read its recordings.

---

## 1. Summary of findings

### 1.1 Device-side (observed against live hardware)

| ID     | Issue                                                                           | Severity         | Status on `06.0601`                            |
|--------|---------------------------------------------------------------------------------|------------------|------------------------------------------------|
| **S1** | WiFi passwords stored in cleartext in the system log, surviving factory reset   | **High**         | Passwords fixed; **SSID history remains** §3.1 |
| **S2** | Recordings and snapshots served without any authentication                      | **High**         | **Confirmed, wider than first reported**       |
| **S3** | Live video WebSocket accepts anyone, with no token                              | **High**         | **Confirmed**                                  |
| **S4** | Cleartext, unauthenticated cloud update check to a released AWS IP              | **High**         | Endpoint gone, risk inherited by the FOTA path |
| **S5** | Continuous location-service chatter to Qualcomm                                 | Medium (privacy) | **Confirmed**                                  |
| **S6** | Device state readable without authentication                                    | Low–Medium       | **Confirmed**                                  |
| **S7** | Credentials and tokens exposed over plain HTTP; TLS present but unauthenticated | Medium           | **Confirmed**                                  |

### 1.2 Firmware (static analysis)

| ID     | Severity   | Finding                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
|--------|------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **F1** | **High**   | **No cryptographic authenticity.** Images carry a CRC-32 only (§4.2.3). CRC-32 is not a security primitive — a modified image can be trivially adjusted to reproduce any target checksum, so the check stops accidental corruption and nothing else. Anyone able to deliver an image to the head MCU (the network-facing APQ side being the natural pivot) can run arbitrary code on the MCU that drives the camera, display and motion sensing. **Remediation:** sign images (e.g. ECDSA P-256 over the image, signature in a trailer) and verify in the bootloader before flashing.                                                                                                                                                                                                                                                                       |
| **F2** | **Medium** | **No confidentiality.** Images are plaintext and unpacked (§4.2.1), so complete reverse engineering is possible — as demonstrated in §4.2.4 and §5. Alone this is an accepted trade-off; combined with F1 it materially lowers the cost of producing a *working* malicious image.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| **F3** | **Medium** | **No rollback protection.** The image format carries no version or monotonic counter the loader could enforce; the only version marker is the out-of-band `stmf_version` file (`1`, `1`, `8`). Older images are published in this repository, so anyone with the update path from F1 can also **downgrade** the device. **Remediation:** embed a monotonic security-version field, covered by the signature, and refuse anything lower than the value fused/stored on the device.                                                                                                                                                                                                                                                                                                                                                                           |
| **F4** | **Low**    | **No exploit mitigations** (§4.2.7): no stack canaries, MPU unused, no VTOR relocation. Any memory-safety bug in the APQ IPC parsing path would therefore be directly exploitable. **Remediation:** rebuild with `-fstack-protector-strong` and apply a basic MPU policy (XN on RAM, read-only on flash).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| **F5** | Info       | **No secrets in firmware — positive result.** Zero credentials, keys, certificates or SSIDs across all three images (§4.2.4).                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                               |
| **F6** | Info       | **Verbose diagnostics compiled in** — `assertion "%s" failed: file "%s", line %d`, source file names (`renderer.cpp`), demangled C++ symbols and detailed state logging. Minor information disclosure through any exposed console, and confirms release builds are not stripping asserts.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| **F7** | Info       | **Resource parser is sound** (§4.2.6) — magic, version and entry-count validation plus bounded string comparison. Worth preserving as the pattern for any new parser on the IPC path.                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| **F8** | Info       | **The cloud update endpoint appears in this repository, not in the firmware.** §4.2.5 proves the images are clean; the address is documented in `README.md` next to the `CheckVersionOnCloud` action (both the IPv4 and the resolved `…compute-1.amazonaws.com` name), committed in `b03d571`. **Accepted, deliberately retained:** the host is decommissioned and the entry is part of the documented API research, so it carries no live exposure. Redaction would in any case be cosmetic — the value persists in git history and in every existing clone, fork and cache of the public repository, and undoing that would require a history rewrite (`git filter-repo`), a force-push, fork clean-up and a GitHub cache purge. The residual *device-side* risk — a released EC2 elastic IP being re-allocated, combined with F1/F3 — is recorded as S4. |

### 1.3 Chained risks

| #  | Risk                                       | Sources                    | Likelihood                   | Impact        | Overall  |
|----|--------------------------------------------|----------------------------|------------------------------|---------------|----------|
| R1 | Persistent implant surviving factory reset | F1 + F2 + device-side auth | Low–Medium                   | **Critical**  | **High** |
| R2 | Hijacking the cleartext update check       | F1 + S4                    | **Medium–High**              | **Critical**  | **High** |
| R3 | WiFi credential theft from the log         | S1                         | **High** (affected versions) | High          | **High** |
| R4 | Unauthenticated surveillance of the owner  | S2 + S3                    | **High**                     | High          | **High** |
| R5 | Cheap weaponisation of a malicious image   | F2 + F6                    | High                         | Low (enabler) | Medium   |
| R6 | Firmware downgrade                         | F3                         | Low                          | Low           | **Low**  |
| R7 | Runtime exploitation of the IPC path       | F4                         | Unknown                      | Medium        | Medium   |

Likelihood assumes an attacker who has reached the local network. Nearly everything below is gated on
that, or on the device being exposed to the internet.

---

## 2. The platform underneath

What the device actually runs, established from evidence rather than assumption. The head firmware in
`firmware/` (§4) is only the STM32 display/sensor MCU; everything network-facing runs on a second,
much larger system, and this section is what could be determined about it **without opening the device
or running code on it** — only read-only network requests and the device's own log.

| Property               | Finding                                  | Evidence                                                                                                                                                         |
|------------------------|------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Operating system       | **Android**                              | The log records an app-private path `/data/user/0/lu.mudesign.ulo/files/…`. `/data/user/<n>/<package>/` is the Android per-user application data layout          |
| Android generation     | **4.2 or later**                         | The `/data/user/0/` multi-user path replaced `/data/data/` in Android 4.2                                                                                        |
| Application package    | `lu.mudesign.ulo`                        | Same log path. `lu` matches the vendor's Luxembourg registration                                                                                                 |
| Application framework  | **Qt** (C++, not Java)                   | Log symbols such as `Wifi::updateConfiguration(QJsonObject)` — `QJsonObject` is Qt                                                                               |
| SoC                    | **Qualcomm APQ**                         | Head firmware talks to a peer it calls the APQ throughout (`ManageApqWake`, `APQ image update`, §5.3); APQ is Qualcomm's applications-processor-only part number |
| SoC vendor services    | Qualcomm IZat / gpsOneXTRA               | Continuous `xtrapath{1,2,3}.izatcloud.net` lookups (§3.5)                                                                                                        |
| Android services alive | Connectivity + time                      | `connectivitycheck.gstatic.com` (captive-portal probe) and `2.android.pool.ntp.org` (default Android NTP pool) in DNS capture                                    |
| Hardware/software ODM  | **VVDN Technologies**                    | Log lines are prefixed `VVDN:` — e.g. `VVDN: wifi: Connected to network "…"`                                                                                     |
| Platform image age     | **not newer than 2017-01-20**            | The shipped TLS certificate was generated on that date (§3.7, archived in `firmware/06.0601/device/`)                                                            |
| Real-time clock        | **none, or unbacked**                    | Every boot the log restarts at `01/01/70 01:00:39` — the Unix epoch — until NTP corrects it                                                                      |
| Internal storage       | 1718 MB total                            | `GET /api/v1/files/stats`                                                                                                                                        |
| Web server             | **Mongoose/Civetweb family**, not Apache | See below                                                                                                                                                        |

**The web server is not Apache.** It sends **no `Server` header at all**, its 404 body is
`Error 404: Not Found` / `Not found`, and its directory index uses `?nd` / `?dd` / `?sd` sort links with
a `[DIRECTORY]` size marker and a `Parent directory` row. That combination is the signature of the
Mongoose/Civetweb family of embedded C servers rather than Apache, nginx or lighttpd. The exact product
and version cannot be pinned down further from the outside, because the banner is absent.

**The same server answers on four ports** — 80, 8080, 443 and 8443 — serving byte-identical content
(same `ETag`, same `Content-Length`). A port scan of the usual suspects found **nothing else open**: no
SSH (22), no Telnet (23), and importantly **no ADB (5555)**, so the obvious route to a shell is closed.

### 2.1 What this does not tell us

Everything above is inference from the outside. Not determined: the Android release number, the kernel
version, the SELinux mode, whether the device is rooted, the bootloader lock state, and the contents of
the Android partitions. Those need either a shell on the device or the APQ-side image, and **the
APQ-side image is not published by the vendor and is not in this repository** — only the head MCU
firmware is. That gap is why §4 and §5 cover only the MCU.

---

## 3. Device-side findings

Observed against a live unit. Everything in this section was re-checked on firmware `06.0601`; where a
finding could not be reproduced on that version it is marked accordingly rather than removed, because
older units remain in service and the original observations were made on `10.1308`.

### 3.1 S1 — WiFi credentials in the system log

`http://<ULO_IP>/logs/system.txt` contained the plaintext passwords of WiFi networks the device had
joined; the relevant lines are those containing `Wifi::updateConfiguration(QJsonObject)`. The log was
never rotated, so it kept them **forever — including across a factory reset**.

**Impact.** Anyone who reads that file learns the household WiFi password. A unit sold or discarded after
a factory reset still carries the previous owner's credentials.

**Status on `06.0601`.** Partly fixed. `/logs/system.txt`, `/logs/` and `/system.txt` all return
`404`, and the current log (`GET /api/v1/system/log`) contains no `Wifi::updateConfiguration`,
`password`, `psk` or `passphrase` pattern — **the passwords are gone**.

**What remains: the network names.** The log still records every network the device has joined, in
lines of the form `VVDN: wifi: Connected to network "<ssid>"` and
`Unexpectedly disconnected from network "<ssid>"`, and it is still never rotated. On the unit examined
the log went back far enough to include **the manufacturer's own factory provisioning SSID
(`uloprod01`)**, alongside every network the owner had ever used — evidence that this history survives
both normal use and whatever reset the unit received before shipping.

**Impact of the remainder.** SSIDs are not secrets in the way passwords are, but they are strong
identifiers: public wardriving databases map SSIDs to physical locations, so the list geolocates the
device's history, and it discloses every other network the owner connects to. It is readable by anyone
who can authenticate, and the log is not clearable through any documented endpoint.

Treat the password disclosure as fixed on this version and present on `10.1308` and earlier; treat the
SSID history as **present on all versions including `06.0601`**.

### 3.2 S2 — Recordings and snapshots are served to anyone

The media tree is exposed by a plain directory index that performs **no authentication at all**:

```
GET http://<ULO_IP>/media/                 -> 200, full directory listing
GET http://<ULO_IP>/media/loginPicture.jpg -> 200, a picture taken by the camera
```

This was originally reported as reachable "while someone else is logged in". On `06.0601` it is broader:
the index and the files answer with no credentials at all, and keep answering **after every session has
been logged out**. No session needs to exist.

**Impact.** Every recording and snapshot the camera has stored, plus the login background image, is
readable by anyone who can reach the device on the network.

**Contrast.** The JSON API is not affected — `/api/v1/files/media` correctly returns `401`. It is the
static file tree that is open.

### 3.3 S3 — Live video needs no authentication

The live stream (`ws://<ULO_IP>/api/v1/live`, sub-protocol `mudesign.ulo.mp4`) hands out video to anybody
who opens the socket. Measured on `06.0601`:

| Attempt                    | Result after 10 seconds                    |
|----------------------------|--------------------------------------------|
| No token frame sent at all | **14 fragments, 1 690 855 bytes of H.264** |
| Deliberately invalid token | **16 fragments, 1 701 038 bytes of H.264** |

The camera never challenges the client and never closes the socket. `rtsp://<ULO_IP>:8901/live` is
likewise unsecured.

**Impact.** Anyone on the network can watch and record whatever the camera sees — 1280x720 H.264 with
AAC audio, so this includes sound.

**Contrast.** The *event* WebSocket on `ws://<ULO_IP>/api/v1` does validate the token and answers an
invalid one with
`{"event":"failure","data":"Session does not exist, please authenticate again to continue!!"}`. The check
simply was not applied to the video channel.

### 3.4 S4 — The cloud update check

`http://<ULO_IP>/api/v1/interface/CheckVersionOnCloud` contacted `34.232.121.46`, which traces to
`ec2-34-232-121-46.compute-1.amazonaws.com` — a plain, unauthenticated AWS EC2 host over cleartext HTTP.
It is no longer active, so the update check simply fails, but the address is not owned by anyone in
particular either: a released EC2 elastic IP returns to Amazon's pool and can be re-allocated to any
other AWS customer. Combined with head firmware images carrying only a CRC-32 and no signature (F1/F3),
whoever ends up holding that IP could answer the update check.

**Impact.** An attacker who answers the update check controls what the camera believes is the current
firmware and where it comes from. Combined with F1 (images carry a CRC-32 and no signature) that is a
path to running attacker-chosen code on the device — the chain is set out as R2 in §6.2.

* **An attacker does not need that IP.** The request is plain HTTP with no TLS, so anyone on the same
  network can ARP-spoof, DNS-spoof or otherwise redirect it and answer it themselves. Internet isolation
  alone does not cover this — the device must also be kept off segments shared with untrusted devices.
* **This is not in the head firmware.** The endpoint was verified absent from all three images in plain
  text, UTF-16, packed IPv4, base64 and single-byte XOR forms (§4.2.5). It lives purely on the
  APQ/Android side.
* **Status on `06.0601`.** `CheckVersionOnCloud` is gone (`404`). The update path is now
  `interface/fotaStatus`, `fotaNumberOfUpdates`, `fotaIsInstallAvailable`, `fotaStartDownload` and
  `fotaInstallFirmware`. Those are all *local* reads of state the device already holds; none makes the
  camera ask a server, so the exposure now sits in whatever `fotaStartDownload` contacts.
* **Hunting the traffic.** DNS logs will not show it. A DNS capture of the camera resolved no vendor, AWS
  or update-related name at all, which is expected when the target is a hard-coded IP: no name is ever
  looked up. Router or firewall connection logs, a port mirror or an inline capture are required instead.

### 3.5 S5 — Continuous location-service chatter to Qualcomm

The camera keeps contacting Qualcomm's assisted-GNSS infrastructure. A DNS capture over a **4.8 hour**
window shows it asking for `xtrapath1.izatcloud.net`, `xtrapath2.izatcloud.net` and
`xtrapath3.izatcloud.net` **52 times each — 156 lookups, roughly one every 5.6 minutes, every single one
blocked and immediately retried**. Those three names are the only third-party destinations it wants,
besides NTP and Google's connectivity check.

`izatcloud.net` is Qualcomm's IZat / gpsOneXTRA service, from which Qualcomm chipsets download GPS
ephemeris data (`xtra*.bin`), historically over cleartext HTTP.

**Impact.** ULO is a mains-powered indoor camera with no navigation feature and no reason to want GPS
assistance data, yet it asks relentlessly and never gives up when the request fails. Each successful
fetch would disclose the household's public IP address and a recurring timing pattern to a third party
unrelated to the product. This is platform chatter from the Android/Qualcomm side rather than vendor
behaviour.

Older Qualcomm XTRA clients have been *reported* to send device identifiers alongside the download. That
was **not** verified on this device — treat it as unconfirmed. What is confirmed is the persistent
outbound contact itself.

**Also seen in the same capture,** neither alarming but worth knowing: `2.android.pool.ntp.org` (32
lookups — the clock genuinely needs this, it resets to `01/01/70` on every boot) and
`connectivitycheck.gstatic.com` (28 lookups, Android's captive-portal probe).

### 3.6 S6 — Device state readable without authentication

`GET http://<ULO_IP>/api/v1/state` answers without any token, disclosing battery level, whether the
camera is on mains power, whether an administrator account exists, and the configuration flag that
reveals whether the camera is currently upside down in setup mode.

**Impact.** Minor alone, but a free presence and occupancy signal: it tells anyone on the network that a
ULO is present, whether somebody is currently handling it, and whether it is running on battery.

### 3.7 S7 — Credentials and tokens travel in the clear

The API is served over plain HTTP on ports **80** and **8080**, and this is what every known client
uses. Login sends the account name and password as HTTP Basic
(`Authorization: Basic <base64>`), and every later call carries the session token as a bearer header, so
anyone able to observe the traffic gets both. In addition, `GET /api/v1/users` returns the **push
notification tokens of every paired phone** to any authenticated caller.

**TLS exists but authenticates nothing.** The same server also answers on **443** and **8443**, and the
full API works there — verified by logging in over HTTPS on `06.0601`. It is not a fix, for four
reasons, each confirmed against the device:

| Property    | Observed                                       | Consequence                                                                            |
|-------------|------------------------------------------------|----------------------------------------------------------------------------------------|
| Certificate | Self-signed, `CN=localhost`, no subjectAltName | No client can validate it; browsers reject it outright                                 |
| Generated   | 2017-01-20, fixed 10-year life                 | Baked into the platform image, not per device — so the private key ships on every unit |
| Signature   | SHA-1                                          | Deprecated for signatures                                                              |
| Protocol    | TLS 1.0 and 1.1 accepted, 1.3 absent           | Deprecated versions still negotiable (RFC 8996)                                        |

Because the certificate is shipped rather than generated per device, whoever extracts the key from any
one unit can impersonate every other unit, so TLS here defeats a *passive* observer and not an active
one. The certificate is archived with its analysis in
[`firmware/06.0601/device/`](../firmware/06.0601/device/README.md).

**Impact.** Anyone able to observe the traffic — on the same wireless network, or anywhere along the
path if the device is exposed — recovers the account password and the session token, and with them full
control of the camera. The push tokens additionally allow notifications to be spoofed to the owner's
phone.

**Remediation:** owner-side, prefer HTTPS with the certificate pinned — `ulo --https --pin-cert <sha1>`
does this — which removes the passive-capture exposure. It cannot remove the active-attacker exposure
while the key is shared. Vendor-side this needed a per-device key and a vendor-controlled name.

A related operational quirk: the camera keeps **one session per account**. Logging in again with the same
account silently invalidates the previous session, so an attacker holding one set of credentials can
repeatedly evict the legitimate owner.

---

## 4. Firmware analysis

Static analysis of the head firmware images in `firmware/`. This section is a process log — what was
tried, how, and what came out, including the checks that came back clean.

### 4.1 Image inventory

| Version dir | `stmf_version` | Image                              | Size      | SHA-256 (first 16) |
|-------------|----------------|------------------------------------|-----------|--------------------|
| `06.0601`   | 1              | `ulo-head-v2.1.2-UserV1-CRC32.bin` | 393 216 B | `06922CE59C296BDD` |
| `08.0701`   | 1              | `ulo-head-v2.1.3-UserV3-CRC32.bin` | 393 216 B | `1B697CC3A5DECDC4` |
| `10.1308`   | 8              | `ulo-head-v2.1.8-UserV8-CRC32.bin` | 393 216 B | `B2C8970069C727D3` |

All three are exactly 384 KiB — a fixed-size flash slot, tail-padded with `0xFF`.

### 4.2 Steps performed and results

#### 4.2.1 Entropy and layout

Total entropy 6.34 / 6.34 / 6.39 bits per byte. A 16-block entropy map is flat at ~6.9–7.1 for the first
~80 % of each image and `0.00` for the final ~19 %. `0xFF` fill: 20.6 / 20.6 / 19.7 %; `0x00`: 8.3 % in
all three.

> **Result:** the images are **plain, unencrypted, uncompressed code**, tail-padded to the slot size.
> Nothing is packed or obfuscated.

#### 4.2.2 Architecture and load address

Initial `SP = 0x20000400`; reset vector `= 0x08020201` (Thumb bit set → entry at `0x08020200`); 122
vector-table entries point inside the image; `vfma.f32` / `vcvt.f32.s32` instructions are present.

> **Result:** ARM **Cortex-M4F**, image base **`0x08020000`**. The first 128 KiB of flash — the
> **bootloader** — is *not* part of these images and is not in this repository.

#### 4.2.3 Integrity mechanism

The trailing 4 bytes were tested against zlib CRC-32 (raw, inverted, whole-image, padding-stripped) and
against the STM32 hardware CRC unit (poly `0x04C11DB7`, init `0xFFFFFFFF`, word-wise, non-reflected):

| Candidate                          | v2.1.2             | v2.1.3             | v2.1.8             |
|------------------------------------|--------------------|--------------------|--------------------|
| zlib `crc32(image[:-4])`           | `0xa25d969e`       | `0xd2270293`       | `0x6ad134c6`       |
| zlib inverted                      | `0x5da26961`       | `0x2dd8fd6c`       | `0x952ecb39`       |
| **STM32 HW CRC over `image[:-4]`** | **`0xf2c86d40`** ✅ | **`0xf9059517`** ✅ | **`0xad37cb3d`** ✅ |
| Trailing 4 bytes (LE)              | `0xf2c86d40`       | `0xf9059517`       | `0xad37cb3d`       |

> **Result:** the `-CRC32` filename suffix is literal — the last word is an **STM32 hardware CRC-32 over
> the whole image minus those 4 bytes**, matching exactly on all three images. This is the **only**
> integrity value present. There is **no signature, no MAC, no header, and no version or anti-rollback
> field**.

#### 4.2.4 String extraction and secret hunting

1 815 ASCII strings (≥5 chars) in v2.1.8, swept for `pass / pwd / key / secret / token / auth / admin /
root / login / ssid / wifi / wpa / psk / http / ftp / :// / cert / -----BEGIN / priv / aes / rsa / sha /
md5 / hmac / backdoor / debug / ota / firmware …`.

> **Result: no credentials, no keys, no certificates, no SSIDs, no URLs, no network endpoints.**
> The only `http` hit is the GCC bug-report URL inside a libstdc++ diagnostic; the only `sha` hit is the
> string `Ulo shaking`; the `key` hits are `Key Pressed` / `Key Released` / `global constructors keyed to`.

The application strings identify the true role of this MCU — it is the **head controller**, not the
network stack: display/animation engine, sensors and I/O, power and battery, IPC with the APQ, and
camera/stream signalling (enumerated in §5.3).

> **Important scoping consequence:** the HTTP API, the WebSocket video stream and the WiFi configuration
> handling — everything in §3 — live on the **APQ/Android side**, which is **not** in this repository.
> This firmware only *signals* update and factory-reset events; it does not implement them, and it never
> touches the network.

#### 4.2.5 Targeted hunt for the cloud update endpoint

Because the update path is the sensitive asset, a dedicated sweep was run for the vendor's cloud update
host across all three images, going beyond plain ASCII strings:

| Probe                                                       | v2.1.2 | v2.1.3 | v2.1.8 |
|-------------------------------------------------------------|--------|--------|--------|
| Hostname in ASCII (`…compute-1.amazonaws.com`)              | absent | absent | absent |
| Substrings `amazon` / `amazonaws` / `compute-1` (ASCII)     | absent | absent | absent |
| Substring `amazon` in UTF-16LE                              | absent | absent | absent |
| Update-host IPv4 in dotted ASCII                            | absent | absent | absent |
| Update-host IPv4 packed, big-endian and little-endian       | absent | absent | absent |
| Base64 of the hostname and of the dotted IPv4               | absent | absent | absent |
| `amazonaws` XOR-obfuscated, all 255 single-byte keys        | absent | absent | absent |
| `http` XOR-obfuscated, all 255 single-byte keys             | absent | absent | absent |
| `CheckVersionOnCloud` (the APQ API action that triggers it) | absent | absent | absent |
| Any DNS-shaped hostname or any dotted-quad IPv4 literal     | absent | absent | absent |

> **Result: the head firmware contains no trace of the Amazon update endpoint in any form.** This is the
> expected outcome of the architecture established in §4.2.4 — the MCU has no network interface and only
> *signals* update state to and from the APQ. The endpoint is resolved and contacted exclusively on the
> APQ/Android side. Publishing these three images therefore does **not** disclose it. The endpoint is
> recorded in this repository's `README.md` as part of the documented API — see F8.

#### 4.2.6 Resource parser review

The most attractive target in this image is the animation resource parser, found via the literal-pool
reference to `Invalid animation sector ! (sector=%d; magicNb=%u; entriesNb=%u)` (string at `0x08066c30`,
referenced from `0x08038964`). The containing function at `0x080388b0` was disassembled and analysed.

What it does: iterates 4 sectors; checks **two magic words**; requires `version == 1`
(`ldrh r2,[r5,#8]; cmp r2,#1`); **rejects `entriesNb > 0x40`** (`ldrh r3,[r5,#0xa]; cmp r3,#0x40; bhi`);
walks fixed 0x19-byte entries with a correctly computed end pointer (`r3 = entriesNb * 25 + table_base`);
and compares entry names with a **length-bounded 15-byte** comparison (`movs r2, #0xf; bl 0x8059950`).

> **Result: the parser is defensively written** — magic, version and count are all validated before use
> and the name comparison is bounded. The only unchecked value is a 16-bit entry offset added to the
> sector base without an explicit end-of-sector assertion; it is bounded to +64 KiB and is not reachable
> from any externally exposed interface identified in this image. **Not reported as a finding.**

#### 4.2.7 Mitigation and capability constant scan (all three images)

| Probe                                                 | Result     | Meaning                                                                                            |
|-------------------------------------------------------|------------|----------------------------------------------------------------------------------------------------|
| `FLASH_KEY1` `0x45670123` / `FLASH_KEY2` `0xCDEF89AB` | **absent** | the application never unlocks flash — it **cannot self-program**; flashing is the bootloader's job |
| `FLASH_OPTKEY1/2`                                     | **absent** | never modifies option bytes (RDP is not set from here)                                             |
| `MPU_TYPE` / `MPU_CTRL` (`0xE000ED90/94`)             | **absent** | **MPU never configured** — no W^X, no region protection                                            |
| `SCB_VTOR` (`0xE000ED08`)                             | **absent** | no vector-table relocation                                                                         |
| `__stack_chk_fail` / `__stack_chk_guard`              | **absent** | compiled **without stack protector**                                                               |
| AES S-box / SHA-256 K-table / MD5 T-table             | **absent** | no software crypto of any kind                                                                     |
| `CRYP` / `HASH` / `RNG` peripheral bases              | **absent** | STM32 crypto and RNG peripherals unused                                                            |
| `FLASH_R_BASE` (`0x40023C00`)                         | present ×1 | flash latency/ACR configuration only                                                               |

#### 4.2.8 Cross-version diff

| Comparison      | Differing bytes  | First difference | String-set delta                                             |
|-----------------|------------------|------------------|--------------------------------------------------------------|
| v2.1.2 → v2.1.3 | 225 352 (57.3 %) | `0x0001cc`       | 841 → 840 strings; **1 added, 2 removed — all binary noise** |
| v2.1.3 → v2.1.8 | 247 301 (62.9 %) | `0x0001c4`       | 840 → 868 strings; genuine feature additions                 |
| v2.1.2 → v2.1.8 | 247 300 (62.9 %) | `0x0001c4`       | —                                                            |

> **Result:** the large byte deltas are dominated by full-recompile code-layout shifts, not by scale of
> change. **v2.1.2 → v2.1.3 is functionally near-identical**, consistent with both carrying
> `stmf_version = 1`. **v2.1.8** adds real functionality (§5.6). No security-relevant string appears or
> disappears in any version.

### 4.3 Limitations of the firmware analysis

* **The bootloader is not in this repository** (`0x08000000`–`0x0801FFFF`). The code that actually
  *validates and applies* an update could not be audited. F1 is stated on the basis that the shipped
  image format contains no signature for a bootloader to verify — if the bootloader performs additional
  out-of-band verification, that would need confirming against the bootloader binary.
* **Readout protection (RDP)** and other option bytes live outside the image and cannot be read from
  these files. Whether flash readback is blocked on production hardware is **unknown** — and §4.2.7
  confirms the application firmware never sets it itself.
* **No dynamic analysis**: no emulation, no fuzzing of the APQ IPC protocol, no hardware or JTAG/SWD
  testing. The IPC message-dispatch path was only partially reversed; a fuzzing campaign against it is
  the obvious next step, especially given F4.

---

## 5. Firmware content inventory

Finding F2 (images are plaintext, unencrypted and uncompressed) means the full content is directly
enumerable with static tooling — no keys, no unpacking, no hardware.

### 5.1 What is identifiable, and how

| Content class                 | Identifiable?       | Method                                                              |
|-------------------------------|---------------------|---------------------------------------------------------------------|
| Image layout / region map     | ✅ exact             | vector-table walk + `0xFF` tail-padding scan                        |
| Integrity value               | ✅ exact             | STM32 hardware CRC-32 recomputation (verifies on all three images)  |
| CPU, load address, entry      | ✅ exact             | initial SP / reset vector, FPU instructions, vector-table targets   |
| Function inventory            | ⚠️ lower bound      | decode of all Thumb `BL`/`BLX` targets                              |
| Subsystems & feature set      | ✅ high confidence   | classified log/format strings                                       |
| Animation catalogue           | ✅ complete          | lowercase name tokens in the animation rodata block                 |
| Event & state enumerations    | ✅ complete          | log strings emitted per enum case                                   |
| APQ IPC message names         | ⚠️ partial          | only messages that appear in log text                               |
| Function names / full symbols | ❌ stripped          | only 4 C++ signatures survive, via `assert()` `__PRETTY_FUNCTION__` |
| Animation pixel/vector data   | ❌ not present       | lives in separate flash sectors, not in these images                |
| Bootloader                    | ❌ not in repository | occupies `0x08000000`–`0x0801FFFF`                                  |

### 5.2 Region map

Load address `0x08020000` (ARM Cortex-M4F), initial `SP = 0x20000400`, entry `0x08020200` in all images.

| Region                      | Address range             | v2.1.2            | v2.1.3            | v2.1.8            |
|-----------------------------|---------------------------|-------------------|-------------------|-------------------|
| Vector table (122 handlers) | `0x08020000`–`0x080201EC` | 492 B             | 492 B             | 492 B             |
| Code + rodata               | `0x080201EC`–(see below)  | 315 440 B         | 315 496 B         | 319 160 B         |
| End of used flash           | —                         | `0x0806D21C`      | `0x0806D254`      | `0x0806E0A4`      |
| `0xFF` tail padding         | up to `0x0807FFFC`        | 77 280 B (19.7 %) | 77 224 B (19.6 %) | 73 560 B (18.7 %) |
| STM32 CRC-32 trailer        | last 4 B                  | `0xF2C86D40` ✅    | `0xF9059517` ✅    | `0xAD37CB3D` ✅    |

Internal rodata blocks (v2.1.8 addresses):

| Block                                     | Range                     |
|-------------------------------------------|---------------------------|
| Application text / log strings            | `0x08065920`–`0x08066B10` |
| Animation name table                      | `0x080661C8`–`0x08066920` |
| Assert / `__PRETTY_FUNCTION__` signatures | `0x08066C30`–`0x08066DB0` |
| libstdc++ / C++ ABI, RTTI, demangler      | `0x08066DE0`–`0x0806A800` |
| C library locale & time formats           | `0x0806A800`–`0x0806AC00` |

| Metric                                       | v2.1.2 | v2.1.3 | v2.1.8 |
|----------------------------------------------|--------|--------|--------|
| Distinct `BL` targets (function lower bound) | 835    | 835    | 841    |
| Raw ASCII strings (≥4 chars)                 | 3 514  | 3 522  | 3 547  |
| Application-level strings (noise filtered)   | 315    | 315    | 346    |

No RTOS banner is present (`FreeRTOS`, `ChibiOS`, `CMSIS`, SEGGER RTT all absent); the only task-like
labels are `idle` and `main`.

### 5.3 Identified subsystems

All of the following come from log and format strings in the application rodata block.

**Animation & rendering** — `Renderer::pushTransform(const Transform&)`, `Renderer::popTransform()`,
`Renderer::drawSpline(const PointF*, std::size_t, const PointF*, Color)`, source file `renderer.cpp`,
asserts `m_transformStack.size() >= 2` / `>= 1` / `count >= 2`, `colorHSVtoRGB`, `AE_PlayAnimation`,
`playAnimRandomly`, `updateDemoModeState`, `Animation not found !`, `Animation already to be played !`,
`Invalid animation sector ! (sector; magicNb; entriesNb)`, `ERROR: LCD initialization failed`.

**Sensors & user input** — `ERROR: accelerometer initialization failed`, `Error ADC %u`, tap/key events
(§5.4), `Ulo shaking`, `Ulo went upside up` / `upside down`, `Orientation up` / `down`,
`Ulo displacement detected...` / `but filtered !!!` / `can be detected again...`,
`Fire displacement event`, `Awake due to movement (pos x; y)`, `notifyOrientation`.

**Camera & streaming** — `ERROR: camera initialization failed`, `Camera initialization succeed`,
`Snapshot not ready !`, `VideoStreamingStarted`, `VideoStreamingStopped`, `VideoSnapshot`,
`exclusionArea - top; left; bottom; right; reverse` (+ its `invalid!` variant),
`Ulo in/out Alert mode`, `Ulo in/out Spy mode`.

**Power, battery & sleep** — `Battery Low - vAvg; vSum; count`, `Battery low`, `Battery full`,
`Battery charging`, `USB plugged` / `unplugged` / `USB abnormal power!`, `Go to standby mode`,
`updateGoAsleepState 4 - software update ongoing/stucked/timeouted`,
`ManageApqWake - wake up APQ begin/continue/done`, `onWakeupState - stayAwake`,
`Awake in Standard/Alert/Spy/unknown mode`, `Unhandled awake event! (id)`,
`Service timer - elapsed / APQ awaking up / need to awake APQ / no need to awake APQ (2)`,
`onSetServiceTimer - enabled; interval`, `onSetWorkingOptions - apqSleepDisabled`.

**APQ IPC & update signalling** — `Messages::Heartbeat (elapsed; min; max)`, `Messages::SetPtsMode`,
`updateAPQPowerState` (6 variants: power-off-while-powering-up, software update ongoing, powered off,
APK update done, APQ image update done, APQ dead), `ACK for SleepRequest timeouted !!!`,
`ACK for AwakeRequest timeouted !!!`, `APQ sleep mode canceled !!!`, `APQ is crashed !!!`,
`APQ is not active or crashed !!!`, `APQ Image Update`, `APK Application Update`,
`APK Settings Factory Reset`, `Notify factory reset to APK...`, `APK settings factory reset is done`,
`APQ image update done`, `APK app update should be done`, `Software Update Done (done)`,
`Awake due to software update (awake)`, `APQ power switch` / `power off` / `image installing`,
`Waiting core activity` / `Core active` / `Core crash`.

**Diagnostics** — `===== Hello =====` (boot banner), `assertion "%s" failed: file "%s", line %d`,
`%s.%03lu - %s` (timestamped log line format), `Last commands (size=%d):`,
`Pure virtual function call.`, `DMA failure`, `(null)`, `Unrealized`.

### 5.4 Enumerations recovered

| Enumeration        | Values (in image order)                                                                                                                                                 |
|--------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Input events       | `Single Tap`, `Double Tap`, `Slide`, `Long Tap`, `Very Long Tap`, `Key Pressed`, `Key Released`, `Unknown Key Event`                                                    |
| Working modes      | `Standard`, `Alert`, `Spy`, `unknown`                                                                                                                                   |
| Battery/USB events | `Battery low`, `Battery full`, `USB plugged`, `USB unplugged`, `USB abnormal power!`, `Battery charging`, `Battery unknown event`                                       |
| APQ states         | `APQ in unkwown state` *(sic)*, `APQ power switch`, `APQ power off`, `APQ image installing`, `Waiting core activity`, `Core active`, `Core crash`, `Base unknown event` |
| Update events      | `APQ Image Update`, `APK Application Update`, `APK Settings Factory Reset`, `Software Update Done`                                                                      |
| Motion events      | `Ulo went upside up`, `Ulo went upside down`, `Orientation up`, `Orientation down`, `Ulo shaking`, displacement detected/filtered                                       |

### 5.5 Animation catalogue

Names only — the animation **assets** are not in these images; the firmware reads them from separate
flash sectors validated by the parser at `0x080388B0` (§4.2.6). The sentinel `none` precedes the table.

**v2.1.2 and v2.1.3 — 29 animations (identical sets):**

```
blinks        amazed        waitingloop   agitated      proud         attentive
2blinks       wakeslong     wakesshort    squint        caress        charged
abnormalpower recharging    tired         happy         cries         waiting
upsidedown    dizzy         inalertmode   outalertmode  inspymode     outspymode
nervous       updating      squint2       snapshot      asleepshort
```

**v2.1.8 — 44 animations; 15 added, none removed:**

```
bored         circles       cogitates     crazy         embarassed    grumpy
inlove        laughing      puzzled       rectangle     scared        surprised
triangle      upset         winks
```

### 5.6 Cross-version content delta

Application-level string sets (literal-pool noise filtered out):

| Comparison      | App strings added | Removed | Animations added |
|-----------------|-------------------|---------|------------------|
| v2.1.2 → v2.1.3 | **0**             | **0**   | 0                |
| v2.1.3 → v2.1.8 | **31**            | **0**   | 15               |

> **v2.1.2 → v2.1.3 is a pure rebuild** at the observable-content level — zero application-string change,
> consistent with both carrying `stmf_version = 1`. The ~57 % byte delta in §4.2.8 is code-layout shift,
> not functional change.

**v2.1.8 (`stmf_version = 8`) adds** — beyond the 15 animations: a service timer, wake and sleep policy
control, battery-low averaging, `HSV→RGB` colour conversion, random animation playback, a new APQ power
state (`APQ powered off`) and shake detection. No security-relevant string is added or removed —
consistent with F1 and F5.

### 5.7 Limits of the listing

* **Function names are stripped.** The 835–841 `BL` targets are addresses only; the sole recovered
  signatures come from `__PRETTY_FUNCTION__` literals kept alive by `assert()` (F6).
* **The `BL`-target count is a lower bound** — functions reached only indirectly (vtables, function
  pointers, the IPC dispatch table) are not counted.
* **Animation assets, fonts and bitmaps are absent** from these images; only the loader and the name
  table are present.
* **The APQ IPC wire format is not enumerated.** Only message names appearing in log text are known;
  recovering the full message set requires reversing the dispatch table.
* **The bootloader is not in this repository**, so the update/validation path cannot be inventoried.

---

## 6. Risk assessment

Interpretation of the findings above: how they chain into concrete scenarios. Nothing here has been
demonstrated against hardware — attack chains are described to justify prioritisation, not as verified
exploits. The summary table is in §1.3.

### 6.1 R1 — Persistent implant that survives factory reset

**Chain:** F1 (CRC-32 only) + F2 (plaintext images) + unauthenticated device-side access.

CRC-32 is linear and invertible: an attacker can modify the image however they like and then patch any
4 bytes anywhere in it to force the checksum back to the expected value. It detects bit-rot and nothing
else. Anyone able to deliver an image to the head MCU can therefore run arbitrary code on it.

Why this is worse than a typical MCU compromise:

* The head MCU drives the **camera, display, accelerometer** and the **APQ's power and wake lines**
  (§5.3).
* The MCU is what *notifies* the Android side of a factory reset (`Notify factory reset to APK...`,
  `APK Settings Factory Reset`) — it is not itself reset by one. **An implant here survives factory
  reset, and survives re-flashing the Android side entirely.**
* It sits below anything a user — or a forensic examiner working from the Android side — would normally
  inspect.

The natural delivery route is the APQ/Android side, or the hijacked update check of R2.

> **What limits this.** §4.2.7 found `FLASH_KEY1`/`FLASH_KEY2` **absent** — the application cannot
> self-program flash. A runtime memory-corruption exploit over the IPC path is therefore RAM-only and
> dies at the next reboot (that is R7, not R1). Persistence specifically requires the **bootloader's**
> update path, and the bootloader is not in this repository and was not audited. F1 is stated on format
> grounds: the image carries nothing that a bootloader *could* verify, even if it wanted to.

**Remediation:** sign images (e.g. ECDSA P-256, signature in a trailer) and verify in the bootloader
before flashing.

### 6.2 R2 — Hijacking the cleartext update check

`CheckVersionOnCloud` contacted `34.232.121.46` over cleartext HTTP with no authentication. There is no
TLS, so there is no certificate to validate and nothing pins the response to the real vendor. R2 is
therefore not an independent risk so much as a **delivery vector for R1**: R2 gets an attacker-chosen
image onto the device, and R1 (no signature, CRC-32 only) is what makes the device accept it. Both ends
of the trust chain — server authenticity and payload authenticity — are absent.

**Re-allocation of the elastic IP (remote).** `ec2-34-232-121-46.compute-1.amazonaws.com` is not a name
anyone registers — Amazon derives it mechanically from the IP address. **Whoever is allocated that IP
automatically owns that hostname.** A released elastic IP returns to Amazon's pool and can be handed to
any other AWS customer. Likelihood alone is low — an attacker would have to cycle EC2 allocations to land
that specific address — but the cost is a few dollars, the payoff is total, and the risk never expires.

**Redirecting the request on the LAN (the cheaper path).** An attacker on the same network does not need
to win the IP lottery. ARP spoofing, a rogue DHCP server, DNS spoofing, a rogue AP or a compromised
router is enough to route the request to an attacker-controlled host, which answers with its own version
manifest and image. Over cleartext HTTP the device cannot tell the difference. This costs one `arpspoof`
invocation, is gated on nothing but LAN access — the same precondition as R3 and R4 — and is why R2's
likelihood is **Medium–High**. The update host being decommissioned does not help: the device still
*makes* the request, so there is still a request to hijack.

**Two bounds.** `CheckVersionOnCloud` lives purely on the APQ/Android side and is verified absent from
all three head images (§4.2.5 / F8), so what this path directly establishes is APQ compromise; reaching
the head MCU still goes via the APQ→MCU update path. And persistence on the MCU remains gated on the
unaudited bootloader, exactly as in R1.

**Remediation:** no vendor-side fix is available. Owner-side, isolate the device (§7) — isolation must
exclude untrusted *LAN* peers, not merely internet access. Vendor-side this would have been HTTPS with a
pinned certificate against a vendor-controlled DNS name, on top of signed images.

### 6.3 R3 — WiFi credential theft

S1: the log is readable without authentication, contains plaintext WiFi passwords, is never rotated and
survives factory reset. This requires no exploitation whatsoever — it is a single HTTP GET.

Concrete scenario: buy a used ULO, read the previous owner's WiFi password out of the log. Given typical
password reuse, that is frequently a pivot into their entire home network. Anyone who sells, returns or
discards one of these units is leaking credentials, and the factory reset they almost certainly performed
first did not help.

**Version applicability.** Observed on `10.1308` and earlier; **not reproducible on `06.0601`** (§3.1).
The risk stands for affected units — and second-hand units are exactly the scenario above — but a unit
running `06.0601` is not exposed through this path.

**Remediation:** wipe the log and rotate the WiFi password *before* the device leaves your hands.

### 6.4 R4 — Surveillance inversion

The WebSocket video stream is unauthenticated (S3), `rtsp://<ULO_IP>:8901/live` is unsecured, and the
`/media/` tree serves stored recordings to anyone (S2) — on a device that ships `Spy mode`, `Alert mode`,
snapshot capture and displacement detection (§5.3).

Measured on `06.0601`: opening the live socket with **no token frame at all** yielded 14 fragments
totalling 1 690 855 bytes of H.264 within ten seconds, and the camera neither challenged nor closed the
socket.

A camera bought for security becomes a camera pointed *at* its owner, viewable and recordable by anyone
on the local network with no credentials at all.

**Remediation:** none available to the owner beyond isolating the segment (§7.1) — the checks are absent
on the device and cannot be enabled. Vendor-side, the live socket and the `/media/` tree need the same
token validation the event socket already performs.

### 6.5 R5 — Weaponisation is cheap

Section §5 is itself the demonstration: exact load address `0x08020000`, entry point, full region map,
835–841 function entry points, complete subsystem and enumeration inventory, and a working CRC
recomputation — all recovered from plaintext images with no keys, no unpacking and no hardware. F6 adds
source filenames and C++ signatures preserved by `assert()`.

On its own this is an accepted trade-off for an embedded product. Sitting next to F1, it drops the cost
of producing a *working* malicious image from weeks to days.

**Remediation:** not worth addressing directly — obscurity is not a control. Fixing F1 removes the value
of the knowledge, which is why signing is the priority rather than encrypting or stripping the images.

### 6.6 R6 — Downgrade

Real in principle — the image format carries no version or monotonic counter, nothing enforces
anti-rollback, and older images are published in this repository.

But the cross-version diff found **zero** security-relevant strings added or removed across all three
versions, and v2.1.2 → v2.1.3 is a pure rebuild at the observable-content level (§5.6). There is no known
security fix to roll back *past*.

**Assessment:** treat F3 as a structural gap that matters for any *future* update, not as an exploitable
path today.

**Remediation:** embed a monotonic security-version field covered by the image signature, and refuse
anything lower than the value stored on the device (as per F3).

### 6.7 R7 — Runtime exploitation of the APQ IPC path

F4 records no stack canaries, an unconfigured MPU (so no W^X and no region protection) and no VTOR
relocation. Any memory-safety bug on the IPC parsing path would therefore be directly exploitable, with
none of the usual speed bumps.

Two things bound this:

* **No such bug is known.** The one parser reviewed in depth — the animation resource parser — is
  defensively written (F7).
* **The IPC dispatch table was never reversed**, so the actual attack surface is unmeasured. This is the
  single largest unknown in the whole assessment.

Because the application cannot self-flash, success here yields RAM-only code execution lost at reboot —
serious, but not persistence. Persistence is R1.

**Remediation:** rebuild with `-fstack-protector-strong` and apply a basic MPU policy (XN on RAM,
read-only on flash), per F4. Reversing and fuzzing the IPC dispatch table is the prerequisite for
measuring this properly (§7.2).

### 6.8 What limits the blast radius

* **F5 — no secrets in the firmware.** No keys, certificates, credentials or SSIDs in any of the three
  images. Extracting all of them yields nothing reusable against any *other* device. There is no
  fleet-wide key to steal, which is the difference between "one compromised device" and "compromised
  product line".
* **F7 — the reviewed parser is sound**, and is a good template for any new parser on the IPC path.
* **The MCU has no network stack.** It cannot be attacked directly from the network; everything must
  transit the APQ first.
* **The application cannot self-program flash** (§4.2.7), which blunts runtime exploits into
  non-persistent ones.
* **Almost everything requires LAN access** or an internet-exposed device.

---

## 7. Recommendations

### 7.1 For owners

1. **Keep ULO off the internet**, on an isolated VLAN or guest network. This single measure defeats R2's
   remote variant and blunts R1 and R4.
2. **Do not share that segment with untrusted devices.** Internet isolation alone does not stop R2's LAN
   variant — an attacker on the same network can answer the update check without any internet
   involvement. Where the segment must be shared, enable dynamic ARP inspection and DHCP snooping on
   managed switches, and block outbound `34.232.121.46` / `*.compute-1.amazonaws.com` at the router.
3. **Assume the camera view and its recordings are public** to anyone on that segment (S2, S3, R4), and
   position the device accordingly.
4. **Block `*.izatcloud.net`** at DNS and, ideally, egress at the router (S5). Nothing on the device
   depends on it.
5. **Wipe `/logs/system.txt` and rotate the WiFi password before selling or disposing of a unit** on
   affected firmware (S1, R3) — factory reset does not do this for you.
6. **Treat any second-hand unit's head firmware as untrustworthy.** It cannot be verified from the
   Android side, and a factory reset does not clear it.
7. **Use a dedicated account** for tooling rather than a personal one, so a session eviction (S7) does
   not lock the owner out of the app.
8. **Prefer HTTPS with a pinned certificate** for any tooling — `ulo --https --pin-cert <sha1>` — which
   keeps the password and session token off the wire in clear (S7). It does not help against an
   attacker who already holds the shipped private key, so it supplements isolation rather than
   replacing it.
9. **Assume the network names you have joined are recorded permanently.** The log keeps every SSID the
   device has connected to, is never rotated and cannot be cleared through the API (S1). Consider this
   before joining the camera to a network whose name you would not want disclosed, and before passing
   the unit on.

### 7.2 For anyone continuing this research

1. **Audit the bootloader** (`0x08000000`–`0x0801FFFF`, not in this repository). R1 hinges entirely on
   whether it performs out-of-band verification that the image format cannot express. This is the biggest
   open question in the assessment.
2. **Fuzz the APQ IPC dispatch path**, given F4 — it is the only meaningful remote-ish attack surface on
   the MCU and it is currently unmeasured.
3. **Confirm readout protection (RDP)** on production hardware. It lives in option bytes, outside these
   images, and §4.2.7 confirms the application never sets it itself.
4. **Capture the FOTA download path.** `fotaStartDownload` is the only remaining endpoint that could
   generate outbound traffic; DNS logs will not reveal it if the target is a hard-coded IP (§3.4).
5. **Get the APQ-side image.** Everything in §2 is inference from the outside; the Android side hosts
   the API, the streams and the WiFi handling, and it is the component that actually matters. Neither
   a shell nor the image was obtained here, and that is the single biggest gap in the assessment.
6. **Recover the TLS private key from a unit** and compare it against another unit's. That would settle
   whether the shipped certificate is genuinely fleet-wide, which is the assumption S7 rests on.

---

## 8. Reproducing the firmware analysis

Content inventory (script kept outside the repository, no external dependencies beyond Python 3.14):

```
python fw_content_inventory.py firmware/06.0601/factory/ulo-head-v2.1.2-UserV1-CRC32.bin \
                               firmware/08.0701/factory/ulo-head-v2.1.3-UserV3-CRC32.bin \
                               firmware/10.1308/factory/ulo-head-v2.1.8-UserV8-CRC32.bin
```

---

## 9. Known CVEs in the technology stack

The technologies identified in §2 carry substantial public vulnerability histories. Because the
device ships a platform image dated no newer than 2017-01-20 and receives no updates, it is
exposed to every CVE published since. This section catalogues the most relevant ones per component.

### 9.1 Web server — Mongoose / Civetweb (embedded C)

The device runs an embedded HTTP server from the Mongoose/Civetweb family (identified by response
signatures in §2; exact version unknown — the `Server` header is suppressed). Both projects have
accumulated significant CVEs:

**Mongoose (Cesanta):**

| CVE                         | Year | Severity | Description                                                                   |
|-----------------------------|------|----------|-------------------------------------------------------------------------------|
| CVE-2026-11404              | 2026 | High     | OOB read in built-in TLS `ClientHello` processing — crashes HTTPS/MQTTS/WSS   |
| CVE-2026-6986, -6985, -2968 | 2026 | High     | Improper signature verification and infinite loops                            |
| CVE-2026-25193              | 2026 | High     | DoS via malformed socket connections causing resource exhaustion              |
| CVE-2025-51495              | 2025 | High     | Integer overflow in WebSocket handling (v7.5–7.17) — crash or buffer overflow |
| CVE-2025-23061              | 2025 | Critical | Code injection via improper input handling (before 8.9.5)                     |
| CVE-2024-53900              | 2024 | Critical | Code execution vulnerability                                                  |
| 10 TLS vulns (Nozomi)       | 2024 | Various  | Segfaults on malicious TLS packets (fixed in v7.15)                           |

**Civetweb:**

| CVE            | Year | Severity | Description                                                            |
|----------------|------|----------|------------------------------------------------------------------------|
| CVE-2026-5789  | 2026 | High     | Privilege escalation via unquoted service path (v1.16)                 |
| CVE-2025-9648  | 2025 | Medium   | DoS via null bytes in HTTP POST causing infinite loop                  |
| CVE-2025-55763 | 2025 | Critical | Buffer overflow in URI parser (v1.14–1.16) — **remote code execution** |
| CVE-2020-27304 | 2020 | High     | Directory traversal via form-based file uploads                        |

**Relevance to ULO:** The device's web server version is unknown but predates 2017 (platform image
date). It is almost certainly vulnerable to multiple entries above. The server listens on **four
ports** (80, 8080, 443, 8443) and cannot be updated.

### 9.2 Android platform — Android 4.2+ on Qualcomm APQ

The device runs Android ≥4.2 on a Qualcomm APQ SoC (§2) with a platform image dated no newer than
2017-01-20. This generation of Android has **hundreds of unpatched CVEs**. Key exploitable ones:

| CVE                          | Severity | Description                                                                          |
|------------------------------|----------|--------------------------------------------------------------------------------------|
| CVE-2013-4787 ("Master Key") | Critical | APK signature bypass — install malware as trusted app. Only patched in Android 4.3+. |
| CVE-2013-6282 ("vroot")      | Critical | Missing access checks in ARM `get_user`/`put_user` — kernel read/write → root.       |
| CVE-2012-4220, -4221, -4222  | High     | Integer overflow in Qualcomm DIAG/KGSL drivers — arbitrary code execution via ioctl. |
| CVE-2013-2595 ("Gandalf")    | High     | Qualcomm camera driver mmap flaw — map physical memory → privilege escalation.       |

**Relevance to ULO:** ADB (port 5555) is closed, and there is no SSH or Telnet (§2). These
exploits require either a local app running on the device (unlikely on a camera) or a pivot from
the web server. If an attacker achieves code execution on the web server (via a Mongoose/Civetweb
CVE), the entire Android stack is wide open to privilege escalation because the Android version is
unpatched and unpatchable, the vendor is effectively dormant, and the kernel-level exploits above
grant full root access.

### 9.3 TLS implementation — TLS 1.0/1.1, self-signed shared certificate

| Attack              | CVE           | Protocol              | Description                                            |
|---------------------|---------------|-----------------------|--------------------------------------------------------|
| BEAST               | CVE-2011-3389 | TLS 1.0               | CBC IV predictability — decrypt session data with MitM |
| POODLE              | CVE-2014-3566 | SSL 3.0 / TLS 1.0–1.1 | Padding oracle attack — decrypt ciphertext via MitM    |
| Deprecated protocol | RFC 8996      | TLS 1.0, 1.1          | Formally deprecated; no longer considered secure       |

The device's TLS certificate is self-signed, uses SHA-1, and the **same private key ships on every
unit** (generated 2017-01-20, baked into the platform image). See §3.7.

### 9.4 VVDN Technologies (ODM)

No VVDN-specific CVEs were found. VVDN is an ODM, not a product vendor, so vulnerabilities appear
under the specific chip or software component rather than the VVDN name. Their contribution to ULO
is the platform image, WiFi integration and board support — all of which inherit the Android 4.2
and Qualcomm APQ vulnerability surface described above.

---

## 10. Attack vectors using tools already on the network

Given the technologies and findings above, the following attack chains are feasible using **only
what is already on the device and commonly available network tools**:

### 10.1 Path 1: Unauthenticated surveillance (no exploit needed)

1. Attacker joins the same WiFi network.
2. Discovers ULO via mDNS, ARP scan, or port scan (ports 80/8080/443/8443).
3. Opens `ws://<IP>/api/v1/live` — receives live H.264 video with audio, no credentials needed (S3).
4. Browses `http://<IP>/media/` — downloads all stored recordings, no credentials needed (S2).
5. Reads `GET /api/v1/state` — learns battery, power status, setup mode (S6).

**Tools required:** A web browser or `curl`/`wscat`. Nothing beyond what any computer has.

### 10.2 Path 2: Credential theft → full device control

1. Passively capture HTTP traffic (the default; no TLS) (S7).
2. Extract the `Authorization: Basic` header → plaintext username and password.
3. Or: read the session token from any `Authorization: Bearer` header.
4. Use the token to control mode, delete recordings, factory-reset, or trigger firmware update.

**Tools required:** `tcpdump`, `wireshark`, or any packet sniffer. ARP spoofing if not on the same
segment.

### 10.3 Path 3: Firmware implant via update hijack

1. ARP-spoof or DNS-spoof the network to intercept `CheckVersionOnCloud` or the FOTA download (S4).
2. Serve a crafted firmware image with the CRC-32 adjusted to match (F1).
3. The device accepts and installs it — no signature verification exists.
4. The implant survives factory reset (head MCU is not cleared by Android-side reset) (R1).

**Tools required:** `arpspoof` / `ettercap` + a Python script to compute the STM32 CRC-32.

### 10.4 Path 4: Web server exploit → Android root

1. Exploit a Mongoose/Civetweb vulnerability (e.g. CVE-2025-55763 buffer overflow) for code
   execution in the web server process (§9.1).
2. From there, exploit CVE-2013-6282 (kernel get_user/put_user) for root (§9.2).
3. Full control of the Android side: access all files, install persistent backdoor, pivot to other
   network devices.

**Tools required:** Custom exploit code; framework-level tools like Metasploit may have modules for
the Android-side CVEs.

---

## 11. Related documents

* [API reference](API.md) — every confirmed endpoint, including the update path discussed in §3.4
* [Application guide](APPLICATION.md) — the tooling used to make the device-side observations
* [Use cases](USE_CASES.md) — the isolated-network deployment recommended in §7.1
* [Company and manufacturer](COMPANY.md) — Mu Design Sàrl, Kickstarter campaign, legal status
* [Access research](ACCESS_RESEARCH.md) — ongoing attempts to gain deeper access for community firmware
