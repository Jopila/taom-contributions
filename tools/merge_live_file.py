#!/usr/bin/env python
"""Three-way merge of a live (CRLF, unversioned) file with an artist's versions.

Porting an artist's commit means merging the live Armory / TAOM_Map file (CRLF on disk) with
his version from the lotraom-assets mirror (git blobs, LF). A raw `git merge-file` on mixed
endings reports the whole file as one conflict. This tool merges on LF and restores the live
file's terminator, BOM and final-newline state afterwards.

    python tools/merge_live_file.py <live> --base <file|rev:path> --theirs <file|rev:path>
        [--repo E:/repos/lotraom-assets] [--apply] [--tag TAG] [--out PATH]
    python tools/merge_live_file.py --base-search <mirror-path> --theirs <file|rev:path>
        [--repo E:/repos/lotraom-assets]

Dry run by default. `--apply` backs live up as `<live>.bak-<tag or merge>-<timestamp>` (the
suffix goes LAST: the engine globs `*.xml`) and writes the merge. `--out` writes the merge to
another path instead and leaves live untouched.

Exit codes: 0 clean (or base-search done), 1 conflicts (nothing written), 2 refused (the XML
result does not parse, or an input error).

`--base-search` lists the mirror revisions of a path with the size of their diff to theirs,
smallest first. The right base is the revision nearest the artist's file, not his commit's
parent (docs/reviews/lessons/data-content-cultures.md).
"""
import argparse
import datetime
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

BOM = b"\xef\xbb\xbf"
DEFAULT_REPO = "E:/repos/lotraom-assets"
XML_SUFFIXES = {".xml", ".xslt", ".xsl"}
_GIT_SPEC = re.compile(r"^(?![A-Za-z]:[\\/])[^:\\]+:.+")


class MergeError(Exception):
    pass


def detect_style(data):
    """Return (has_bom, terminator, ends_with_newline) for raw bytes. Terminator is dominant."""
    has_bom = data.startswith(BOM)
    body = data[len(BOM):] if has_bom else data
    triple = body.count(b"\r\r\n")
    crlf = body.count(b"\r\n") - triple
    lf = body.count(b"\n") - body.count(b"\r\n")
    best = max((triple, "\r\r\n"), (crlf, "\r\n"), (lf, "\n"), key=lambda t: t[0])
    terminator = best[1] if best[0] else "\n"
    return has_bom, terminator, body.endswith(b"\n")


def to_lf(data):
    """Decode (BOM stripped) and normalise every terminator to LF."""
    text = data.decode("utf-8-sig")
    text = text.replace("\r\r\n", "\n").replace("\r\n", "\n")
    # A missing final newline would read as a change to the last line; live's state is restored later.
    return text if text.endswith("\n") or not text else text + "\n"


def read_source(spec, repo):
    p = Path(spec)
    if p.is_file():
        return p.read_bytes()
    if _GIT_SPEC.match(spec):
        r = subprocess.run(["git", "-C", str(repo), "show", spec], capture_output=True)
        if r.returncode != 0:
            raise MergeError(f"git show {spec} failed: {r.stderr.decode('utf-8', 'replace').strip()}")
        return r.stdout
    raise MergeError(f"{spec}: not a file, and not a <rev>:<path> git spec")


def merge_text(live_lf, base_lf, theirs_lf):
    """Run git merge-file on temp copies. Returns (exit_code, merged_lf_with_markers)."""
    with tempfile.TemporaryDirectory(prefix="merge_live_") as td:
        paths = []
        for name, text in (("live", live_lf), ("base", base_lf), ("theirs", theirs_lf)):
            p = Path(td) / name
            p.write_bytes(text.encode("utf-8"))
            paths.append(str(p))
        r = subprocess.run(
            ["git", "merge-file", "-p", "--diff3", "-L", "live", "-L", "base", "-L", "theirs", *paths],
            capture_output=True,
        )
    if r.returncode < 0 or r.returncode > 127:
        raise MergeError(f"git merge-file failed ({r.returncode}): {r.stderr.decode('utf-8', 'replace').strip()}")
    return r.returncode, r.stdout.decode("utf-8")


def conflict_hunks(merged):
    hunks, cur = [], None
    for line in merged.split("\n"):
        if line.startswith("<<<<<<< "):
            cur = [line]
        elif cur is not None:
            cur.append(line)
            if line.startswith(">>>>>>> "):
                hunks.append("\n".join(cur))
                cur = None
    return hunks


def restore_style(merged_lf, has_bom, terminator, final_newline):
    text = merged_lf
    if text.endswith("\n"):
        text = text[:-1]
    if final_newline:
        text += "\n"
    text = text.replace("\n", terminator)
    return (BOM if has_bom else b"") + text.encode("utf-8")


def summarize(live_lf, base_lf, merged_lf):
    """Approximate, by line multiset: lines the merge took from theirs, and live-only lines kept."""
    m, lv, b = Counter(merged_lf.split("\n")), Counter(live_lf.split("\n")), Counter(base_lf.split("\n"))
    from_theirs = sum((m - lv).values())
    live_only = lv - b
    kept = sum((live_only & m).values())
    return from_theirs, kept


def check_xml(path, data):
    if Path(path).suffix.lower() not in XML_SUFFIXES:
        return None
    try:
        ET.fromstring(data[len(BOM):] if data.startswith(BOM) else data)
    except ET.ParseError as exc:
        return str(exc)
    return None


def do_merge(args):
    repo = args.repo
    live_path = Path(args.live)
    if not live_path.is_file():
        raise MergeError(f"live file not found: {live_path}")
    live_raw = live_path.read_bytes()
    has_bom, terminator, final_nl = detect_style(live_raw)
    live_lf = to_lf(live_raw)
    base_lf = to_lf(read_source(args.base, repo))
    theirs_lf = to_lf(read_source(args.theirs, repo))

    code, merged = merge_text(live_lf, base_lf, theirs_lf)
    if code:
        hunks = conflict_hunks(merged)
        print(f"CONFLICT: {len(hunks)} hunk(s); nothing written.")
        for h in hunks:
            print(h)
            print()
        return 1

    out_bytes = restore_style(merged, has_bom, terminator, final_nl)
    bad = check_xml(live_path, out_bytes)
    if bad:
        print(f"REFUSED: merged result is not well-formed XML ({bad}); nothing written.")
        return 2

    from_theirs, kept = summarize(live_lf, base_lf, merged)
    term = {"\r\r\n": "CR CR LF", "\r\n": "CRLF", "\n": "LF"}[terminator]
    print(
        f"Clean merge: {from_theirs} line(s) from theirs, {kept} live-only line(s) kept "
        f"(approximate, by line); terminator {term}, BOM {'yes' if has_bom else 'no'}, "
        f"final newline {'yes' if final_nl else 'no'}."
    )
    minority = live_raw.count(b"\n") - live_raw.count(terminator.encode("ascii"))
    if minority > 0:
        print(f"NOTE: {minority} line(s) in live do not end in {term}; the result writes every line with {term}.")
    if out_bytes == live_raw:
        print("Result equals live; nothing to change.")
        return 0
    if args.out:
        Path(args.out).write_bytes(out_bytes)
        print(f"Wrote {args.out} ({len(out_bytes)} bytes); live untouched.")
    elif args.apply:
        stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
        bak = live_path.with_name(f"{live_path.name}.bak-{args.tag or 'merge'}-{stamp}")
        if bak.exists():
            raise MergeError(f"backup already exists: {bak}")
        bak.write_bytes(live_raw)
        tmp = live_path.with_name(f"{live_path.name}.merge-tmp")
        tmp.write_bytes(out_bytes)
        os.replace(tmp, live_path)  # a failed write leaves live whole, not truncated
        print(f"Backed up to {bak}\nWrote {live_path} ({len(out_bytes)} bytes).")
    else:
        print("Dry run; pass --apply to back up live and write.")
    return 0


def _numstat(a_lf, b_lf):
    with tempfile.TemporaryDirectory(prefix="merge_live_") as td:
        pa, pb = Path(td) / "a", Path(td) / "b"
        pa.write_bytes(a_lf.encode("utf-8"))
        pb.write_bytes(b_lf.encode("utf-8"))
        r = subprocess.run(["git", "diff", "--no-index", "--numstat", str(pa), str(pb)], capture_output=True, text=True)
    if not r.stdout.strip():
        return 0
    add, dele = r.stdout.split()[:2]
    return (int(add) if add.isdigit() else 0) + (int(dele) if dele.isdigit() else 0)


def do_base_search(args):
    repo = args.repo
    theirs_lf = to_lf(read_source(args.theirs, repo))
    log = subprocess.run(
        ["git", "-C", str(repo), "log", "--all", "--format=%H%x09%cs%x09%s", "--", args.base_search],
        capture_output=True,
        text=True,
    )
    if log.returncode != 0:
        raise MergeError(f"git log failed: {log.stderr.strip()}")
    rows, seen = [], {}
    for line in log.stdout.splitlines():
        rev, date, subject = line.split("\t", 2)
        show = subprocess.run(["git", "-C", str(repo), "show", f"{rev}:{args.base_search}"], capture_output=True)
        if show.returncode != 0:
            continue  # the commit deleted the path
        content = to_lf(show.stdout)
        if content not in seen:
            seen[content] = _numstat(content, theirs_lf)
        rows.append((seen[content], rev, date, subject))
    if not rows:
        print(f"No revisions of {args.base_search} found in {repo}.")
        return 0
    rows.sort(key=lambda r: r[0])
    print(f"{'rev':<10} {'date':<11} {'diff lines':>10}  subject")
    for size, rev, date, subject in rows:
        print(f"{rev[:9]:<10} {date:<11} {size:>10}  {subject}")
    return 0


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("live", nargs="?", help="the live file to merge into")
    ap.add_argument("--base", help="common ancestor: a path or <rev>:<path>")
    ap.add_argument("--theirs", help="the artist's version: a path or <rev>:<path>")
    ap.add_argument("--repo", default=DEFAULT_REPO, help="git repo that resolves <rev>:<path> (default %(default)s)")
    ap.add_argument("--apply", action="store_true", help="back up live and write the merge")
    ap.add_argument("--tag", help="backup tag (default 'merge')")
    ap.add_argument("--out", help="write the merge here instead of touching live")
    ap.add_argument("--base-search", metavar="MIRROR_PATH", help="rank mirror revisions of a path by diff to theirs")
    args = ap.parse_args(argv)
    try:
        if args.base_search:
            if not args.theirs:
                ap.error("--base-search needs --theirs")
            return do_base_search(args)
        if not (args.live and args.base and args.theirs):
            ap.error("live, --base and --theirs are required")
        return do_merge(args)
    except (MergeError, UnicodeDecodeError, OSError) as exc:  # 1 means "conflicts", never a crash
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
