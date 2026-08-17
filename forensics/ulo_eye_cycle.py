#!/usr/bin/env python3
"""
ULO Eye Colour Cycle
====================

Walks the camera's iris hue through the whole colour wheel, 1 to 359 and back to the
start, until interrupted with Ctrl+C.

Unlike the other scripts in this folder, this one WRITES to the camera: it sends
`PUT /api/v1/config/eyes` once per step. Nothing else is touched, and the eye settings
found at startup are put back when the script stops (use --keep to leave the last
colour in place).

The camera applies the value to the physical LEDs immediately, so the step and delay
control how fast the eyes fade around the wheel. It keeps a single session per account,
so the script signs back in automatically when something else evicts it.

Usage:
    python ulo_eye_cycle.py <ULO_IP> --user EMAIL --password PASS
    python ulo_eye_cycle.py <ULO_IP> --user EMAIL --password PASS --step 1 --delay 0.02

Requirements: Python 3.10+, no third-party packages.
"""

import argparse
import base64
import getpass
import json
import signal
import ssl
import sys
import time
import urllib.error
import urllib.request

HUE_MIN = 1
HUE_MAX = 359
REFLECTIONS = ("none", "triangle", "circles", "rectangle")


class UloError(RuntimeError):
    """A request the camera refused."""


class UloEyes:
    """Minimal ULO client: login, read the eyes section, write the eyes section."""

    def __init__(self, host: str, user: str, password: str, use_https: bool, timeout: float):
        scheme = "https" if use_https else "http"
        self.base = f"{scheme}://{host}"
        self.user = user
        self.password = password
        self.timeout = timeout
        self.token: str | None = None

        # The camera's certificate is self-signed and cannot be validated against anything,
        # so verification is switched off deliberately when TLS is requested.
        self.context = ssl._create_unverified_context() if use_https else None

    def request(self, method: str, path: str, body: str | None = None) -> tuple[int, str]:
        data = body.encode() if body is not None else None
        request = urllib.request.Request(f"{self.base}{path}", data=data, method=method)

        if self.token:
            request.add_header("Authorization", f"Bearer {self.token}")
        else:
            raw = base64.b64encode(f"{self.user}:{self.password}".encode()).decode()
            request.add_header("Authorization", f"Basic {raw}")

        if data is not None:
            # No charset parameter: the camera answers 415 when one is present.
            request.add_header("Content-Type", "application/json")

        try:
            with urllib.request.urlopen(request, timeout=self.timeout, context=self.context) as response:
                return response.status, response.read().decode(errors="replace")
        except urllib.error.HTTPError as error:
            return error.code, error.read().decode(errors="replace")
        except urllib.error.URLError as error:
            raise UloError(f"{self.base} could not be reached: {error.reason}") from error

    def login(self) -> None:
        self.token = None
        status, body = self.request("POST", "/api/v1/login", '{ "iOSAgent": false }')
        if status != 200:
            raise UloError(f"Login of {self.user!r} failed with status {status}. {body.strip()}")
        try:
            self.token = json.loads(body)["token"]
        except (ValueError, KeyError) as error:
            raise UloError(f"Login response carried no token: {body.strip()}") from error

    def logout(self) -> None:
        if self.token:
            try:
                self.request("POST", "/api/v1/logout")
            except UloError:
                pass

    def read_eyes(self) -> dict:
        status, body = self.request("GET", "/api/v1/config/eyes")
        if status != 200:
            raise UloError(f"Could not read the eye settings: {status} {body.strip()}")
        return json.loads(body)

    def write_eyes(self, eyes: dict) -> tuple[int, str]:
        body = json.dumps(eyes, separators=(",", ":"))
        status, response = self.request("PUT", "/api/v1/config/eyes", body)

        # Another client (phone app, web UI, the GUI) took the single session slot.
        if status in (401, 403):
            self.login()
            status, response = self.request("PUT", "/api/v1/config/eyes", body)

        return status, response


def _raise_keyboard_interrupt(_signum, _frame):
    """Turn Ctrl+Break on Windows into the same tidy shutdown as Ctrl+C."""
    raise KeyboardInterrupt


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Cycle the iris hue of a ULO camera from 1 to 359 and start over, until Ctrl+C.",
        epilog="This script writes to the camera. The original eye settings are restored on exit.",
    )
    parser.add_argument("host", help="Camera address, e.g. 10.0.0.63")
    parser.add_argument("--user", required=True, help="Account used to log in")
    parser.add_argument("--password", help="Password (prompted when omitted)")
    parser.add_argument("--step", type=int, default=2, help="Hue increment per write (default: 2)")
    parser.add_argument("--delay", type=float, default=0.05, help="Seconds between writes (default: 0.05)")
    parser.add_argument("--iris-size", type=int, help="Iris size 0-100 (default: leave as found)")
    parser.add_argument("--pupil-size", type=int, help="Pupil size 0-100 (default: leave as found)")
    parser.add_argument("--reflection", choices=REFLECTIONS, help="Reflection shape (default: leave as found)")
    parser.add_argument("--keep", action="store_true", help="Leave the last colour instead of restoring")
    parser.add_argument("--https", action="store_true", help="Use https (certificate is not validated)")
    parser.add_argument("--timeout", type=float, default=15.0, help="Request timeout in seconds")
    args = parser.parse_args()

    if not HUE_MIN <= args.step <= HUE_MAX:
        parser.error(f"--step must be between {HUE_MIN} and {HUE_MAX}")
    for name, value in (("--iris-size", args.iris_size), ("--pupil-size", args.pupil_size)):
        if value is not None and not 0 <= value <= 100:
            parser.error(f"{name} must be between 0 and 100")

    password = args.password or getpass.getpass(f"Password for {args.user}: ")
    camera = UloEyes(args.host, args.user, password, args.https, args.timeout)

    try:
        camera.login()
        original = camera.read_eyes()
    except UloError as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    print(f"Connected to {camera.base} as {args.user}")
    print("Original: hue {irisHue}, iris {irisSize}, pupil {pupilSize}, reflection {reflection}".format(**original))

    eyes = {
        "irisHue": HUE_MIN,
        "irisSize": args.iris_size if args.iris_size is not None else original["irisSize"],
        "pupilSize": args.pupil_size if args.pupil_size is not None else original["pupilSize"],
        "reflection": args.reflection or original["reflection"],
    }

    print(f"Cycling hue {HUE_MIN}-{HUE_MAX} in steps of {args.step} every {args.delay}s. Ctrl+C to stop.")

    # Ctrl+Break would otherwise kill the process outright and leave the last colour behind.
    if hasattr(signal, "SIGBREAK"):
        signal.signal(signal.SIGBREAK, _raise_keyboard_interrupt)

    hue, laps, writes = HUE_MIN, 0, 0
    try:
        while True:
            eyes["irisHue"] = hue
            status, body = camera.write_eyes(eyes)

            if status == 200:
                writes += 1
                print(f"\rhue {hue:3d}   lap {laps}   writes {writes}   ", end="", flush=True)
            else:
                print(f"\nhue {hue} rejected: {status} {body.strip()}", file=sys.stderr)
                time.sleep(0.5)

            hue += args.step
            if hue > HUE_MAX:
                hue = HUE_MIN
                laps += 1

            time.sleep(args.delay)
    except KeyboardInterrupt:
        print()
    except UloError as error:
        print(f"\nerror: {error}", file=sys.stderr)
        return 1
    finally:
        if args.keep:
            print("Stopped. Last colour left on the camera.")
        else:
            try:
                camera.write_eyes(original)
                print(f"Stopped after {laps} lap(s) and {writes} write(s). Original colour restored.")
            except UloError as error:
                print(f"warning: could not restore the original colour: {error}", file=sys.stderr)
        camera.logout()

    return 0


if __name__ == "__main__":
    sys.exit(main())
