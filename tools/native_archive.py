#!/usr/bin/env python
"""Archive each installed native engine build (#1004).

`TaleWorlds.Native.dll` is the only copy of an engine build's native code, and a Steam update
replaces it. `native_build_match.py --old` and crash triage against an older build need the old
binary, so this keeps one copy of every build, plus the two caches that make it usable: the
engine-method map and the matcher index.

  python tools/native_archive.py [--detach] [--status]
        [--game-dir DIR] [--archive-dir DIR]

The foreground check is stat-only and silent when nothing changed. With --detach (the
SessionStart hook's call) changed DLLs are archived by a detached child and the call returns at
once. Nothing is ever overwritten or deleted in the archive. Always exits 0, except an argparse
error (2): a failure here is advice, never a block.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

DEFAULT_GAME_DIR = "E:/Steam/steamapps/common/Mount & Blade II Bannerlord"
DEFAULT_ARCHIVE_DIR = "E:/Decompiled_Bannerlord/_native"
BUILDS = (("client", "Win64_Shipping_Client"), ("wEditor", "Win64_Shipping_wEditor"))
STATE_NAME = ".native-archive-state.json"
LOCK_NAME = ".native-archive.lock"
LOG_NAME = "native-archive.log"
LOCK_STALE_SECONDS = 1800
LOG_CAP = 1_000_000
REASON_MAX = 200
VERSION_RE = re.compile(r"v\d+\.\d+\.\d+")  # the same expression session-start.sh greps


class Build:
    def __init__(self, tag: str, folder: str, game: Path):
        self.tag = tag
        self.dll = game / "bin" / folder / "TaleWorlds.Native.dll"
        self.key = self.dll.as_posix()

    def version(self) -> str:
        try:
            text = (self.dll.parent / "Version.xml").read_text(encoding="utf-8-sig", errors="replace")
        except OSError:
            return "unknown"
        m = VERSION_RE.search(text)
        return m.group(0) if m else "unknown"


def _reason(exc: BaseException) -> str:
    text = " ".join(str(exc).split()) or type(exc).__name__
    return text[:REASON_MAX]


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# --- state ---------------------------------------------------------------------------------

def load_state(archive: Path) -> dict:
    try:
        data = json.loads((archive / STATE_NAME).read_text(encoding="utf-8"))
        return data if isinstance(data, dict) else {}
    except (OSError, ValueError):
        return {}


def save_state(archive: Path, state: dict) -> None:
    path = archive / STATE_NAME
    partial = path.with_name(path.name + ".tmp")
    partial.write_text(json.dumps(state, indent=1, sort_keys=True), encoding="utf-8")
    os.replace(partial, path)  # a killed run leaves the old state or none, never half


def _entry(state: dict, build: Build):
    e = state.get(build.key)
    return e if isinstance(e, dict) else None


def _stat_matches(entry, dll: Path) -> bool:
    if not entry:
        return False
    try:
        st = os.stat(dll)
    except OSError:
        return False
    return entry.get("size") == st.st_size and entry.get("mtime_ns") == st.st_mtime_ns


def _failed_steps(entry) -> list:
    return [(step, str(entry.get(step))[len("failed: "):]) for step in ("map", "index")
            if str(entry.get(step, "")).startswith("failed: ")]


def _needs_work(entry, dll: Path) -> bool:
    return (not _stat_matches(entry, dll)
            or entry.get("map") != "ok" or entry.get("index") != "ok")


# --- lock ----------------------------------------------------------------------------------

def _lock_is_fresh(archive: Path) -> bool:
    try:
        return time.time() - (archive / LOCK_NAME).stat().st_mtime < LOCK_STALE_SECONDS
    except OSError:
        return False


def acquire_lock(archive: Path, _retry: bool = True) -> bool:
    """One archive run at a time across sessions. A lock older than LOCK_STALE_SECONDS is an
    abandoned run (a killed child) and is taken over."""
    lock = archive / LOCK_NAME
    try:
        fd = os.open(lock, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        if _lock_is_fresh(archive) or not _retry:
            return False
        try:
            lock.unlink()
        except OSError:
            return False
        return acquire_lock(archive, _retry=False)
    except OSError:
        return False
    with os.fdopen(fd, "w") as fh:
        fh.write(f"{os.getpid()} {time.time():.0f}\n")
    return True


def release_lock(archive: Path) -> None:
    try:
        (archive / LOCK_NAME).unlink()
    except OSError:
        pass


# --- the archive ---------------------------------------------------------------------------

def _find_archived(archive: Path, size: int, sha: str):
    """The name of an archive file with this content, under any name, or None."""
    for path in sorted(archive.glob("TaleWorlds.Native.*.dll")):
        try:
            if path.stat().st_size == size and _sha256(path) == sha:
                return path.name
        except OSError:
            continue
    return None


def _archive_name(archive: Path, build: Build, version: str, sha: str) -> str:
    plain = f"TaleWorlds.Native.{version}.{build.tag}.dll"
    if not (archive / plain).exists():
        return plain
    return f"TaleWorlds.Native.{version}.{build.tag}.{sha[:16]}.dll"


def archive_copy(archive: Path, build: Build, version: str, size: int, sha: str):
    """(archive file name holding this DLL's bytes, whether it was copied now). Copies when no
    file has them; never replaces an existing file."""
    have = _find_archived(archive, size, sha)
    if have:
        return have, False
    name = _archive_name(archive, build, version, sha)
    target = archive / name
    if target.exists():
        return name, False  # only a 16-hex-digit collision lands here; never overwrite it
    partial = archive / (name + ".tmp")
    shutil.copyfile(build.dll, partial)
    os.replace(partial, target)
    return name, True


def _build_map(installed: Path):
    import native_decompile as nd
    import native_engine_methods as nem
    built = nem.load_or_build(installed, nd.DEFAULT_PROJECT_DIR, nd.cache_key(installed))
    if built.problems:
        raise RuntimeError(f"{len(built.problems)} unresolved: {built.problems[0]}")


def _build_index(archived: Path):
    import native_build_match as nbm
    import native_decompile as nd
    nbm.load_index(archived, nd.DEFAULT_PROJECT_DIR, True)


def _step(label: str, fn, arg: Path, log) -> str:
    try:
        fn(arg)
    except Exception as exc:  # a map or index failure never undoes the copy
        reason = _reason(exc)
        log(f"{label} failed: {reason}")
        return f"failed: {reason}"
    log(f"{label} ok")
    return "ok"


def process(build: Build, archive: Path, state: dict, log) -> None:
    version = build.version()
    st = os.stat(build.dll)
    entry = _entry(state, build)
    same = _stat_matches(entry, build.dll)
    archived_as = entry.get("archived_as") if same else None
    if not (archived_as and (archive / archived_as).is_file()):
        sha = _sha256(build.dll)
        archived_as, copied = archive_copy(archive, build, version, st.st_size, sha)
        log(f"{version} {build.tag}: " + ("copied as " if copied else "already archived as ")
            + archived_as)
    else:
        sha = entry["sha16"]
        log(f"{version} {build.tag}: already archived as {archived_as}")
    entry = {"size": st.st_size, "mtime_ns": st.st_mtime_ns, "sha16": sha[:16],
             "version": version, "tag": build.tag, "archived_as": archived_as,
             "map": entry.get("map", "pending") if same else "pending",
             "index": entry.get("index", "pending") if same else "pending"}
    state[build.key] = entry
    save_state(archive, state)  # the copy is safe on disk before the slow steps start
    if entry["map"] != "ok":
        entry["map"] = _step(f"{version} {build.tag} map", _build_map, build.dll, log)
        save_state(archive, state)
    if entry["index"] != "ok":
        entry["index"] = _step(f"{version} {build.tag} index", _build_index,
                               archive / archived_as, log)
        save_state(archive, state)


def work(builds: list, archive: Path) -> int:
    def log(msg: str) -> None:
        print(f"{time.strftime('%Y-%m-%d %H:%M:%S')} {msg}", flush=True)

    if not acquire_lock(archive):
        return 0
    try:
        state = load_state(archive)
        for build in builds:
            if not _needs_work(_entry(state, build), build.dll):
                continue
            try:
                process(build, archive, state, log)
            except Exception as exc:
                log(f"{build.version()} {build.tag}: archive step failed: {_reason(exc)}")
    finally:
        release_lock(archive)
    return 0


# --- detach --------------------------------------------------------------------------------

def _spawn_detached(cmd: list, log: Path, cwd: Path) -> None:
    try:  # one run per changed session, so keep the log bounded: truncate it past LOG_CAP
        if log.stat().st_size > LOG_CAP:
            log.write_bytes(b"")
    except OSError:
        pass
    kwargs = {}
    if os.name == "nt":
        kwargs["creationflags"] = (getattr(subprocess, "DETACHED_PROCESS", 0x8)
                                   | getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0x200))
    else:
        kwargs["start_new_session"] = True
    with open(log, "ab") as sink:
        subprocess.Popen(cmd, stdin=subprocess.DEVNULL, stdout=sink, stderr=subprocess.STDOUT,
                         cwd=cwd, close_fds=True, **kwargs)


def detach(builds: list, archive: Path, state: dict, game: Path) -> int:
    if _lock_is_fresh(archive):
        return 0  # another session is archiving
    fresh, failed = [], []
    for b in builds:
        entry = _entry(state, b)
        if not _stat_matches(entry, b.dll):
            fresh.append(b)
        elif _needs_work(entry, b.dll):
            failed.append((b, _failed_steps(entry)))
    for b, steps in failed:
        for step, reason in steps:
            print(f"NOTE: native archive: {b.version()} {b.tag} {step} failed: {reason} "
                  "(see the log)")
    log = archive / LOG_NAME
    cmd = [sys.executable, str(Path(__file__).resolve()), "--game-dir", str(game),
           "--archive-dir", str(archive)]
    try:
        _spawn_detached(cmd, log, archive)
    except OSError as exc:
        print(f"NOTE: native archive UNCHECKED: could not start the background run ({_reason(exc)}).")
        return 0
    if fresh:
        what = ", ".join(f"{b.version()} {b.tag}" for b in fresh)
        print(f"native archive: archiving {what} in the background (log: {log.as_posix()})")
    return 0


# --- entry point ---------------------------------------------------------------------------

def status(archive: Path) -> int:
    state = load_state(archive)
    rows = sorted((e for e in state.values() if isinstance(e, dict)),
                  key=lambda e: (str(e.get("version")), str(e.get("tag"))))
    if not rows:
        print(f"native archive: nothing archived yet ({archive.as_posix()})")
    for e in rows:
        print(f"{e.get('version')} {e.get('tag')}: {e.get('archived_as')} "
              f"sha16={e.get('sha16')} map={e.get('map')} index={e.get('index')}")
    return 0


def run(args) -> int:
    archive = Path(args.archive_dir)
    if args.status:
        return status(archive)
    game = Path(args.game_dir)
    builds = [Build(tag, folder, game) for tag, folder in BUILDS]
    builds = [b for b in builds if b.dll.is_file()]
    if not builds:
        return 0  # the hook already notes a missing install
    try:
        archive.mkdir(parents=True, exist_ok=True)
    except OSError as exc:
        print(f"NOTE: native archive UNCHECKED: {_reason(exc)}")
        return 0
    state = load_state(archive)
    if not any(_needs_work(_entry(state, b), b.dll) for b in builds):
        return 0
    return detach(builds, archive, state, game) if args.detach else work(builds, archive)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Archive each installed native engine build.")
    ap.add_argument("--game-dir", default=os.environ.get("BANNERLORD_GAME_DIR") or DEFAULT_GAME_DIR)
    ap.add_argument("--archive-dir",
                    default=os.environ.get("TAOM_NATIVE_ARCHIVE") or DEFAULT_ARCHIVE_DIR)
    ap.add_argument("--detach", action="store_true",
                    help="start the work in a detached child and return at once")
    ap.add_argument("--status", action="store_true", help="one line per archived build")
    args = ap.parse_args(argv)
    try:
        return run(args)
    except Exception as exc:  # fail open, but say so: silence would read as "archived"
        print(f"NOTE: native archive UNCHECKED: {_reason(exc)}")
        return 0


if __name__ == "__main__":
    sys.exit(main())
