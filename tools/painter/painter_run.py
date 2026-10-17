"""Run a Python script inside a live Substance 3D Painter session.

Painter exposes POST /run.json on localhost:60041 when it is launched with
--enable-remote-scripting (C:\\Users\\mikew\\substance_painter_mcp\\launch-painter-remote.bat).
The endpoint evaluates one base64 Python *expression*, so this runner wraps the
script: it runs in a fresh globals dict that holds ARGS (from --arg / --args-json),
and the script's RESULT variable comes back as JSON. No dependency on the MCP
package: this works when the substance-painter MCP server is not loaded.

    python tools/painter/painter_run.py tools/painter/painter_new_project.py \\
        --arg low=E:/.../carn_dum_town_low.fbx --arg high=E:/.../carn_dum_town_high.fbx

--arg values are JSON when they parse as JSON (numbers, lists), else strings.
Exit 0 and the RESULT JSON on stdout; exit 1 and Painter's error otherwise. A Painter-side
script fails by raising (RESULT is data), and painter_run then exits 1.
Workflow: .claude/skills/prep-tripo-prop/SKILL.md.
"""
from __future__ import annotations

import argparse
import base64
import http.client
import json
import sys
from pathlib import Path


def b64(text: str) -> str:
    return base64.b64encode(text.encode("utf-8")).decode("ascii")


def wrap(script: str, args: dict) -> str:
    return (
        "__import__('json').dumps((lambda g: (exec(__import__('base64').b64decode("
        f"{b64(script)!r}).decode('utf-8'), g), g.get('RESULT'))[1])("
        "{'ARGS': __import__('json').loads(__import__('base64').b64decode("
        f"{b64(json.dumps(args))!r}).decode('utf-8'))}}), default=str)"
    )


def parse_arg(text: str) -> tuple[str, object]:
    key, _, value = text.partition("=")
    try:
        return key, json.loads(value)
    except json.JSONDecodeError:
        return key, value


def main(argv=None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("script", type=Path)
    ap.add_argument("--arg", action="append", default=[], help="key=value (repeatable)")
    ap.add_argument("--args-json", default="{}", help="ARGS as one JSON object")
    ap.add_argument("--timeout", type=float, default=3600.0)
    ap.add_argument("--port", type=int, default=60041)
    opts = ap.parse_args(argv)

    args = json.loads(opts.args_json)
    args.update(parse_arg(a) for a in opts.arg)
    body = json.dumps({"python": b64(wrap(opts.script.read_text(encoding="utf-8"), args))})
    # Connecting is bounded on its own: on Windows a refused connection can burn the whole timeout
    # and raise TimeoutError, which must still read as "cannot reach", not as a busy Painter.
    conn = http.client.HTTPConnection("localhost", opts.port, timeout=min(opts.timeout, 10.0))
    try:
        conn.connect()
    except OSError as e:
        print(f"ERROR cannot reach Painter on :{opts.port} (launched with "
              f"--enable-remote-scripting?): {e}")
        return 1
    conn.sock.settimeout(opts.timeout)
    try:
        conn.request("POST", "/run.json", body=body.encode("utf-8"),
                     headers={"Content-Type": "application/json"})
        raw = conn.getresponse().read().decode("utf-8")
    except TimeoutError:
        print(f"ERROR no answer within {opts.timeout:g} s: the script may still be running in "
              "Painter; wait for Painter to go idle before running another")
        return 1
    except OSError as e:
        print(f"ERROR connection to Painter lost on :{opts.port}: {e}")
        return 1
    finally:
        conn.close()

    outer = json.loads(raw) if raw.strip() else None
    if not isinstance(outer, str):
        # success is always a JSON string (the wrapper's json.dumps); Painter
        # reports a script exception as an {"error"|"description": ...} dict
        detail = (outer.get("error") or outer.get("description") or outer
                  if isinstance(outer, dict) else outer)
        print(f"ERROR {detail}")
        return 1
    print(json.dumps(json.loads(outer), indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
