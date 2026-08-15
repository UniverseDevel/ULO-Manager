#!/usr/bin/env python3
"""
Raw HTTP probe - shows exactly what the camera puts on the wire.

Written because firmware 10.1308 answers POST /api/v1/snapshot with a bare `success` line inside
the header block. That is not valid HTTP, so every ordinary client (curl, requests, .NET's
HttpClient) refuses the whole response and the reply is never seen. This script speaks HTTP over a
plain socket and prints the bytes verbatim, which is how that defect was found.

Usage:
    python raw_http.py <host> POST /api/v1/snapshot --user EMAIL --password PASS --body '{"savePicture": 0}'
    python raw_http.py <host> GET  /api/v1/state
    python raw_http.py <host> POST /api/v1/snapshot --user EMAIL --password PASS --tls --port 443

Anything that is not a valid header line is called out explicitly at the end.
"""

from __future__ import annotations

import argparse
import base64
import json
import socket
import ssl
import sys


def connect(host: str, port: int, use_tls: bool, timeout: float) -> socket.socket:
    sock = socket.create_connection((host, port), timeout)
    if not use_tls:
        return sock

    # The camera's certificate cannot be validated - it is self-signed on 06.0601 and issued by a
    # private "Mu Design CA" on 10.1308 - and its ciphers are old, so everything is relaxed here.
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    context.check_hostname = False
    context.verify_mode = ssl.CERT_NONE
    try:
        context.set_ciphers("ALL:@SECLEVEL=0")
    except ssl.SSLError:
        pass

    return context.wrap_socket(sock, server_hostname=host)


def request(host: str, port: int, method: str, path: str, body: str | None,
            headers: dict[str, str], use_tls: bool, timeout: float) -> bytes:
    payload = (body or "").encode()
    lines = [f"{method} {path} HTTP/1.1", f"Host: {host}", "Connection: close"]
    lines += [f"{name}: {value}" for name, value in headers.items()]

    if payload:
        # No charset parameter: the camera answers 415 when one is present.
        lines += ["Content-Type: application/json", f"Content-Length: {len(payload)}"]

    raw = ("\r\n".join(lines) + "\r\n\r\n").encode() + payload

    sock = connect(host, port, use_tls, timeout)
    try:
        sock.sendall(raw)
        sock.settimeout(timeout)
        chunks = []
        while True:
            data = sock.recv(65536)
            if not data:
                break
            chunks.append(data)
    except (TimeoutError, socket.timeout):
        pass
    finally:
        sock.close()

    return b"".join(chunks)


def login(host: str, port: int, user: str, password: str, use_tls: bool, timeout: float) -> str | None:
    credentials = base64.b64encode(f"{user}:{password}".encode()).decode()
    response = request(
        host, port, "POST", "/api/v1/login", '{ "iOSAgent": false }',
        {"Authorization": f"Basic {credentials}"}, use_tls, timeout)

    _, _, body = response.partition(b"\r\n\r\n")
    try:
        return json.loads(body.decode()).get("token")
    except (ValueError, UnicodeDecodeError):
        return None


def describe(response: bytes) -> None:
    head, separator, body = response.partition(b"\r\n\r\n")
    if not separator:
        print("No header/body separator found - the response is not HTTP at all:")
        print(repr(response[:400]))
        return

    lines = head.split(b"\r\n")
    print("--- status line")
    print("   ", lines[0].decode(errors="replace"))

    print("--- headers")
    malformed = []
    for line in lines[1:]:
        text = line.decode(errors="replace")
        if b":" not in line:
            malformed.append(text)
            print(f"    {text}      <-- NOT A HEADER (no colon)")
        else:
            print(f"    {text}")

    print("--- body")
    print("   ", body.decode(errors="replace")[:2000] or "(empty)")

    print("--- verdict")
    if malformed:
        print(f"    MALFORMED: {len(malformed)} line(s) in the header block are not headers: {malformed}")
        print("    A standard HTTP client rejects this whole response.")
    else:
        print("    Well formed.")


def main() -> int:
    parser = argparse.ArgumentParser(description="Send a raw HTTP request to a ULO camera and show the unparsed reply.")
    parser.add_argument("host")
    parser.add_argument("method", nargs="?", default="GET")
    parser.add_argument("path", nargs="?", default="/api/v1/state")
    parser.add_argument("--body")
    parser.add_argument("--user")
    parser.add_argument("--password")
    parser.add_argument("--port", type=int)
    parser.add_argument("--tls", action="store_true", help="Wrap the connection in TLS")
    parser.add_argument("--timeout", type=float, default=10.0)
    args = parser.parse_args()

    port = args.port or (443 if args.tls else 80)
    headers: dict[str, str] = {}

    if args.user and args.password:
        token = login(args.host, port, args.user, args.password, args.tls, args.timeout)
        if not token:
            print("Login failed - continuing unauthenticated.", file=sys.stderr)
        else:
            headers["Authorization"] = f"Bearer {token}"

    response = request(args.host, port, args.method.upper(), args.path, args.body,
                       headers, args.tls, args.timeout)

    if not response:
        print("No response.", file=sys.stderr)
        return 1

    describe(response)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
