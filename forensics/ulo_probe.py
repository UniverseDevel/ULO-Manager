#!/usr/bin/env python3
"""
ULO Device Reconnaissance — Non-Destructive Probe
===================================================

Systematically enumerates everything accessible on a ULO camera from the
network, using only read-only HTTP/WebSocket requests and a port scan.
Does NOT attempt exploits, write operations or firmware modification.

Usage:
    python ulo_probe.py <ULO_IP> [--user EMAIL --password PASS]

Output: a text report to stdout and a JSON file (ulo_probe_results.json).

Requirements: Python 3.10+, no third-party packages.
"""

import argparse
import base64
import json
import socket
import ssl
import sys
import time
import urllib.request
import urllib.error
from datetime import datetime
from pathlib import Path

# ── Configuration ──────────────────────────────────────────────────────────

COMMON_PORTS = [
    20, 21, 22, 23, 25, 53, 80, 443, 554, 1883, 1900, 2323, 3000, 3389,
    4443, 5000, 5353, 5555, 7681, 8000, 8008, 8080, 8081, 8443, 8554,
    8883, 8888, 8901, 9090, 9100, 9999, 10000, 49152, 49153, 49154,
]

# Known documented endpoints
KNOWN_ENDPOINTS = [
    ("GET",  "/api/v1/state"),
    ("GET",  "/api/v1/mode"),
    ("GET",  "/api/v1/time"),
    ("GET",  "/api/v1/config"),
    ("GET",  "/api/v1/config/access"),
    ("GET",  "/api/v1/config/alert"),
    ("GET",  "/api/v1/config/device"),
    ("GET",  "/api/v1/config/email"),
    ("GET",  "/api/v1/config/exclusion"),
    ("GET",  "/api/v1/config/eyes"),
    ("GET",  "/api/v1/config/face"),
    ("GET",  "/api/v1/config/firmware"),
    ("GET",  "/api/v1/config/language"),
    ("GET",  "/api/v1/config/time"),
    ("GET",  "/api/v1/config/video"),
    ("GET",  "/api/v1/config/voice"),
    ("GET",  "/api/v1/config/wifi"),
    ("GET",  "/api/v1/config/wifi/networks"),
    ("GET",  "/api/v1/config/time/countries"),
    ("GET",  "/api/v1/config/language/languages"),
    ("GET",  "/api/v1/config/reset"),
    ("GET",  "/api/v1/users"),
    ("GET",  "/api/v1/users/1"),
    ("GET",  "/api/v1/users/1/devices"),
    ("GET",  "/api/v1/files/media"),
    ("GET",  "/api/v1/files/media?type=video"),
    ("GET",  "/api/v1/files/media?type=snapshot"),
    ("GET",  "/api/v1/files/stats"),
    ("GET",  "/api/v1/files/directoryCount"),
    ("GET",  "/api/v1/files/backup"),
    ("GET",  "/api/v1/system/log"),
    ("GET",  "/api/v1/system/backups"),
    ("GET",  "/api/v1/interface/fotaStatus"),
    ("GET",  "/api/v1/interface/fotaNumberOfUpdates"),
    ("GET",  "/api/v1/interface/fotaIsInstallAvailable"),
    ("GET",  "/api/v1/behaviors"),
    ("GET",  "/api/v1/neighbors"),
    ("GET",  "/api/v1/import"),
]

# Undocumented / speculative endpoints to probe
DISCOVERY_PATHS = [
    # Android / debug
    "/", "/index.html", "/index.htm",
    "/debug", "/debug/", "/debug/log",
    "/admin", "/admin/", "/administrator",
    "/shell", "/console", "/terminal",
    "/cgi-bin/", "/cgi-bin/luci",
    # Common embedded server paths
    "/server-status", "/server-info",
    "/status", "/info", "/version",
    "/healthcheck", "/health",
    "/metrics", "/stats",
    # Mongoose / Civetweb specific
    "/ssi", "/.htpasswd", "/.htaccess",
    # Android paths
    "/data/", "/system/", "/sdcard/",
    "/proc/", "/proc/version", "/proc/cpuinfo",
    "/etc/", "/etc/passwd", "/etc/shadow",
    "/dev/", "/tmp/",
    # ULO-specific guesses
    "/api/", "/api/v1/", "/api/v1/version",
    "/api/v1/firmware", "/api/v1/update",
    "/api/v1/debug", "/api/v1/shell",
    "/api/v1/system/", "/api/v1/system/info",
    "/api/v1/system/version", "/api/v1/system/reboot",
    "/api/v1/system/update", "/api/v1/system/firmware",
    "/api/v1/system/config", "/api/v1/system/status",
    "/api/v1/system/network", "/api/v1/system/wifi",
    "/api/v1/interface/", "/api/v1/interface/version",
    "/api/v1/interface/CheckVersionOnCloud",
    "/api/v1/accessEverywhere",
    "/api/v1/backgroundImage",
    "/api/v1/snapshot",
    "/api/v1/record",
    "/api/v1/live",
    "/api/v2/", "/api/v2/state",
    # Media and filesystem
    "/media/", "/logs/", "/logs/system.txt",
    "/system.txt", "/log.txt",
    "/firmware/", "/update/", "/upload/",
    "/backup/", "/backups/",
    "/config/", "/settings/",
    "/sdcard/", "/sd/", "/mnt/",
    # Directory traversal probes (read-only — just checks response)
    "/media/../", "/media/../../",
    "/api/v1/../", "/api/../",
    "/../etc/passwd", "/..%2f..%2fetc%2fpasswd",
    "/%2e%2e/%2e%2e/etc/passwd",
]

# ── Helpers ────────────────────────────────────────────────────────────────

def log(msg: str) -> None:
    print(f"[{datetime.now().strftime('%H:%M:%S')}] {msg}", flush=True)

def tcp_connect(host: str, port: int, timeout: float = 2.0) -> bool:
    try:
        with socket.create_connection((host, port), timeout=timeout):
            return True
    except (OSError, socket.timeout):
        return False

def http_get(host: str, port: int, path: str, token: str | None = None,
             use_tls: bool = False, timeout: float = 5.0) -> dict:
    """Issue a GET and return {status, headers, body, error}."""
    scheme = "https" if use_tls else "http"
    url = f"{scheme}://{host}:{port}{path}"
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"

    req = urllib.request.Request(url, headers=headers, method="GET")
    ctx = None
    if use_tls:
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE

    try:
        with urllib.request.urlopen(req, timeout=timeout, context=ctx) as resp:
            body = resp.read().decode("utf-8", errors="replace")
            return {
                "status": resp.status,
                "headers": dict(resp.headers),
                "body": body[:4000],
                "error": None,
            }
    except urllib.error.HTTPError as e:
        body = ""
        try:
            body = e.read().decode("utf-8", errors="replace")[:2000]
        except Exception:
            pass
        return {"status": e.code, "headers": dict(e.headers), "body": body, "error": None}
    except Exception as e:
        return {"status": None, "headers": {}, "body": "", "error": str(e)[:200]}

def http_options(host: str, port: int, path: str, timeout: float = 5.0) -> dict:
    """Issue an OPTIONS request to discover allowed methods."""
    url = f"http://{host}:{port}{path}"
    req = urllib.request.Request(url, method="OPTIONS")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            allow = resp.headers.get("Access-Control-Allow-Methods", "")
            return {"status": resp.status, "allow": allow, "error": None}
    except urllib.error.HTTPError as e:
        allow = e.headers.get("Access-Control-Allow-Methods", "") if e.headers else ""
        return {"status": e.code, "allow": allow, "error": None}
    except Exception as e:
        return {"status": None, "allow": "", "error": str(e)[:200]}

def login(host: str, port: int, user: str, password: str) -> str | None:
    """Authenticate and return a bearer token, or None."""
    url = f"http://{host}:{port}/api/v1/login"
    creds = base64.b64encode(f"{user}:{password}".encode()).decode()
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Basic {creds}",
    }
    data = json.dumps({"iOSAgent": False}).encode()
    req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=10) as resp:
            body = json.loads(resp.read())
            return body.get("token")
    except Exception as e:
        log(f"  Login failed: {e}")
        return None

def grab_tls_cert(host: str, port: int = 443) -> dict | None:
    """Connect with TLS and return certificate details."""
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    try:
        with socket.create_connection((host, port), timeout=5) as sock:
            with ctx.wrap_socket(sock, server_hostname=host) as ssock:
                cert = ssock.getpeercert(binary_form=False)
                cipher = ssock.cipher()
                version = ssock.version()
                # getpeercert() with binary_form=False returns {} for
                # self-signed certs that fail validation; get DER instead
                der = ssock.getpeercert(binary_form=True)
                return {
                    "version": version,
                    "cipher": cipher,
                    "cert_parsed": cert if cert else "(self-signed / not validated)",
                    "cert_der_length": len(der) if der else 0,
                }
    except Exception as e:
        return {"error": str(e)[:200]}


# ── Probe phases ───────────────────────────────────────────────────────────

def phase_port_scan(host: str) -> dict:
    """Scan common ports for open services."""
    log("Phase 1: Port scan")
    results = {}
    for port in COMMON_PORTS:
        if tcp_connect(host, port):
            # Try to grab a banner
            banner = ""
            try:
                with socket.create_connection((host, port), timeout=3) as s:
                    s.settimeout(2)
                    try:
                        s.sendall(b"\r\n")
                        banner = s.recv(256).decode("utf-8", errors="replace").strip()
                    except socket.timeout:
                        pass
            except Exception:
                pass
            results[port] = {"open": True, "banner": banner[:200]}
            log(f"  Port {port}: OPEN" + (f" — {banner[:60]}" if banner else ""))
        # Don't log closed ports to keep output clean
    log(f"  {len(results)} open port(s) found out of {len(COMMON_PORTS)} scanned")
    return results

def phase_tls_inspect(host: str, ports: dict) -> dict:
    """Inspect TLS on any open HTTPS ports."""
    log("Phase 2: TLS inspection")
    results = {}
    for port in [443, 8443]:
        if port in ports:
            info = grab_tls_cert(host, port)
            results[port] = info
            log(f"  Port {port}: {info.get('version', 'N/A')}, cipher={info.get('cipher', 'N/A')}")
    if not results:
        log("  No TLS ports found")
    return results

def phase_unauth_endpoints(host: str, port: int = 80) -> dict:
    """Test which endpoints respond without authentication."""
    log("Phase 3: Unauthenticated endpoint enumeration")
    results = {}
    for method, path in KNOWN_ENDPOINTS:
        resp = http_get(host, port, path)
        status = resp["status"]
        tag = "✓ OPEN" if status == 200 else f"  {status}" if status else f"  ERR"
        results[path] = {
            "status": status,
            "auth_required": status == 401,
            "body_preview": resp["body"][:200] if status == 200 else "",
        }
        if status == 200:
            log(f"  {tag}  {path}  ({len(resp['body'])} bytes)")
    unauth_count = sum(1 for r in results.values() if r["status"] == 200)
    auth_count = sum(1 for r in results.values() if r["auth_required"])
    log(f"  {unauth_count} open without auth, {auth_count} require auth")
    return results

def phase_auth_endpoints(host: str, port: int, token: str) -> dict:
    """Test known endpoints with authentication."""
    log("Phase 4: Authenticated endpoint enumeration")
    results = {}
    for method, path in KNOWN_ENDPOINTS:
        resp = http_get(host, port, path, token=token)
        status = resp["status"]
        results[path] = {
            "status": status,
            "body_preview": resp["body"][:300] if status == 200 else resp["body"][:100],
        }
        if status == 200:
            log(f"  ✓ {path}  ({len(resp['body'])} bytes)")
    return results

def phase_discovery(host: str, port: int = 80, token: str | None = None) -> dict:
    """Probe undocumented paths for anything unexpected."""
    log("Phase 5: Endpoint discovery (undocumented paths)")
    results = {}
    for path in DISCOVERY_PATHS:
        resp = http_get(host, port, path, token=token)
        status = resp["status"]
        if status and status not in (404, 405):
            results[path] = {
                "status": status,
                "body_preview": resp["body"][:300],
            }
            log(f"  [{status}] {path}" + (f"  ({len(resp['body'])} bytes)" if status == 200 else ""))
    log(f"  {len(results)} non-404 responses out of {len(DISCOVERY_PATHS)} probed")
    return results

def phase_options_sweep(host: str, port: int = 80) -> dict:
    """OPTIONS sweep to discover allowed methods on API paths."""
    log("Phase 6: OPTIONS method sweep")
    api_paths = [
        "/api/v1/state", "/api/v1/mode", "/api/v1/time",
        "/api/v1/config", "/api/v1/users", "/api/v1/snapshot",
        "/api/v1/record", "/api/v1/files/media", "/api/v1/files/stats",
        "/api/v1/system/log", "/api/v1/system/backup",
        "/api/v1/system/restore", "/api/v1/system/reset",
        "/api/v1/interface/fotaStatus",
        "/api/v1/interface/fotaStartDownload",
        "/api/v1/interface/fotaInstallFirmware",
        "/api/v1/behaviors", "/api/v1/neighbors",
        "/api/v1/admin", "/api/v1/import",
        "/api/v1/live",
        # Speculative
        "/api/v1/debug", "/api/v1/shell", "/api/v1/exec",
        "/api/v1/firmware", "/api/v1/update", "/api/v1/upload",
        "/api/v1/reboot", "/api/v1/network",
    ]
    results = {}
    for path in api_paths:
        resp = http_options(host, port, path)
        if resp["allow"]:
            results[path] = resp["allow"]
            log(f"  {path}: {resp['allow']}")
    log(f"  {len(results)} paths responded to OPTIONS")
    return results

def phase_directory_index(host: str, port: int = 80) -> dict:
    """Enumerate the directory index on /media/ and root."""
    log("Phase 7: Directory index enumeration")
    results = {}
    for path in ["/", "/media/", "/logs/", "/firmware/", "/update/",
                 "/config/", "/backup/", "/data/", "/sdcard/"]:
        resp = http_get(host, port, path)
        if resp["status"] == 200 and ("<a " in resp["body"].lower() or "href=" in resp["body"].lower()):
            # Count links
            body = resp["body"]
            link_count = body.lower().count("href=")
            results[path] = {
                "status": 200,
                "link_count": link_count,
                "body_preview": body[:500],
            }
            log(f"  ✓ {path}: directory listing with {link_count} links")
        elif resp["status"] == 200:
            results[path] = {"status": 200, "body_preview": resp["body"][:200]}
            log(f"  ✓ {path}: 200 (not a directory listing)")
    return results

def phase_adb_check(host: str) -> dict:
    """Check if ADB is accessible on common ports."""
    log("Phase 8: ADB (Android Debug Bridge) check")
    adb_ports = [5555, 5037, 5556, 5557, 5558]
    results = {}
    for port in adb_ports:
        if tcp_connect(host, port, timeout=3):
            # Try ADB handshake: send CNXN message header
            try:
                with socket.create_connection((host, port), timeout=3) as s:
                    # ADB protocol: CNXN command
                    s.sendall(b"CNXN\x00\x00\x00\x01"
                              b"\x00\x10\x00\x00"
                              b"\x07\x00\x00\x00"
                              b"host::\x00")
                    s.settimeout(3)
                    resp_data = s.recv(256)
                    if resp_data[:4] == b"CNXN":
                        results[port] = "ADB RESPONDING"
                        log(f"  Port {port}: ADB RESPONDING!")
                    else:
                        results[port] = f"open, non-ADB response ({resp_data[:20]})"
                        log(f"  Port {port}: open but not ADB")
            except Exception as e:
                results[port] = f"open, connection error: {e}"
                log(f"  Port {port}: open but error — {e}")
        else:
            pass  # Don't log closed ports
    if not results:
        log("  No ADB ports open (checked 5555, 5037, 5556-5558)")
    return results

def phase_system_log(host: str, port: int, token: str | None = None) -> dict:
    """Grab and analyse the system log for useful intelligence."""
    log("Phase 9: System log analysis")
    resp = http_get(host, port, "/api/v1/system/log", token=token)
    if resp["status"] != 200:
        log(f"  System log returned {resp['status']}")
        return {"status": resp["status"]}

    log_text = resp["body"]
    analysis = {
        "status": 200,
        "length": len(log_text),
        "ssids": [],
        "interesting_lines": [],
        "vvdn_lines": 0,
        "crash_lines": 0,
    }

    for line in log_text.split("\n"):
        line_strip = line.strip()
        if "Connected to network" in line_strip:
            # Extract SSID
            start = line_strip.find('"')
            end = line_strip.rfind('"')
            if start != -1 and end > start:
                ssid = line_strip[start+1:end]
                if ssid not in analysis["ssids"]:
                    analysis["ssids"].append(ssid)
        if "VVDN:" in line_strip:
            analysis["vvdn_lines"] += 1
        if "crash" in line_strip.lower() or "exception" in line_strip.lower():
            analysis["crash_lines"] += 1
            analysis["interesting_lines"].append(line_strip[:200])
        if any(kw in line_strip.lower() for kw in [
            "adb", "shell", "root", "su ", "debug", "serial", "uart",
            "jtag", "swd", "unlock", "boot", "kernel", "mount",
            "partition", "recovery", "fastboot"
        ]):
            analysis["interesting_lines"].append(line_strip[:200])

    analysis["interesting_lines"] = analysis["interesting_lines"][:50]
    log(f"  Log length: {analysis['length']} chars")
    log(f"  SSIDs found: {len(analysis['ssids'])}")
    log(f"  VVDN platform lines: {analysis['vvdn_lines']}")
    log(f"  Crash/exception lines: {analysis['crash_lines']}")
    log(f"  Debug-interesting lines: {len(analysis['interesting_lines'])}")
    return analysis


# ── Main ───────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(description="ULO Device Probe — non-destructive reconnaissance")
    parser.add_argument("host", help="ULO camera IP address")
    parser.add_argument("--user", help="Account email for authenticated probing")
    parser.add_argument("--password", help="Account password")
    parser.add_argument("--port", type=int, default=80, help="HTTP port (default: 80)")
    parser.add_argument("--output", default="ulo_probe_results.json", help="JSON output file")
    args = parser.parse_args()

    host = args.host
    port = args.port

    log(f"ULO Device Probe — target: {host}:{port}")
    log(f"Time: {datetime.now().isoformat()}")
    log("=" * 60)

    results = {
        "target": host,
        "port": port,
        "timestamp": datetime.now().isoformat(),
        "phases": {},
    }

    # Phase 1: Port scan
    ports = phase_port_scan(host)
    results["phases"]["port_scan"] = ports

    # Phase 2: TLS inspection
    tls = phase_tls_inspect(host, ports)
    results["phases"]["tls"] = tls

    # Phase 3: Unauthenticated endpoints
    unauth = phase_unauth_endpoints(host, port)
    results["phases"]["unauth_endpoints"] = unauth

    # Phase 4: Authenticated endpoints (if credentials provided)
    token = None
    if args.user and args.password:
        log("Authenticating...")
        token = login(host, port, args.user, args.password)
        if token:
            log(f"  Authenticated, token: {token[:16]}...")
            auth = phase_auth_endpoints(host, port, token)
            results["phases"]["auth_endpoints"] = auth
        else:
            log("  Authentication failed — skipping authenticated probes")
    else:
        log("No credentials provided — skipping authenticated probes")

    # Phase 5: Endpoint discovery
    discovery = phase_discovery(host, port, token)
    results["phases"]["discovery"] = discovery

    # Phase 6: OPTIONS sweep
    options = phase_options_sweep(host, port)
    results["phases"]["options_sweep"] = options

    # Phase 7: Directory index
    dirs = phase_directory_index(host, port)
    results["phases"]["directory_index"] = dirs

    # Phase 8: ADB check
    adb = phase_adb_check(host)
    results["phases"]["adb_check"] = adb

    # Phase 9: System log
    syslog = phase_system_log(host, port, token)
    results["phases"]["system_log"] = syslog

    # Summary
    log("=" * 60)
    log("SUMMARY")
    log(f"  Open ports: {sorted(ports.keys())}")
    log(f"  Unauthenticated endpoints: {sum(1 for r in unauth.values() if r['status'] == 200)}")
    log(f"  Discovered (non-404) paths: {len(discovery)}")
    log(f"  OPTIONS-responsive paths: {len(options)}")
    log(f"  Directory listings: {sum(1 for r in dirs.values() if r.get('link_count', 0) > 0)}")
    log(f"  ADB ports open: {len(adb)}")

    # Write JSON
    output_path = Path(args.output)
    output_path.write_text(json.dumps(results, indent=2, default=str))
    log(f"  Results written to: {output_path}")

    # Access assessment
    log("")
    log("ACCESS ASSESSMENT")
    if adb:
        log("  ★ ADB port(s) open — this is the fastest path to a shell")
    if any(r.get("link_count", 0) > 0 for r in dirs.values()):
        log("  ★ Directory listings available — filesystem partially browsable")
    unauth_paths = [p for p, r in unauth.items() if r["status"] == 200]
    if unauth_paths:
        log(f"  ★ {len(unauth_paths)} endpoint(s) accessible without auth")
    interesting = syslog.get("interesting_lines", [])
    if interesting:
        log(f"  ★ {len(interesting)} debug-interesting lines in system log — review manually")
    log("")
    log("Next steps:")
    log("  1. Review ulo_probe_results.json for full details")
    log("  2. If ADB is open: adb connect <IP>:5555")
    log("  3. If not: look for UART/JTAG on the PCB (physical access)")
    log("  4. Review system log for boot paths, partition info, debug hints")


if __name__ == "__main__":
    main()
