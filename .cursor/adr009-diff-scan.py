#!/usr/bin/env python3
"""ADR-009 preflight: classify secret-pattern hits in git diff vs origin/main.
Writes NDJSON to .cursor/debug-6d9079.log — never logs secret values."""
from __future__ import annotations

import json
import re
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOG = ROOT / ".cursor" / "debug-6d9079.log"
SESSION = "6d9079"
RUN_ID = "post-fix"


def log(hypothesis_id: str, location: str, message: str, data: dict) -> None:
    row = {
        "sessionId": SESSION,
        "runId": RUN_ID,
        "hypothesisId": hypothesis_id,
        "location": location,
        "message": message,
        "data": data,
        "timestamp": int(time.time() * 1000),
    }
    with LOG.open("a", encoding="utf-8") as f:
        f.write(json.dumps(row) + "\n")


def main() -> None:
    # #region agent log
    diff = subprocess.check_output(
        ["git", "diff", "origin/main"], cwd=ROOT, text=True, errors="replace"
    )
    files = subprocess.check_output(
        ["git", "diff", "--name-only", "origin/main"], cwd=ROOT, text=True
    ).splitlines()

    removal_kinds: set[str] = set()
    removal_count = 0
    for line in diff.splitlines():
        if not line.startswith("-") or line.startswith("---"):
            continue
        kinds = []
        if re.search(r"Password\s*=", line, re.I):
            kinds.append("conn_Password=")
        if re.search(r'"Password"\s*:\s*"[^"]+"', line) and "__SET_" not in line:
            kinds.append("json_Password_live")
        if re.search(r'"Secret"\s*:\s*"[^"]{16,}"', line) and "__SET_" not in line:
            kinds.append("json_Secret_live")
        if re.search(r"AIzaSy[A-Za-z0-9_-]+", line):
            kinds.append("AIzaSy")
        if kinds:
            removal_count += 1
            removal_kinds.update(kinds)
    log(
        "A",
        "adr009-diff-scan.py:removal",
        "Removal hunks with live secret patterns",
        {"hitCount": removal_count, "kinds": sorted(removal_kinds)},
    )

    sensitive = [
        f
        for f in files
        if re.search(r"appsettings.*\.json", f, re.I)
        or f.endswith(".env")
        or "secret" in f.lower()
    ]
    log(
        "B",
        "adr009-diff-scan.py:paths",
        "Changed files matching sensitive path rules",
        {
            "paths": sensitive,
            "appsettingsJson": any(
                f.endswith("/appsettings.json") or f.endswith("appsettings.json")
                for f in files
            ),
            "appsettingsExample": any("example" in f and "appsettings" in f for f in files),
        },
    )

    otp_added = sum(
        1
        for line in diff.splitlines()
        if line.startswith("+")
        and not line.startswith("+++")
        and re.search(r'"DevOtpCode"\s*:\s*"\d{4,8}"', line)
    )
    log(
        "C",
        "adr009-diff-scan.py:otp",
        "Added numeric DevOtpCode literals",
        {"hitCount": otp_added},
    )

    other_files: set[str] = set()
    current = None
    for line in diff.splitlines():
        if line.startswith("diff --git"):
            current = line.split(" b/")[-1]
            continue
        if (
            line.startswith("+")
            and not line.startswith("+++")
            and current
            and "appsettings" not in current
            and ("Password=" in line or '"Password"' in line)
        ):
            other_files.add(current)
    log(
        "D",
        "adr009-diff-scan.py:other-password",
        "Non-appsettings added Password patterns",
        {"files": sorted(other_files), "fileCount": len(other_files)},
    )

    plus_live = 0
    for line in diff.splitlines():
        if not line.startswith("+") or line.startswith("+++"):
            continue
        if re.search(r"AIzaSy[A-Za-z0-9_-]{20,}", line):
            plus_live += 1
        elif (
            re.search(r"Password=[^*_\s\"<][^;\"]{6,}", line)
            and "__SET_" not in line
            and "unit-test" not in line
            and "NotContain" not in line
            and "Tracked file contains" not in line
        ):
            plus_live += 1
    log(
        "E",
        "adr009-diff-scan.py:plus-live",
        "Added-line live secret residues (strict)",
        {"hitCount": plus_live},
    )
    # #endregion

    print(json.dumps({"ok": True, "log": str(LOG), "hypotheses": "A-E"}, indent=2))


if __name__ == "__main__":
    main()
