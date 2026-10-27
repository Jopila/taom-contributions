#!/usr/bin/env python3
"""Grammar tags on TAOM's Portuguese (BR) and Italian (IT) names (#1023).

The game's own names carry a leading grammar tag ({.NS}, {.FN}, {.MS}, ...) that por_br_functions.xml GENDER and
ItalianTextProcessor read to pick an article or a preposition. TAOM's names had none. tools/translation_name_tags.json
maps (language, id) to a tag; this module adds it to the language files and never to the translation cache.

    tag_row / tag_entries   the step rebuild_translation_files.py and the translator call on rows
    plan_file / write_planned  an in-place pass over one language file: plan it, then write it

The step is idempotent: a row that already starts with its tag is left alone, and a different leading tag is replaced,
never stacked. The table is built by tools/generate_name_tags.py.

    python tools/name_tags.py --lang all                 dry run over the repo language files
    python tools/name_tags.py --lang all --apply         write them (git is their backup)
    python tools/name_tags.py --target live-map          dry run over the live TAOM_Map loc_settlements.xml
    python tools/name_tags.py --target live-map --apply --allow-live-write    write it, backed up beside it first

Every file is planned and checked before the first one is written; any error exits 2 with nothing written.
"""

from __future__ import annotations

import argparse
import json
import re
import shutil
import sys
import xml.etree.ElementTree as ET
from datetime import datetime
from pathlib import Path

from _gamedir import DEFAULT_GAME_ROOT, game_modules

REPO_ROOT = Path(__file__).resolve().parent.parent
TABLE_PATH = REPO_ROOT / "tools" / "translation_name_tags.json"
LANGUAGE_DIR = REPO_ROOT / "Main" / "_Module" / "ModuleData" / "Languages"
TAGGED_LANGUAGES = ("BR", "IT")

LEADING_TAGS = re.compile(r"^(?:\{\.[A-Za-z]+\})+")
STRING_ROW = re.compile(r'(<string\s+id=")([^"]*)("\s+text=")([^"]*)(")')


def load_table(path: Path | None = None) -> dict:
    with open(path if path is not None else TABLE_PATH, encoding="utf-8") as fh:
        return json.load(fh)


def strip_leading_tags(text: str) -> str:
    return LEADING_TAGS.sub("", text, count=1)


def with_tag(text: str, tag: str) -> str:
    """`text` with exactly one leading `tag`: a missing tag is added, a different one replaced."""
    return tag + strip_leading_tags(text)


def head_noun_tag(lang: str, text: str, table: dict):
    """(noun, tag) for the longest head noun or phrase that starts `text`, or None.

    A noun joined to the rest of the name by hyphens ("Prole-das-Rochas") is found through its first segment, after
    the whole first word has been tried: "Uruk-hai" is a noun of its own.
    """
    spec = table["languages"].get(lang)
    if not spec:
        return None
    nouns = {noun.casefold(): (noun, tag) for noun, tag in spec["head_nouns"].items()}
    if not nouns:
        return None
    words = strip_leading_tags(text).split()
    longest = max(len(noun.split()) for noun in nouns)
    for size in range(min(longest, len(words)), 0, -1):
        found = nouns.get(" ".join(words[:size]).casefold())
        if found:
            return found
    if words and "-" in words[0]:
        return nouns.get(words[0].split("-")[0].casefold())
    return None


def tag_for(lang: str, sid: str, table: dict):
    spec = table["languages"].get(lang)
    if not spec:
        return None
    return spec.get("pins", {}).get(sid) or spec["tags"].get(sid)


def tag_row(lang: str, sid: str, text: str, table: dict) -> str:
    """`text` with the table's tag for (lang, sid), or `text` itself when the table has none."""
    tag = tag_for(lang, sid, table)
    return with_tag(text, tag) if tag else text


def leads_with_article(lang: str, text: str, table: dict) -> bool:
    """True when the name (its tag ignored) starts with one of the language's articles: a tag on top would print it twice."""
    spec = table["languages"].get(lang)
    body = strip_leading_tags(text or "").strip()
    if not spec or not body:
        return False
    articles = spec.get("articles", [])
    return body.split()[0] in articles or any(a.endswith("'") and body.startswith(a) for a in articles)


def tag_entries(lang: str, entries, table: dict):
    """[(id, text)] with the table's tag on each name row. Order is kept."""
    if lang not in table["languages"]:
        return list(entries)
    return [(sid, tag_row(lang, sid, text, table)) for sid, text in entries]


def carry_tag(lang: str, sid: str, old_text: str, new_text: str, table: dict) -> str:
    """`new_text` for a row whose text is being rewritten: the table's tag for the id, else the tag the old text carried.

    The live-map writers (Apply-MapVillageNames.py, add_map_villages.py) rewrite or add a row's text; a tag the table
    knows follows the id, and a tag it does not know is kept rather than dropped.
    """
    if lang not in TAGGED_LANGUAGES:
        return new_text
    tag = tag_for(lang, sid, table)
    if tag:
        return with_tag(new_text, tag)
    kept = LEADING_TAGS.match(old_text)
    return with_tag(new_text, kept.group(0)) if kept else new_text


def rename_ids(table: dict, renames: dict) -> int:
    """Rename ids in every language's tags and pins in place; returns how many entries moved. Tags stay sorted by id."""
    moved = 0
    for spec in table["languages"].values():
        for part in ("tags", "pins"):
            entries = spec.get(part)
            if not entries:
                continue
            hit = [old for old in renames if old in entries]
            for old in hit:
                entries[renames[old]] = entries.pop(old)
                moved += 1
            if hit:
                spec[part] = dict(sorted(entries.items()))
    return moved


def dump_table(table: dict) -> str:
    return json.dumps(table, ensure_ascii=False, indent=1) + "\n"


def retag_xml(xml_text: str, lang: str, table: dict):
    """(new_xml, changes) for one language file's text. changes is [(id, old_text, new_text)].

    Only the text="..." of rows the table names is touched, so line endings, indentation and
    escaping stay byte for byte. The tag has no character that needs XML escaping.
    """
    if lang not in table["languages"]:
        return xml_text, []
    changes = []

    def swap(m):
        sid, old = m.group(2), m.group(4)
        tag = tag_for(lang, sid, table)
        if not tag:
            return m.group(0)
        new = with_tag(old, tag)
        if new == old:
            return m.group(0)
        changes.append((sid, old, new))
        return m.group(1) + sid + m.group(3) + new + m.group(5)

    return STRING_ROW.sub(swap, xml_text), changes


def _ids_and_texts(raw: bytes):
    root = ET.fromstring(raw)
    return [(el.get("id"), el.get("text")) for el in root.iter("string")]


def article_led_rows(path: Path, lang: str, table: dict):
    """Ids of the file's table-tagged rows whose name starts with an article: such a row must be rewritten before it is tagged."""
    root = ET.fromstring(Path(path).read_bytes())
    return [el.get("id") for el in root.iter("string")
            if tag_for(lang, el.get("id"), table) and leads_with_article(lang, el.get("text") or "", table)]


def plan_file(path: Path, lang: str, table: dict):
    """(changes, new_bytes) for one language file, checked; nothing is written.

    The source is parsed first and the result is parsed again; a result whose ids differ, or whose text differs by
    more than the leading tag, raises ValueError. `new_bytes` is None when nothing changes.
    """
    raw = Path(path).read_bytes()
    try:
        before = _ids_and_texts(raw)
    except ET.ParseError as exc:
        raise ValueError(f"{path} does not parse ({exc}); nothing written") from exc
    new_text, changes = retag_xml(raw.decode("utf-8"), lang, table)
    if not changes:
        return [], None
    new_raw = new_text.encode("utf-8")
    try:
        after = _ids_and_texts(new_raw)
    except ET.ParseError as exc:
        raise ValueError(f"{path}: the tagged result does not parse ({exc}); nothing written") from exc
    if [i for i, _ in before] != [i for i, _ in after] or any(
            strip_leading_tags(b) != strip_leading_tags(a) for (_, b), (_, a) in zip(before, after)):
        raise ValueError(f"{path}: the tagged result changes more than the leading tag; nothing written")
    return changes, new_raw


def write_planned(path: Path, new_raw: bytes, backup: bool = False, stamp: str | None = None) -> None:
    """Write a planned file. With `backup`, the old bytes are copied beside it as <name>.bak_nametags_<stamp> first."""
    if backup:
        stamp = stamp or datetime.now().strftime("%Y%m%d_%H%M%S")
        shutil.copy2(path, str(path) + f".bak_nametags_{stamp}")
    Path(path).write_bytes(new_raw)


def repo_language_files(lang: str):
    return sorted((LANGUAGE_DIR / lang).glob("std_taom_*.xml"))


def live_map_file(lang: str) -> Path:
    return (game_modules(DEFAULT_GAME_ROOT) / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml")


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--lang", choices=[*TAGGED_LANGUAGES, "all"], default="all")
    p.add_argument("--target", choices=["repo", "live-map"], default="repo")
    p.add_argument("--apply", action="store_true", help="write the files (default: dry run)")
    p.add_argument("--allow-live-write", action="store_true",
                   help="permit --apply on the live TAOM_Map file, which is outside the repo")
    args = p.parse_args(argv)

    if args.apply and args.target == "live-map" and not args.allow_live_write:
        print("REFUSED: --apply on the live TAOM_Map file needs --allow-live-write", file=sys.stderr)
        return 2

    table = load_table()
    langs = TAGGED_LANGUAGES if args.lang == "all" else (args.lang,)
    live = args.target == "live-map"
    plans, errors = [], []
    for lang in langs:
        files = [live_map_file(lang)] if live else repo_language_files(lang)
        if not files:
            errors.append(f"[{lang}] no language file found")
        for f in files:
            if not f.exists():
                errors.append(f"[{lang}] {f} not found")
                continue
            try:
                changes, new_raw = plan_file(f, lang, table)
                article_led = article_led_rows(f, lang, table)
            except (ValueError, ET.ParseError) as exc:
                errors.append(f"[{lang}] {exc}")
                continue
            if article_led:
                message = (f"[{lang}] {f.name}: row(s) still start with an article, rewrite them without it first: "
                           + ", ".join(article_led))
                if args.apply:
                    errors.append(message)
                    continue
                print("  WARNING " + message)
            plans.append((lang, f, changes, new_raw))
    if errors:
        print("\n".join(errors), file=sys.stderr)
        print("Nothing written.", file=sys.stderr)
        return 2

    total = 0
    for lang, f, changes, new_raw in plans:
        if not changes:
            continue
        total += len(changes)
        if args.apply:
            write_planned(f, new_raw, backup=live)
        print(f"  [{lang}] {f.name}: {len(changes)} row(s) {'tagged' if args.apply else 'would be tagged'}")
    print(f"{'Tagged' if args.apply else 'Would tag'} {total} row(s).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
