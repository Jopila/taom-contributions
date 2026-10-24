#!/usr/bin/env python3
"""Mine this project's Claude Code session transcripts for evidence about the AI harness.

The evidence step of the weekly harness retro. Read-only, stdlib only, no model calls. It streams
every main and subagent transcript in the window and writes `report.md` and `signals.json` OUTSIDE
the repo (a --out inside --repo is refused).

    python tools/harness_retro.py --since 2026-09-21
    python tools/harness_retro.py --top 30 --out E:/Temp/retro

Exit codes: 0 ok; 2 bad arguments or refused out dir; 3 zero records in the window (UNCHECKED).

The transcript format is internal to Claude Code and can change, so every access is defensive: a
line that does not parse is counted as a parse failure, a parsed value that is not an object is
skipped, and neither is fatal. Secrets are redacted with the patterns of `audit_claude_config.py`
(the named ones and the generic key-equals-value one) before anything is written. Record dates are
UTC and `--since` is a calendar date, so the window edge can be off by the hours of the UTC offset.
"""
import argparse
import json
import re
import sys
import tempfile
from collections import Counter, defaultdict
from datetime import date, datetime, timedelta
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parent
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))

from audit_claude_config import _GENERIC_SECRET, _SECRET_COMPILED  # noqa: E402

# The hooks' command reader, so section 9 reads a command the way the git gates do.
HOOKS_DIR = TOOLS_DIR.parent / ".claude" / "hooks"
if str(HOOKS_DIR) not in sys.path:
    sys.path.insert(0, str(HOOKS_DIR))
sys.dont_write_bytecode = True  # no __pycache__ folder inside .claude/hooks
import _shellwords  # noqa: E402

_VALUE_OPTS = _shellwords.GIT_OPTS_WITH_VALUE | {"-R", "--repo"}   # gh -R owner/repo

DEFAULT_SLUG = "e--repos-TAOM"
RECURRING_SESSIONS = 3
RUNAWAY_LEN = 3
STALE_CARD_DAYS = 21

HOOK_DENY_RE = re.compile(r"^(PreToolUse|PostToolUse):(\S+) hook error: \[([^\]]+)\](.*)", re.S)
CLASSIFIER_PREFIX = "Permission for this action was denied by the Claude Code auto mode classifier"
REJECTION_PREFIX = "The user doesn't want to proceed with this tool use"
COMMAND_NAME_RE = re.compile(r"<command-name>\s*/?([^<\s]+)\s*</command-name>")
HOOK_ID_RE = re.compile(r"^([a-z0-9-]+):")
SCRIPT_RE = re.compile(r"([^\s\"'/\\]+\.(?:sh|py|ps1|js|cmd|bat))")

CORRECTION_RES = [re.compile(p, re.I) for p in (
    r"^\s*(no|nope|stop|wait|wrong|undo|revert|don't|do not|not that)\b",
    r"\bI (said|asked|told you)\b",
    r"\bwhy did you\b",
    r"\byou (didn't|did not|forgot|missed|ignored)\b",
    r"\bthat's (wrong|not what)\b",
)]

_WIN_PATH = re.compile(r"[A-Za-z]:\\[^\s'\"]*")
_POSIX_PATH = re.compile(r"(?<![\w<])/[\w.\-@~+]+(?:/[\w.\-@~+]+)+")
_GUID = re.compile(r"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")
_HEX = re.compile(r"\b[0-9a-fA-F]{8,}\b")
_DIGITS = re.compile(r"\d+")
_SPACE = re.compile(r"\s+")
_EXIT_LINE = re.compile(r"^\s*Exit code \d+\s*\n?")
_ERR_TAG = re.compile(r"</?tool_use_error>")
_NOT_A_FAMILY = {"cd", "chdir", "pushd", "popd", "set-location", "sl", "push-location", "pop-location",
                 "for", "select", "case"}
FAMILY_HEAD = 4096
_SUBCOMMAND_TOOLS = {"git", "dotnet", "gh"}
_SCRIPT_TOOLS = {"python", "py", "pwsh", "powershell", "bash", "sh"}
_INLINE_FLAGS = {"-c", "-command", "-encodedcommand", "-e", "-ec"}
_PWSH_VALUE_OPTS = {"-executionpolicy", "-ep", "-workingdirectory", "-wd", "-windowstyle", "-outputformat",
                    "-inputformat", "-configurationname"}


def _redact_generic(m):
    whole, start = m.group(0), m.start(0)
    return whole[:m.start(2) - start] + "[REDACTED:secret-generic]" + whole[m.end(2) - start:]


def redact(text):
    for rule_id, rx, _desc in _SECRET_COMPILED:
        text = rx.sub("[REDACTED:%s]" % rule_id, text)
    return _GENERIC_SECRET.sub(_redact_generic, text)


def normalize_signature(text):
    """Stable signature of an error text: same failure, different paths and numbers, same string."""
    text = redact(text or "")
    text = _EXIT_LINE.sub("", text.strip())
    text = _ERR_TAG.sub("", text).strip()
    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]
    if not lines:
        return "(empty result)"
    if "Traceback (most recent call last)" in text:
        line = lines[-1]
    else:
        line = lines[0]
    line = _WIN_PATH.sub("<path>", line)
    line = _POSIX_PATH.sub("<path>", line)
    line = _GUID.sub("<id>", line)
    line = _HEX.sub("<id>", line)
    line = _DIGITS.sub("#", line)
    line = _SPACE.sub(" ", line).strip()
    return line[:100]


def hook_basename(text):
    """`"$DIR"/.claude/hooks/x.sh` -> `x`."""
    name = re.split(r"[\\/]", text.strip())[-1].strip("\"' ")
    return re.sub(r"\.(sh|py|ps1)$", "", name)


def script_of(command):
    found = SCRIPT_RE.findall(command or "")
    if found:
        return found[-1]
    return redact((command or "").strip())[:40] or "(unknown)"


def command_family(command, tool="Bash"):
    """`cd /x && FOO=1 git -C y diff a` -> `git diff`; `python -B tools/x.py` -> `python x.py`.
    The command is read by the git gates' own reader, so `timeout 600 dotnet test` and
    `{ git status; }` count as their command here too (#985; the idea of rtk's command reader,
    docs/reviews/adopt-rtk-2026-10-23.md). The first segment that runs something other than a
    directory change or a loop header names the family."""
    try:
        # The family is the first command, so the head of a long script is enough to read.
        text, _bodies = _shellwords.strip_heredocs(_shellwords.to_posix((command or "")[:FAMILY_HEAD], tool))
        segs = _shellwords.segments(text).split("\n")
    except Exception:  # a reader failure costs one bucket, never the run
        segs = [command or ""]
    for seg in segs:
        words = [w[2] for w in _shellwords._lead_words(seg, 40)]
        k = _shellwords.command_index(words, tool)
        if k >= len(words):
            continue
        name = re.sub(r"\.exe$", "", re.split(r"[\\/]", words[k])[-1].lower())
        if name not in _NOT_A_FAMILY:
            return name + _family_detail(name, words[k + 1:])
    return "(unknown)"


def _family_detail(name, rest):
    """` <subcommand>` for git, dotnet and gh; ` <script basename>` for a script runner, and nothing
    for inline code (`-c`, `bash -lc`, `pwsh -EncodedCommand`, a heredoc), so a script never
    becomes its own bucket."""
    skip = False
    for w in rest:
        lw = w.lower()
        if skip:
            skip = False
        elif name in _SUBCOMMAND_TOOLS:
            if w in _VALUE_OPTS:
                skip = True
            elif not w.startswith("-"):
                return " " + w
        elif name in _SCRIPT_TOOLS:
            if lw in _INLINE_FLAGS or lw.startswith("<") or (name in ("bash", "sh") and re.fullmatch(r"-[a-z]*c[a-z]*", lw)):
                return ""
            if lw in _PWSH_VALUE_OPTS:
                skip = True
            elif not w.startswith("-"):
                return "" if re.search(r"\s", w) else " " + re.split(r"[\\/]", w)[-1]
        else:
            return ""
    return ""


def default_since(repo, today=None):
    today = today or date.today()
    newest = None
    reviews = Path(repo) / "docs" / "reviews"
    if reviews.is_dir():
        for p in reviews.glob("harness-retro-*.md"):
            m = re.fullmatch(r"harness-retro-(\d{4}-\d{2}-\d{2})\.md", p.name)
            if m:
                try:
                    d = date.fromisoformat(m.group(1))
                except ValueError:
                    continue
                if newest is None or d > newest:
                    newest = d
    return newest or (today - timedelta(days=30))


def result_text(content):
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        return "\n".join(b.get("text", "") for b in content
                         if isinstance(b, dict) and isinstance(b.get("text"), str))
    return ""


class Group:
    __slots__ = ("count", "sessions", "sample", "example", "first", "last")

    def __init__(self):
        self.count = 0
        self.sessions = set()
        self.sample = ""
        self.example = ""
        self.first = ""
        self.last = ""

    def add(self, sid, sample="", ts=None):
        self.count += 1
        self.sessions.add(sid)
        if not self.sample and sample:
            self.sample = sample
        if not self.example:
            self.example = sid
        day = (ts or "")[:10]
        if day:
            # The dates let a retro tell a problem a later commit fixed from one still happening.
            self.first = min(self.first or day, day)
            self.last = max(self.last, day)


class Stats:
    def __init__(self):
        self.main_files = 0
        self.subagent_files = 0
        self.records = 0
        self.parse_failures = 0
        self.compactions = 0
        self.newest_date = None
        self.denies = defaultdict(Group)
        self.blocks = defaultdict(Group)
        self.broken = defaultdict(Group)
        self.cancelled = defaultdict(Group)
        self.cancel_timeouts = Counter()
        self.classifier = Counter()
        self.rejections = Counter()
        self.tool_errors = defaultdict(Group)
        self.runaways = []
        self.corr_count = 0
        self.corr_sessions = set()
        self.corr_excerpts = []
        self.skills = Counter()
        self.slash_commands = Counter()
        self.subagent_types = Counter()
        self.models = Counter()
        self.out_tools = defaultdict(lambda: [0, 0, 0])  # tool -> [results, chars, max]
        self.out_families = defaultdict(lambda: [0, 0, 0])


def add_output(table, key, size):
    row = table[key]
    row[0] += 1
    row[1] += size
    row[2] = max(row[2], size)


def classify_error(stats, text, tool, sid, ts=None):
    """Record one is_error tool result. Returns the (tool, signature) key of a plain tool error."""
    m = HOOK_DENY_RE.match(text)
    if m:
        event, hook_tool, bracket, rest = m.groups()
        reason = redact(rest.lstrip(": ").strip())[:100]
        stats.denies[(hook_basename(bracket), event, hook_tool)].add(sid, reason, ts)
        return None
    if text.startswith(CLASSIFIER_PREFIX):
        stats.classifier[tool] += 1
        return None
    if text.startswith(REJECTION_PREFIX):
        stats.rejections[tool] += 1
        return None
    sig = normalize_signature(text)
    stats.tool_errors[(sig, tool)].add(sid, ts=ts)
    return (tool, sig)


def blocking_reason(err):
    """The reason text of a hook_blocking_error, which recent versions nest one dict deep."""
    for _ in range(3):
        if isinstance(err, dict):
            err = err.get("blockingError")
    return err if isinstance(err, str) else ""


def handle_attachment(stats, att, sid, ts=None):
    if not isinstance(att, dict):
        return
    kind = att.get("type")
    if kind == "hook_blocking_error":
        err = blocking_reason(att.get("blockingError"))
        m = HOOK_ID_RE.match(err)
        name = m.group(1) if m else str(att.get("hookName", "(unknown)"))
        stats.blocks[(name, str(att.get("hookEvent", "")))].add(sid, redact(err)[:100], ts)
    elif kind == "hook_non_blocking_error":
        stderr = att.get("stderr")
        first = next((ln for ln in (stderr if isinstance(stderr, str) else "").splitlines()
                      if ln.strip()), "")
        stats.broken[(script_of(str(att.get("command", ""))), normalize_signature(first))].add(sid, ts=ts)
    elif kind == "hook_cancelled":
        cmd = att.get("command")
        label = str(att.get("hookName", "(unknown)"))
        if isinstance(cmd, str) and cmd:
            label += " " + script_of(cmd)
        stats.cancelled[label].add(sid, ts=ts)
        if att.get("timedOut") in (True, "true", "True"):
            # Killed at its registered timeout: a gate in that state failed open (hook-authoring.md).
            stats.cancel_timeouts[label] += 1


def human_text(r):
    msg = r.get("message")
    content = msg.get("content") if isinstance(msg, dict) else None
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        if any(isinstance(b, dict) and b.get("type") == "tool_result" for b in content):
            return None
        parts = [b.get("text", "") for b in content
                 if isinstance(b, dict) and b.get("type") == "text" and isinstance(b.get("text"), str)]
        return "\n".join(parts) if parts else None
    return None


def handle_human(stats, r, sid, ts):
    """Count one user record that carries no tool result. True when a person typed it."""
    if r.get("isSidechain") or r.get("isMeta") or r.get("isCompactSummary"):
        return False
    text = human_text(r)
    if text is None:
        return False
    stripped = text.strip()
    cmd = COMMAND_NAME_RE.search(stripped)
    if cmd:
        # Built-ins such as /model share this tag, so slash commands are counted apart from skills.
        stats.slash_commands[cmd.group(1)] += 1
        return True
    if not stripped or stripped.startswith("<"):
        return False
    if any(rx.search(stripped) for rx in CORRECTION_RES):
        stats.corr_count += 1
        stats.corr_sessions.add(sid)
        stats.corr_excerpts.append((sid, ts or "", _SPACE.sub(" ", redact(stripped))[:160]))
    return True


def handle_assistant(stats, r, tools, count):
    """Map each tool_use id to (tool name, the id of the assistant message that sent it, the shell
    command or None). The family is read only when a result is counted, so records outside the window
    cost no parse."""
    msg = r.get("message")
    content = msg.get("content") if isinstance(msg, dict) else None
    if not isinstance(content, list):
        return
    for b in content:
        if not isinstance(b, dict) or b.get("type") != "tool_use":
            continue
        name = str(b.get("name", "?"))
        inp = b.get("input") if isinstance(b.get("input"), dict) else {}
        if b.get("id"):
            command = str(inp.get("command", ""))[:FAMILY_HEAD] if name in ("Bash", "PowerShell") else None
            tools[b["id"]] = (name, str(msg.get("id") or b["id"]), command)
        if not count:
            continue
        if name == "Skill" and inp.get("skill"):
            stats.skills[str(inp["skill"])] += 1
        elif name in ("Agent", "Task"):
            stats.subagent_types[str(inp.get("subagent_type") or "general-purpose")] += 1
            stats.models[str(inp.get("model") or "(definition)")] += 1


def scan_file(stats, path, since_str):
    tools = {}
    last_ts = None
    # [key, sid, set of assistant message ids]. A run grows by MESSAGES, not by results: parallel
    # calls sent in one message that fail alike are one batch, not a retry loop.
    run = None

    def flush():
        nonlocal run
        if run and len(run[2]) >= RUNAWAY_LEN:
            stats.runaways.append({"session": run[1], "tool": run[0][0],
                                   "signature": run[0][1], "length": len(run[2])})
        run = None

    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                r = json.loads(line)
            except ValueError:
                stats.parse_failures += 1
                continue
            if not isinstance(r, dict):
                continue
            ts = r.get("timestamp")
            if isinstance(ts, str) and ts:
                last_ts = ts
            else:
                ts = last_ts
            rtype = r.get("type")
            in_window = not (ts and ts[:10] < since_str)
            if rtype == "assistant":
                handle_assistant(stats, r, tools, in_window)  # ids are needed even before the window
            if not in_window:
                continue
            stats.records += 1
            if ts and (stats.newest_date is None or ts[:10] > stats.newest_date):
                stats.newest_date = ts[:10]
            sid = str(r.get("sessionId") or path.stem)
            if rtype == "assistant":
                continue
            if rtype == "system":
                if r.get("subtype") == "compact_boundary":
                    stats.compactions += 1
            elif rtype == "attachment":
                handle_attachment(stats, r.get("attachment"), sid, ts)
            elif rtype == "user":
                msg = r.get("message")
                content = msg.get("content") if isinstance(msg, dict) else None
                results = [b for b in content if isinstance(b, dict) and b.get("type") == "tool_result"] \
                    if isinstance(content, list) else []
                if not results and handle_human(stats, r, sid, ts):
                    flush()  # a person answered between the failures: not a runaway
                for b in results:
                    tool, msg_id, command = tools.get(b.get("tool_use_id"), ("?", b.get("tool_use_id"), None))
                    text = result_text(b.get("content"))
                    add_output(stats.out_tools, tool, len(text))
                    if command is not None:
                        add_output(stats.out_families, command_family(command, tool), len(text))
                    if b.get("is_error") is True:
                        key = classify_error(stats, text, tool, sid, ts)
                    else:
                        key = None
                    if key is not None and run and run[0] == key:
                        run[2].add(msg_id)
                    else:
                        flush()
                        if key is not None:
                            run = [key, sid, {msg_id}]
    flush()


def discover(transcripts, since_dt):
    """Return (main, subagent) transcript paths whose mtime is not older than the window."""
    cutoff = (since_dt - timedelta(days=1)).timestamp()

    def fresh(p):
        try:
            return p.stat().st_mtime >= cutoff
        except OSError:
            return False

    main = sorted(p for p in transcripts.glob("*.jsonl") if fresh(p))
    sub = sorted(p for p in transcripts.glob("*/subagents/agent-*.jsonl") if fresh(p))
    return main, sub


def settings_hook_scripts(repo):
    """(basename without extension, file name) of every PreToolUse and Stop script in settings.json."""
    path = Path(repo) / ".claude" / "settings.json"
    names = []
    try:
        hooks = json.loads(path.read_text(encoding="utf-8-sig")).get("hooks", {})
    except (OSError, ValueError, AttributeError):
        return names
    for event in ("PreToolUse", "Stop"):
        for entry in hooks.get(event, []) if isinstance(hooks, dict) else []:
            for h in entry.get("hooks", []) if isinstance(entry, dict) else []:
                cmd = h.get("command") if isinstance(h, dict) else None
                if isinstance(cmd, str) and SCRIPT_RE.search(cmd):
                    item = (hook_basename(script_of(cmd)), script_of(cmd))
                    if item not in names:
                        names.append(item)
    return names


_ASK_RE = re.compile(r"permissionDecision\W+ask\b")
# Every other way a TAOM gate stops a call: a deny decision, exit 2, a Stop block (directly or
# through _stop_reminder.sh).
_STOPS_RE = re.compile(r"permissionDecision\W+deny\b|\bexit 2\b|_stop_reminder|\"block\"")


def is_ask_only(repo, filename):
    """True for a gate whose code can only ask, never deny or block. An approved ask leaves no trace
    in a transcript, so such a gate's zero fires says nothing about it."""
    try:
        text = (Path(repo) / ".claude" / "hooks" / filename).read_text(encoding="utf-8", errors="replace")
    except OSError:
        return False
    code = "\n".join(ln for ln in text.splitlines() if not ln.lstrip().startswith("#"))
    return bool(_ASK_RE.search(code)) and not _STOPS_RE.search(code)


def unused_skills(repo, used):
    root = Path(repo) / ".claude" / "skills"
    if not root.is_dir():
        return []
    return sorted(p.parent.name for p in root.glob("*/SKILL.md") if p.parent.name not in used)


def memory_health(memory, today):
    memory = Path(memory)
    out = {"memory_md_bytes": None, "memory_md_lines": None, "stale_cards": []}
    md = memory / "MEMORY.md"
    if md.is_file():
        data = md.read_bytes()
        out["memory_md_bytes"] = len(data)
        out["memory_md_lines"] = len(data.decode("utf-8", errors="replace").splitlines())
    feats = memory / "features"
    if feats.is_dir():
        for p in sorted(feats.glob("*.md")):
            modified = None
            try:
                head = p.read_text(encoding="utf-8", errors="replace")[:2000]
                fm = re.match(r"---\s*\n(.*?)\n---", head, re.S)
                m = re.search(r"^modified:\s*(\d{4}-\d{2}-\d{2})", fm.group(1), re.M) if fm else None
                if m:
                    modified = date.fromisoformat(m.group(1))
                else:
                    modified = datetime.fromtimestamp(p.stat().st_mtime).date()
            except (OSError, ValueError):
                continue
            age = (today - modified).days
            if age > STALE_CARD_DAYS:
                out["stale_cards"].append({"name": p.name, "age_days": age})
    out["stale_cards"].sort(key=lambda c: -c["age_days"])
    return out


def top_groups(groups, n, keys):
    rows = []
    for key, g in groups.items():
        row = dict(zip(keys, key))
        row.update(count=g.count, sessions=len(g.sessions), sample=g.sample, example=g.example,
                   first_seen=g.first, last_seen=g.last)
        rows.append(row)
    rows.sort(key=lambda r: (-r["sessions"], -r["count"]))
    return rows[:n]


def output_rows(table, label, total, n, shell_total=None):
    ranked = sorted(table.items(), key=lambda kv: -kv[1][1])
    if len(ranked) > n:
        rest = ranked[n:]
        ranked = ranked[:n] + [("(other)", [sum(v[0] for _k, v in rest), sum(v[1] for _k, v in rest),
                                            max(v[2] for _k, v in rest)])]
    rows = []
    for key, (results, chars, biggest) in ranked:
        row = {label: redact(key), "results": results, "chars": chars, "max": biggest,
               "share": round(chars / total, 4) if total else 0,
               "mean": round(chars / results) if results else 0}
        if shell_total is not None:
            row["shell_share"] = round(chars / shell_total, 4) if shell_total else 0
        rows.append(row)
    return rows


def output_size(stats, n):
    total = sum(v[1] for v in stats.out_tools.values())
    shell = sum(v[1] for v in stats.out_families.values())
    return {"total_chars": total,
            "tools": output_rows(stats.out_tools, "tool", total, n),
            "families": output_rows(stats.out_families, "family", total, n, shell)}


def build_signals(stats, args, since, today, main, sub):
    n = args.top
    tool_rows = top_groups(stats.tool_errors, n, ("signature", "tool"))
    for r in tool_rows:
        r["recurring"] = r["sessions"] >= RECURRING_SESSIONS
    fired = {k[0] for k in stats.denies} | {k[0] for k in stats.blocks}
    gates = settings_hook_scripts(args.repo)
    ask_only = [name for name, filename in gates if name not in fired and is_ask_only(args.repo, filename)]
    used = set(stats.skills) | set(stats.slash_commands)
    runaways = sorted(stats.runaways, key=lambda r: -r["length"])[:n]
    return {
        "window": {"since": since.isoformat(), "newest": stats.newest_date, "today": today.isoformat()},
        "sessions": {"main_files": len(main), "subagent_files": len(sub), "records": stats.records,
                     "parse_failures": stats.parse_failures, "compactions": stats.compactions},
        "hook_denies": top_groups(stats.denies, n, ("hook", "event", "tool")),
        "hook_blocks": top_groups(stats.blocks, n, ("hook", "event")),
        "hook_broken": top_groups(stats.broken, n, ("script", "stderr_signature")),
        "hook_cancelled": [{"hook": k, "count": g.count, "timed_out": stats.cancel_timeouts[k],
                            "sessions": len(g.sessions), "first_seen": g.first, "last_seen": g.last,
                            "example": g.example}
                           for k, g in sorted(stats.cancelled.items(), key=lambda kv: -kv[1].count)[:n]],
        "no_fires": [name for name, _f in gates if name not in fired and name not in ask_only],
        "ask_only_gates": ask_only,
        "classifier_denials": dict(stats.classifier),
        "user_rejections": dict(stats.rejections),
        "tool_errors": tool_rows,
        "runaways": runaways,
        "corrections": {"count": stats.corr_count, "sessions": len(stats.corr_sessions),
                        "excerpts": [{"session": s, "timestamp": t, "text": x}
                                     for s, t, x in stats.corr_excerpts[:n]]},
        "usage": {"skills": dict(stats.skills), "slash_commands": dict(stats.slash_commands),
                  "subagent_types": dict(stats.subagent_types), "models": dict(stats.models)},
        "unused_skills": unused_skills(args.repo, used),
        "memory": memory_health(args.memory, today),
        "output_size": output_size(stats, n),
    }


def cell(v):
    return str(v).replace("|", "\\|").replace("\n", " ")


def table(headers, rows):
    if not rows:
        return ["_none_", ""]
    out = ["| " + " | ".join(headers) + " |", "|" + "---|" * len(headers)]
    out += ["| " + " | ".join(cell(c) for c in row) + " |" for row in rows]
    return out + [""]


def render(sig):
    w, s = sig["window"], sig["sessions"]
    L = ["# Harness retro evidence", "",
         "Window %s to %s (generated %s). Evidence only, no verdicts." % (
             w["since"], w["newest"] or "?", w["today"]), "",
         "## 1. Sessions", ""]
    L += table(["Measure", "Value"], [
        ["Main transcripts", s["main_files"]], ["Subagent transcripts", s["subagent_files"]],
        ["Records parsed", s["records"]], ["Parse failures", s["parse_failures"]],
        ["Compactions", s["compactions"]]])
    L += ["## 2. Hook outcomes", "", "### Denies", ""]
    L += table(["Hook", "Event", "Tool", "Count", "Sessions", "Last seen", "Sample reason"],
               [[r["hook"], r["event"], r["tool"], r["count"], r["sessions"], r["last_seen"], r["sample"]]
                for r in sig["hook_denies"]])
    L += ["### Stop and other blocks", ""]
    L += table(["Hook", "Event", "Count", "Sessions", "Last seen", "Sample reason"],
               [[r["hook"], r["event"], r["count"], r["sessions"], r["last_seen"], r["sample"]]
                for r in sig["hook_blocks"]])
    L += ["### Broken hook runs", ""]
    L += table(["Script", "Stderr signature", "Count", "Sessions", "First seen", "Last seen"],
               [[r["script"], r["stderr_signature"], r["count"], r["sessions"], r["first_seen"], r["last_seen"]]
                for r in sig["hook_broken"]])
    L += ["### Cancelled hooks", "", "A timed-out gate was killed and failed open.", ""]
    L += table(["Hook", "Count", "Timed out", "Sessions", "First seen", "Last seen"],
               [[r["hook"], r["count"], r["timed_out"], r["sessions"], r["first_seen"], r["last_seen"]]
                for r in sig["hook_cancelled"]])
    L += ["### No fires in window (rare-event guard? check before removing)", ""]
    L += ["- `%s`" % h for h in sig["no_fires"]] or ["_none_"]
    L += ["", "### Ask-only gates (an approved ask leaves no trace; fires not observable)", ""]
    L += ["- `%s`" % h for h in sig["ask_only_gates"]] or ["_none_"]
    L += ["", "## 3. Permission outcomes", ""]
    tools = sorted(set(sig["classifier_denials"]) | set(sig["user_rejections"]))
    L += table(["Tool", "Classifier denials", "User rejections"],
               [[t, sig["classifier_denials"].get(t, 0), sig["user_rejections"].get(t, 0)] for t in tools])
    L += ["## 4. Tool error signatures", ""]
    L += table(["Signature", "Tool", "Count", "Sessions", "Last seen", "Example session", "Mark"],
               [[r["signature"], r["tool"], r["count"], r["sessions"], r["last_seen"], r["example"],
                 "RECURRING" if r["recurring"] else ""] for r in sig["tool_errors"]])
    L += ["## 5. Runaway loops", ""]
    L += table(["Session", "Tool", "Signature", "Run length"],
               [[r["session"], r["tool"], r["signature"], r["length"]] for r in sig["runaways"]])
    c = sig["corrections"]
    L += ["## 6. User corrections", "",
          "%d corrections across %d sessions." % (c["count"], c["sessions"]), ""]
    L += table(["Session", "Timestamp", "Excerpt"],
               [[e["session"], e["timestamp"], e["text"]] for e in c["excerpts"]])
    u = sig["usage"]
    L += ["## 7. Usage", "", "### Skill invocations (Skill tool)", ""]
    L += table(["Skill", "Count"], sorted(u["skills"].items(), key=lambda kv: -kv[1]))
    L += ["### Slash commands typed (skills and built-ins)", ""]
    L += table(["Command", "Count"], sorted(u["slash_commands"].items(), key=lambda kv: -kv[1]))
    L += ["### Subagent spawns by type", ""]
    L += table(["Type", "Count"], sorted(u["subagent_types"].items(), key=lambda kv: -kv[1]))
    L += ["### Subagent spawns by model", ""]
    L += table(["Model", "Count"], sorted(u["models"].items(), key=lambda kv: -kv[1]))
    L += ["### Skills with zero invocations in window", ""]
    L += ["- `%s`" % x for x in sig["unused_skills"]] or ["_none_"]
    m = sig["memory"]
    L += ["", "## 8. Memory health", "",
          "MEMORY.md: %s bytes, %s lines (numbers only, no pass or fail)." % (
              m["memory_md_bytes"], m["memory_md_lines"]), "",
          "### Feature cards older than %d days" % STALE_CARD_DAYS, ""]
    L += table(["Card", "Age (days)"], [[x["name"], x["age_days"]] for x in m["stale_cards"]])
    o = sig["output_size"]
    L += ["## 9. Tool output size", "",
          "%d characters of tool results (errors included) in the window. Shares are of all results; "
          "shell share is of Bash and PowerShell results." % o["total_chars"], "", "### By tool", ""]
    L += table(["Tool", "Results", "Chars", "Share", "Mean"],
               [[r["tool"], r["results"], r["chars"], "%.1f%%" % (100 * r["share"]), r["mean"]]
                for r in o["tools"]])
    L += ["### By shell command family", ""]
    L += table(["Family", "Results", "Chars", "Share of all", "Share of shell", "Mean", "Max"],
               [[r["family"], r["results"], r["chars"], "%.1f%%" % (100 * r["share"]),
                 "%.1f%%" % (100 * r["shell_share"]), r["mean"], r["max"]] for r in o["families"]])
    return "\n".join(L) + "\n"


def parse_args(argv):
    repo_default = TOOLS_DIR.parent
    home = Path.home() / ".claude" / "projects" / DEFAULT_SLUG
    p = argparse.ArgumentParser(description="Mine Claude Code transcripts for harness evidence.")
    p.add_argument("--transcripts", type=Path, default=home)
    p.add_argument("--since", help="YYYY-MM-DD (default: newest harness-retro file, else 30 days ago)")
    p.add_argument("--repo", type=Path, default=repo_default)
    p.add_argument("--memory", type=Path, default=home / "memory")
    p.add_argument("--out", type=Path, default=None)
    p.add_argument("--top", type=int, default=20)
    return p.parse_args(argv)


def main(argv=None):
    try:
        args = parse_args(argv)
    except SystemExit as e:
        return 2 if e.code else 0
    today = date.today()
    if args.top < 1:
        print("error: --top must be at least 1", file=sys.stderr)
        return 2
    try:
        since = date.fromisoformat(args.since) if args.since else default_since(args.repo, today)
    except ValueError:
        print("error: --since must be YYYY-MM-DD", file=sys.stderr)
        return 2
    if not args.transcripts.is_dir():
        print("error: transcripts directory not found: %s" % args.transcripts, file=sys.stderr)
        return 2
    out = (args.out or Path(tempfile.gettempdir()) / "taom-harness-retro" / today.isoformat()).resolve()
    repo = args.repo.resolve()
    if out == repo or repo in out.parents:
        print("error: --out %s is inside --repo %s; the report must stay outside the repo" % (out, repo),
              file=sys.stderr)
        return 2

    stats = Stats()
    main_files, sub_files = discover(args.transcripts, datetime.combine(since, datetime.min.time()))
    for path in main_files + sub_files:
        try:
            scan_file(stats, path, since.isoformat())
        except OSError as e:
            print("warning: cannot read %s: %s" % (path, e), file=sys.stderr)
    if stats.records == 0:
        print("UNCHECKED: no transcript records parsed in %s since %s" % (args.transcripts, since.isoformat()))
        return 3

    sig = build_signals(stats, args, since, today, main_files, sub_files)
    out.mkdir(parents=True, exist_ok=True)
    (out / "report.md").write_text(render(sig), encoding="utf-8")
    (out / "signals.json").write_text(json.dumps(sig, indent=2), encoding="utf-8")

    s = sig["sessions"]
    print("report:  %s" % (out / "report.md"))
    print("signals: %s" % (out / "signals.json"))
    print("sessions: %d main, %d subagent transcripts, %d records, %d parse failures, %d compactions" % (
        s["main_files"], s["subagent_files"], s["records"], s["parse_failures"], s["compactions"]))
    print("hooks: %d deny rows, %d block rows, %d broken-run rows, %d cancelled, %d with no fires" % (
        len(sig["hook_denies"]), len(sig["hook_blocks"]), len(sig["hook_broken"]),
        len(sig["hook_cancelled"]), len(sig["no_fires"])))
    print("permissions: %d classifier denials, %d user rejections" % (
        sum(sig["classifier_denials"].values()), sum(sig["user_rejections"].values())))
    print("tool errors: %d signature rows, %d RECURRING" % (
        len(sig["tool_errors"]), sum(1 for r in sig["tool_errors"] if r["recurring"])))
    print("runaways: %d" % len(sig["runaways"]))
    print("corrections: %d across %d sessions" % (sig["corrections"]["count"], sig["corrections"]["sessions"]))
    print("usage: %d skills used, %d subagent spawns, %d skills unused" % (
        sum(1 for _ in sig["usage"]["skills"]), sum(sig["usage"]["subagent_types"].values()),
        len(sig["unused_skills"])))
    print("memory: %s bytes, %s lines, %d stale cards" % (
        sig["memory"]["memory_md_bytes"], sig["memory"]["memory_md_lines"], len(sig["memory"]["stale_cards"])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
