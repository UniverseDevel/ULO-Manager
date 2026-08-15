#!/usr/bin/env python3
"""
WebSocket probe - where the camera accepts an upgrade.

The event channel and the live video are WebSockets. This checks which ports and schemes accept the
handshake, which is how it was confirmed that `wss://` works on 443 and 8443 (contrary to the
assumption that the sockets are plain-HTTP only) and that the camera needs relaxed ciphers, so a
client must not enforce a modern security level.

Usage:
    python websocket_probe.py <host>
    python websocket_probe.py <host> --path /api/v1/live
"""

from __future__ import annotations

import argparse
import base64
import os
import socket
import ssl

SUBPROTOCOL = "mudesign.ulo.json"


def upgrade(host: str, port: int, path: str, use_tls: bool, timeout: float) -> str:
    key = base64.b64encode(os.urandom(16)).decode()
    request = (
        f"GET {path} HTTP/1.1\r\n"
        f"Host: {host}\r\n"
        "Upgrade: websocket\r\n"
        "Connection: Upgrade\r\n"
        f"Sec-WebSocket-Key: {key}\r\n"
        "Sec-WebSocket-Version: 13\r\n"
        f"Sec-WebSocket-Protocol: {SUBPROTOCOL}\r\n\r\n")

    try:
        sock = socket.create_connection((host, port), timeout)
    except OSError as error:
        return f"connect failed: {error}"

    if use_tls:
        context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
        context.check_hostname = False
        context.verify_mode = ssl.CERT_NONE

        # Without this the handshake fails against the camera's legacy ciphers on some builds of
        # OpenSSL - a finding in itself: a client must leave the security level to the platform
        # rather than demand a modern one.
        try:
            context.set_ciphers("ALL:@SECLEVEL=0")
        except ssl.SSLError:
            pass

        try:
            sock = context.wrap_socket(sock, server_hostname=host)
        except Exception as error:  # noqa: BLE001 - any TLS failure is interesting here
            sock.close()
            return f"TLS failed: {error}"

    try:
        sock.sendall(request.encode())
        sock.settimeout(timeout)
        response = sock.recv(400)
    except (TimeoutError, socket.timeout):
        return "no response"
    except OSError as error:
        return f"error: {error}"
    finally:
        sock.close()

    first = response.split(b"\r\n")[0].decode(errors="replace")
    negotiated = "yes" if SUBPROTOCOL.encode() in response else "no"
    return f"{first}   (subprotocol echoed: {negotiated})"


def main() -> int:
    parser = argparse.ArgumentParser(description="Check which ports accept a WebSocket upgrade.")
    parser.add_argument("host")
    parser.add_argument("--path", default="/api/v1", help="Event channel /api/v1, live video /api/v1/live")
    parser.add_argument("--timeout", type=float, default=8.0)
    args = parser.parse_args()

    print(f"{args.host}{args.path}\n")
    for port, use_tls in ((80, False), (8080, False), (443, True), (8443, True)):
        scheme = "wss" if use_tls else "ws"
        print(f"{scheme:>4}://{args.host}:{port:<5} -> {upgrade(args.host, port, args.path, use_tls, args.timeout)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
