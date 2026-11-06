#!/usr/bin/env python3
"""List SMS from a USB-connected Android phone via ADB.

IMPORTANT LIMITATION
--------------------
This tool ONLY reads SMS stored on a phone that is physically connected over
USB with USB debugging enabled. It cannot reach a SIM that sits inside a remote
vehicle tracker (e.g. Jimi VG03). For tracker-SIM SMS, see README.md.
"""

from __future__ import annotations

import argparse
import html
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import webbrowser
from datetime import datetime, timezone
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, urlparse

# content://sms/ type codes (Android Telephony.TextBasedSmsColumns)
TYPE_MAP = {
    "1": "inbox",
    "2": "sent",
    "3": "draft",
    "4": "outbox",
    "5": "failed",
    "6": "queued",
}

ROW_RE = re.compile(r"Row:\s*\d+\s+(.*)")
KV_RE = re.compile(r"(\w+)=((?:NULL)|(?:'[^']*')|(?:[^,\s]+))")


def die(msg: str, code: int = 1) -> None:
    print(msg, file=sys.stderr)
    raise SystemExit(code)


def find_adb(explicit: str | None) -> str:
    if explicit:
        if not os.path.isfile(explicit) or not os.access(explicit, os.X_OK):
            die(f"adb not executable: {explicit}")
        return explicit
    path = shutil.which("adb")
    if path:
        return path
    # Common macOS / Android SDK locations
    home = Path.home()
    candidates = [
        home / "Library/Android/sdk/platform-tools/adb",
        home / "Android/Sdk/platform-tools/adb",
        Path("/usr/local/bin/adb"),
        Path("/opt/homebrew/bin/adb"),
    ]
    for c in candidates:
        if c.is_file() and os.access(c, os.X_OK):
            return str(c)
    die(
        "adb not found. Install Android platform-tools and ensure `adb` is on PATH.\n"
        "  macOS: brew install android-platform-tools\n"
        "  Or set ANDROID_HOME and add $ANDROID_HOME/platform-tools to PATH.\n"
        "  Or pass --adb /path/to/adb"
    )
    raise AssertionError("unreachable")


def run_adb(adb: str, serial: str | None, args: list[str], timeout: int = 60) -> str:
    cmd = [adb]
    if serial:
        cmd.extend(["-s", serial])
    cmd.extend(args)
    try:
        proc = subprocess.run(
            cmd,
            capture_output=True,
            text=True,
            timeout=timeout,
            check=False,
        )
    except subprocess.TimeoutExpired:
        die(f"adb timed out: {' '.join(cmd)}")
    if proc.returncode != 0:
        err = (proc.stderr or proc.stdout or "").strip()
        die(f"adb failed ({proc.returncode}): {err or ' '.join(cmd)}")
    return proc.stdout


def list_devices(adb: str) -> list[dict[str, str]]:
    out = run_adb(adb, None, ["devices", "-l"])
    devices: list[dict[str, str]] = []
    for line in out.splitlines():
        line = line.strip()
        if not line or line.startswith("List of devices"):
            continue
        parts = line.split()
        if len(parts) < 2:
            continue
        serial, state = parts[0], parts[1]
        model = ""
        for p in parts[2:]:
            if p.startswith("model:"):
                model = p.split(":", 1)[1]
        devices.append({"serial": serial, "state": state, "model": model})
    return devices


def resolve_serial(adb: str, serial: str | None) -> str:
    devices = list_devices(adb)
    online = [d for d in devices if d["state"] == "device"]
    if not devices:
        die(
            "No devices reported by adb.\n"
            "1) Enable Developer options → USB debugging on the phone\n"
            "2) Connect USB and tap 'Allow' / trust this computer\n"
            "3) Run: adb devices"
        )
    if serial:
        match = next((d for d in devices if d["serial"] == serial), None)
        if not match:
            known = ", ".join(f"{d['serial']} ({d['state']})" for d in devices)
            die(f"Device serial not found: {serial}\nKnown: {known}")
        if match["state"] != "device":
            die(f"Device {serial} is '{match['state']}', not ready (need 'device').")
        return serial
    if len(online) == 0:
        unauthorized = [d for d in devices if d["state"] == "unauthorized"]
        if unauthorized:
            die(
                "Device connected but unauthorized. Unlock the phone and accept the "
                "USB debugging trust prompt, then retry."
            )
        states = ", ".join(f"{d['serial']}={d['state']}" for d in devices)
        die(f"No online device. States: {states}")
    if len(online) > 1:
        lines = [
            "Multiple devices connected — pass --serial <id>:",
            *[
                f"  {d['serial']}\t{d['model'] or '(unknown model)'}"
                for d in online
            ],
        ]
        die("\n".join(lines))
    return online[0]["serial"]


def unquote_val(raw: str) -> str | None:
    if raw == "NULL":
        return None
    if len(raw) >= 2 and raw[0] == "'" and raw[-1] == "'":
        return raw[1:-1].replace("\\'", "'")
    return raw


def parse_content_query(output: str) -> list[dict[str, Any]]:
    """Parse `adb shell content query` Row lines into dicts."""
    rows: list[dict[str, Any]] = []
    for line in output.splitlines():
        m = ROW_RE.search(line)
        if not m:
            continue
        payload = m.group(1)
        row: dict[str, Any] = {}
        for km in KV_RE.finditer(payload):
            key, raw = km.group(1), km.group(2)
            row[key] = unquote_val(raw)
        if row:
            rows.append(row)
    return rows


def ms_to_iso(ms: str | None) -> str:
    if not ms or not str(ms).isdigit():
        return ""
    try:
        dt = datetime.fromtimestamp(int(ms) / 1000.0, tz=timezone.utc)
        return dt.astimezone().strftime("%Y-%m-%d %H:%M:%S %Z")
    except (OverflowError, OSError, ValueError):
        return str(ms)


def normalize_messages(raw_rows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    messages: list[dict[str, Any]] = []
    for r in raw_rows:
        type_code = str(r.get("type") or "")
        messages.append(
            {
                "id": r.get("_id"),
                "address": r.get("address") or "",
                "date": ms_to_iso(r.get("date")),
                "date_ms": r.get("date"),
                "body": r.get("body") or "",
                "type": TYPE_MAP.get(type_code, type_code or "unknown"),
                "type_code": type_code,
                "read": r.get("read"),
                "thread_id": r.get("thread_id"),
            }
        )
    messages.sort(key=lambda m: int(m["date_ms"] or 0), reverse=True)
    return messages


def fetch_sms(
    adb: str,
    serial: str,
    limit: int | None,
    box: str,
) -> list[dict[str, Any]]:
    """Query SMS via content provider; fall back to dumpsys telephony if needed."""
    uri = "content://sms/"
    if box == "inbox":
        uri = "content://sms/inbox"
    elif box == "sent":
        uri = "content://sms/sent"
    elif box == "all":
        uri = "content://sms/"
    else:
        die(f"Unknown --box value: {box} (use all|inbox|sent)")

    # Prefer projection of common columns; some OEMs ignore projection.
    query_args = [
        "shell",
        "content",
        "query",
        "--uri",
        uri,
        "--projection",
        "address:date:body:type:read:thread_id:_id",
    ]
    out = run_adb(adb, serial, query_args, timeout=90)
    if "No result found" in out or not out.strip():
        # Retry without projection / broader URI
        out = run_adb(
            adb,
            serial,
            ["shell", "content", "query", "--uri", "content://sms/"],
            timeout=90,
        )

    rows = parse_content_query(out)
    if not rows and out.strip() and "Row:" not in out:
        # Permission / OEM denial — try dumpsys as last resort (often empty for SMS)
        dump = run_adb(adb, serial, ["shell", "dumpsys", "telephony.registry"], timeout=30)
        if "SMS" in dump.upper():
            print(
                "content query returned no parseable rows; dumpsys telephony has "
                "SMS-related text but is not a full inbox dump. On many phones you "
                "must grant SMS read permission to the shell / use a userdebug build, "
                "or export via the Messages app.",
                file=sys.stderr,
            )
        else:
            print(
                "No SMS rows returned. Possible causes:\n"
                "  • Empty inbox on this phone\n"
                "  • OEM blocks `content query` for SMS over ADB\n"
                "  • Need a different user / work profile\n"
                f"Raw adb output (first 500 chars):\n{out[:500]!r}",
                file=sys.stderr,
            )

    messages = normalize_messages(rows)
    if box in ("inbox", "sent"):
        messages = [m for m in messages if m["type"] == box]
    if limit is not None and limit > 0:
        messages = messages[:limit]
    return messages


def print_table(messages: list[dict[str, Any]]) -> None:
    if not messages:
        print("(no messages)")
        return
    # Column widths
    cols = ("type", "date", "address", "body")
    widths = {c: len(c) for c in cols}
    display: list[dict[str, str]] = []
    for m in messages:
        body = (m["body"] or "").replace("\n", " ").replace("\r", " ")
        if len(body) > 80:
            body = body[:77] + "..."
        row = {
            "type": str(m["type"]),
            "date": str(m["date"]),
            "address": str(m["address"]),
            "body": body,
        }
        display.append(row)
        for c in cols:
            widths[c] = max(widths[c], len(row[c]))

    header = "  ".join(c.upper().ljust(widths[c]) for c in cols)
    print(header)
    print("-" * len(header))
    for row in display:
        print("  ".join(row[c].ljust(widths[c]) for c in cols))
    print(f"\n{len(messages)} message(s)")


def build_html(messages: list[dict[str, Any]], serial: str) -> str:
    rows_html = []
    for m in messages:
        body = html.escape(m["body"] or "").replace("\n", "<br>")
        rows_html.append(
            "<tr>"
            f"<td>{html.escape(str(m['type']))}</td>"
            f"<td>{html.escape(str(m['date']))}</td>"
            f"<td>{html.escape(str(m['address']))}</td>"
            f"<td class='body'>{body}</td>"
            "</tr>"
        )
    table_body = "\n".join(rows_html) or (
        "<tr><td colspan='4'>(no messages)</td></tr>"
    )
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width, initial-scale=1"/>
<title>SheikhGo SMS Viewer</title>
<style>
  :root {{
    --bg: #0f1419;
    --panel: #1a2332;
    --text: #e7ecf3;
    --muted: #8b9bb4;
    --accent: #3d8bfd;
    --warn: #f0a020;
    --border: #2a3548;
  }}
  * {{ box-sizing: border-box; }}
  body {{
    margin: 0; font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, sans-serif;
    background: var(--bg); color: var(--text); line-height: 1.45;
  }}
  header {{
    padding: 1.25rem 1.5rem; border-bottom: 1px solid var(--border);
    background: linear-gradient(180deg, #152033, var(--bg));
  }}
  h1 {{ margin: 0 0 0.35rem; font-size: 1.35rem; font-weight: 650; }}
  .meta {{ color: var(--muted); font-size: 0.9rem; }}
  .warn {{
    margin: 1rem 1.5rem; padding: 0.85rem 1rem; border: 1px solid #6a4a12;
    background: #2a210c; color: #ffd997; border-radius: 8px; font-size: 0.92rem;
  }}
  .warn strong {{ color: var(--warn); }}
  main {{ padding: 0 1.5rem 2rem; overflow-x: auto; }}
  table {{
    width: 100%; border-collapse: collapse; margin-top: 0.5rem;
    background: var(--panel); border-radius: 8px; overflow: hidden;
  }}
  th, td {{
    text-align: left; padding: 0.65rem 0.75rem; border-bottom: 1px solid var(--border);
    vertical-align: top; font-size: 0.92rem;
  }}
  th {{
    background: #121a28; color: var(--muted); font-weight: 600;
    text-transform: uppercase; font-size: 0.75rem; letter-spacing: 0.04em;
  }}
  tr:hover td {{ background: #1f2b3d; }}
  td.body {{ max-width: 48rem; white-space: pre-wrap; word-break: break-word; }}
  .count {{ margin-top: 0.75rem; color: var(--muted); font-size: 0.85rem; }}
  a {{ color: var(--accent); }}
</style>
</head>
<body>
<header>
  <h1>SheikhGo SMS Viewer</h1>
  <div class="meta">Device serial: {html.escape(serial)} · local ADB snapshot · not live-synced</div>
</header>
<div class="warn">
  <strong>Limitation:</strong> This page shows SMS from the USB-connected Android
  phone only. It cannot read a SIM inside a remote vehicle tracker (e.g. Jimi VG03).
  Practical options for tracker SMS: (1) move SIM into a phone, (2) configure the
  tracker to reply/forward SMS to your phone, (3) use a future SMS gateway.
</div>
<main>
  <table>
    <thead><tr><th>Type</th><th>Date</th><th>Address</th><th>Body</th></tr></thead>
    <tbody>
{table_body}
    </tbody>
  </table>
  <p class="count">{len(messages)} message(s)</p>
</main>
</body>
</html>
"""


def serve_html(messages: list[dict[str, Any]], serial: str, port: int, open_browser: bool) -> None:
    html_doc = build_html(messages, serial)
    tmp = Path(tempfile.mkdtemp(prefix="sheikhgo-sms-"))
    index = tmp / "index.html"
    index.write_text(html_doc, encoding="utf-8")
    data_path = tmp / "messages.json"
    data_path.write_text(json.dumps(messages, indent=2, ensure_ascii=False), encoding="utf-8")

    class Handler(SimpleHTTPRequestHandler):
        def __init__(self, *args: Any, **kwargs: Any) -> None:
            super().__init__(*args, directory=str(tmp), **kwargs)

        def log_message(self, fmt: str, *args: Any) -> None:
            sys.stderr.write("[http] " + (fmt % args) + "\n")

        def do_GET(self) -> None:  # noqa: N802
            parsed = urlparse(self.path)
            if parsed.path in ("/", "/index.html"):
                super().do_GET()
                return
            if parsed.path == "/messages.json":
                super().do_GET()
                return
            # Allow refresh query no-op
            qs = parse_qs(parsed.query)
            if "refresh" in qs:
                self.send_response(302)
                self.send_header("Location", "/")
                self.end_headers()
                return
            self.send_error(404)

    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    url = f"http://127.0.0.1:{port}/"
    print(f"Serving SMS table at {url}")
    print(f"JSON also at {url}messages.json")
    print("Press Ctrl+C to stop.")
    if open_browser:
        try:
            webbrowser.open(url)
        except Exception:
            pass
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")
    finally:
        server.server_close()


def print_limitation_banner() -> None:
    print(
        "NOTE: Reads SMS from the USB-connected phone only — "
        "not a remote tracker SIM (Jimi VG03, etc.).\n",
        file=sys.stderr,
    )


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        description=(
            "List SMS from a USB-connected Android device via ADB. "
            "Cannot read SMS from a remote tracker SIM."
        ),
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
examples:
  %(prog)s
  %(prog)s --serial R58M... --limit 50
  %(prog)s --json
  %(prog)s --box inbox --serve --port 8765
""",
    )
    p.add_argument("--serial", "-s", help="ADB device serial (required if multiple devices)")
    p.add_argument("--adb", help="Path to adb binary (default: PATH / common SDK locations)")
    p.add_argument("--limit", "-n", type=int, default=None, help="Max messages (newest first)")
    p.add_argument(
        "--box",
        choices=("all", "inbox", "sent"),
        default="all",
        help="SMS box filter (default: all)",
    )
    p.add_argument("--json", action="store_true", help="Print JSON instead of a table")
    p.add_argument(
        "--serve",
        action="store_true",
        help="Serve a local HTML table via python http.server (127.0.0.1)",
    )
    p.add_argument("--port", type=int, default=8765, help="HTTP port for --serve (default 8765)")
    p.add_argument(
        "--no-browser",
        action="store_true",
        help="With --serve, do not open a browser",
    )
    p.add_argument(
        "--list-devices",
        action="store_true",
        help="List adb devices and exit",
    )
    p.add_argument(
        "--quiet",
        "-q",
        action="store_true",
        help="Suppress the limitation banner on stderr",
    )
    return p


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if not args.quiet:
        print_limitation_banner()

    adb = find_adb(args.adb)

    if args.list_devices:
        devices = list_devices(adb)
        if not devices:
            print("No devices.")
            return 1
        for d in devices:
            model = d["model"] or "-"
            print(f"{d['serial']}\t{d['state']}\t{model}")
        return 0

    serial = resolve_serial(adb, args.serial)
    messages = fetch_sms(adb, serial, args.limit, args.box)

    if args.serve:
        serve_html(messages, serial, args.port, open_browser=not args.no_browser)
        return 0

    if args.json:
        print(json.dumps(messages, indent=2, ensure_ascii=False))
    else:
        print(f"Device: {serial}\n")
        print_table(messages)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
