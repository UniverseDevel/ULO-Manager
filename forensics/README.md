# Forensics

The scripts used to work out how the camera actually behaves. Everything documented in
[`docs/API.md`](../docs/API.md) and [`docs/SECURITY.md`](../docs/SECURITY.md) was measured with
these against live units, rather than taken from vendor material — and they are kept so any finding
can be reproduced, and so a unit on a firmware version nobody has seen yet can be characterised.

They are deliberately plain: Python 3 standard library and PowerShell only, no packages to install,
no state, and every address and account is passed on the command line. None of them writes anything
back to the camera except where a script says so explicitly.

| Script                                               | What it answers                                                                                                                                                                    |
|------------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [`ulo_probe.py`](ulo_probe.py)                       | Full survey of one unit: open ports, endpoint discovery, authentication surface, static file exposure, system log analysis. The broad first pass; writes `ulo_probe_results.json`. |
| [`probe_endpoints.py`](probe_endpoints.py)           | Which endpoints exist on this firmware, with the status code of each. Run it against two units and diff, and the firmware table in `docs/API.md` falls out.                        |
| [`raw_http.py`](raw_http.py)                         | What the camera literally puts on the wire, bypassing every HTTP parser.                                                                                                           |
| [`websocket_probe.py`](websocket_probe.py)           | Which ports and schemes accept a WebSocket upgrade (`ws://`, `wss://`).                                                                                                            |
| [`capture_certificate.ps1`](capture_certificate.ps1) | The TLS certificate a unit presents, with every field worth recording, optionally archived as DER and PEM.                                                                         |

## Findings these produced

* **`raw_http.py`** found that firmware 10.1308 answers `POST /api/v1/snapshot` with a bare
  `success` line inside the header block. That is not a valid header, so ordinary clients reject the
  entire response and the picture can never be retrieved — the reason `UloRawHttp` exists in the
  application. The script flags such lines explicitly.
* **`probe_endpoints.py`** established which calls are real per firmware: `accessEverywhere`,
  `backgroundImage` and `CheckVersionOnCloud` answer on 10.1308 and 404 on 06.0601, while
  `fotaVersion`, `eyes` and `faces` are listed by the camera's own web app but 404 on both. It also
  showed `config/time/zones` is `POST`-only on both, and that `system/backups` is refused with
  *"Please switch to Standard mode"* unless the camera records in standard mode.
* **`websocket_probe.py`** showed the sockets do work over TLS on 443 and 8443, and that the
  handshake needs a relaxed cipher policy — so a client must leave the TLS level to the platform
  instead of demanding a modern one.
* **`capture_certificate.ps1`** produced the archived certificates under
  `firmware/06.0601/device/` and `firmware/10.1308/device/`, and showed the two firmware versions
  ship completely different certificates and keys.

## Usage

```bash
python ulo_probe.py <camera-ip> --user you@example.com --password yourpass
python probe_endpoints.py <camera-ip> --user you@example.com --password yourpass
python raw_http.py <camera-ip> POST /api/v1/snapshot --user you@example.com --password yourpass --body '{"savePicture": 0}'
python websocket_probe.py <camera-ip>
```

```powershell
./capture_certificate.ps1 -CameraHost <camera-ip> -OutputFolder ../firmware/<version>/device
```

Each script has `--help`.

## Please note

* **Only probe cameras you own.** These scripts authenticate, enumerate and read the device log.
* **The camera keeps one session per account.** Probing signs other clients out — the phone app, the
  web UI, another copy of this tool. Use a dedicated account, and expect the scripts to re-login when
  the camera evicts them.
* **A camera under a probe misses events.** It has a single video pipeline, and repeated snapshot or
  live-video calls have been observed to make a unit reboot. Do not run these against a camera you
  are relying on at that moment.
* **Results contain your environment.** `ulo_probe_results.json` includes the device name, Wi-Fi
  SSID, accounts and log excerpts. Read it before sharing it — it is deliberately not committed here.
