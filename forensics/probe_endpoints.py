#!/usr/bin/env python3
"""
Endpoint sweep - which calls exist on this firmware.

Runs every endpoint of the registry against one unit and prints the status code for each, so the
firmware comparison table in `docs/API.md` can be rebuilt from measurements instead of hearsay.
Point it at two units on different firmware and diff the two outputs.

Usage:
    python probe_endpoints.py <host> --user EMAIL --password PASS
    python probe_endpoints.py <host> --user EMAIL --password PASS --write-only    (include PUT/POST/DELETE)

Read-only calls are sent by default. Anything that changes or destroys state is skipped unless
--write-only is given, and the genuinely destructive ones (factory reset, delete, firmware install)
are never sent at all.
"""

from __future__ import annotations

import argparse
import base64
import json
import socket
import time

# (method, path, needs_body) - the read-only surface of the API.
READ_ONLY = [
    ("GET", "/api/v1/state", None),
    ("GET", "/api/v1/mode", None),
    ("GET", "/api/v1/time", None),
    ("GET", "/api/v1/record", None),
    ("GET", "/api/v1/config", None),
    ("GET", "/api/v1/config/access", None),
    ("GET", "/api/v1/config/alert", None),
    ("GET", "/api/v1/config/device", None),
    ("GET", "/api/v1/config/email", None),
    ("GET", "/api/v1/config/exclusion", None),
    ("GET", "/api/v1/config/eyes", None),
    ("GET", "/api/v1/config/face", None),
    ("GET", "/api/v1/config/firmware", None),
    ("GET", "/api/v1/config/language", None),
    ("GET", "/api/v1/config/language/languages", None),
    ("GET", "/api/v1/config/time", None),
    ("GET", "/api/v1/config/time/countries", None),
    ("GET", "/api/v1/config/time/zones", None),
    ("GET", "/api/v1/config/video", None),
    ("GET", "/api/v1/config/voice", None),
    ("GET", "/api/v1/config/wifi", None),
    ("GET", "/api/v1/config/wifi/networks", None),
    ("GET", "/api/v1/config/reset", None),
    ("GET", "/api/v1/users", None),
    ("GET", "/api/v1/users/1", None),
    ("GET", "/api/v1/users/1/devices", None),
    ("GET", "/api/v1/files/media", None),
    ("GET", "/api/v1/files/media?type=video", None),
    ("GET", "/api/v1/files/media?type=snapshot", None),
    ("GET", "/api/v1/files/directoryCount", None),
    ("GET", "/api/v1/files/stats", None),
    ("GET", "/api/v1/files/backup", None),
    ("GET", "/api/v1/system/log", None),
    ("GET", "/api/v1/system/backups", None),
    ("GET", "/api/v1/accessEverywhere", None),
    ("GET", "/api/v1/interface/fotaStatus", None),
    ("GET", "/api/v1/interface/fotaNumberOfUpdates", None),
    ("GET", "/api/v1/interface/fotaIsInstallAvailable", None),
    ("GET", "/api/v1/interface/fotaVersion", None),
    ("GET", "/api/v1/behaviors", None),
    ("GET", "/api/v1/neighbors", None),
    ("GET", "/api/v1/eyes", None),
    ("GET", "/api/v1/faces", None),
    ("GET", "/api/v1/import", None),
    ("GET", "/media/", None),
    ("GET", "/logs/", None),
]

# Sent only with --write-only. These change something but nothing that cannot be set back.
WRITE = [
    ("POST", "/api/v1/config/time/zones", '{ "code": "SK" }'),
    ("POST", "/api/v1/system/log", "{}"),
    ("POST", "/api/v1/backgroundImage", "{}"),
    ("POST", "/api/v1/interface/CheckVersionOnCloud", "{}"),
]


def call(host: str, port: int, method: str, path: str, body: str | None,
         token: str | None, timeout: float) -> tuple[str, str]:
    payload = (body or "").encode()
    lines = [f"{method} {path} HTTP/1.1", f"Host: {host}", "Connection: close"]
    if token:
        lines.append(f"Authorization: Bearer {token}")
    if payload:
        lines += ["Content-Type: application/json", f"Content-Length: {len(payload)}"]

    raw = ("\r\n".join(lines) + "\r\n\r\n").encode() + payload

    try:
        sock = socket.create_connection((host, port), timeout)
    except OSError as error:
        return "----", str(error)

    try:
        sock.sendall(raw)
        sock.settimeout(timeout)
        received = b""
        while len(received) < 4096:
            chunk = sock.recv(4096)
            if not chunk:
                break
            received += chunk
    except (TimeoutError, socket.timeout):
        pass
    except OSError as error:
        return "----", str(error)
    finally:
        sock.close()

    if not received:
        return "----", "no response"

    head, _, body_bytes = received.partition(b"\r\n\r\n")
    status_line = head.split(b"\r\n")[0].decode(errors="replace")
    status = status_line.split(" ")[1] if " " in status_line else "????"

    note = ""
    if any(b":" not in line for line in head.split(b"\r\n")[1:] if line):
        note = "MALFORMED HEADERS  "

    return status, note + body_bytes.decode(errors="replace").replace("\n", " ")[:80]


def login(host: str, port: int, user: str, password: str, timeout: float) -> str | None:
    credentials = base64.b64encode(f"{user}:{password}".encode()).decode()
    payload = '{ "iOSAgent": false }'.encode()
    raw = ("\r\n".join([
        "POST /api/v1/login HTTP/1.1", f"Host: {host}", "Connection: close",
        f"Authorization: Basic {credentials}", "Content-Type: application/json",
        f"Content-Length: {len(payload)}"]) + "\r\n\r\n").encode() + payload

    sock = socket.create_connection((host, port), timeout)
    try:
        sock.sendall(raw)
        sock.settimeout(timeout)
        received = sock.recv(8192)
    finally:
        sock.close()

    _, _, body = received.partition(b"\r\n\r\n")
    try:
        return json.loads(body.decode()).get("token")
    except (ValueError, UnicodeDecodeError):
        return None


def main() -> int:
    parser = argparse.ArgumentParser(description="Sweep the known endpoints of a ULO camera and report the status of each.")
    parser.add_argument("host")
    parser.add_argument("--user")
    parser.add_argument("--password")
    parser.add_argument("--port", type=int, default=80)
    parser.add_argument("--timeout", type=float, default=10.0)
    parser.add_argument("--write-only", action="store_true", help="Also send the harmless write calls")
    args = parser.parse_args()

    token = None
    if args.user and args.password:
        token = login(args.host, args.port, args.user, args.password, args.timeout)
        print(f"Login: {'ok' if token else 'FAILED'}\n")

    firmware = "unknown"
    if token:
        _, body = call(args.host, args.port, "GET", "/api/v1/config/firmware", None, token, args.timeout)
        for part in body.split('"'):
            if part.count(".") == 1 and part[0].isdigit():
                firmware = part
                break

    print(f"{args.host} - firmware {firmware}")
    print(f"{'STATUS':<8} {'METHOD':<7} {'PATH':<48} BODY")
    print("-" * 110)

    endpoints = READ_ONLY + (WRITE if args.write_only else [])
    for method, path, body in endpoints:
        status, preview = call(args.host, args.port, method, path, body, token, args.timeout)
        print(f"{status:<8} {method:<7} {path:<48} {preview}")

        # The camera keeps one session per account, so a fresh login is needed if it evicts us.
        if status == "401" and token and args.user and args.password:
            token = login(args.host, args.port, args.user, args.password, args.timeout)

        time.sleep(0.2)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
