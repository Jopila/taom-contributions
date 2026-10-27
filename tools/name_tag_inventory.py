#!/usr/bin/env python3
"""Inventory of the name keys that carry grammar tags (#1023), by category, with their BR and IT rows.

Categories and where their keys come from (all read only):

    kingdom        taom_spkingdoms.xml and spkingdoms.xslt: name, short_name, title
    minor_faction  spclans.xslt: name and short_name of every template whose key is not TAOM_clan_*
    clan           characters/clans.xml name, spclans.xslt TAOM_clan_* names, every culture clan_names entry
    culture        taom_spcultures.xml and spcultures.xslt: the culture's own name
    settlement     TAOM_Map settlements.xml name keys (the live install, read only), or, without an install,
                   the Settlements.Settlement.name.* keys of the translation cache
    lore           the four {=taom_namegiving_lore_*} town names of Main/Features/Namegiving/LoreNames.cs

A kingdom's ruler_title and text, a culture's person-name lists and every description are not names, so
they are not inventoried. Read only: the one file it writes is the --json report it is asked for.

    python tools/name_tag_inventory.py            counts per category and language, and the live names
                                                  missing from the translation cache
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter
from dataclasses import dataclass
from pathlib import Path

import name_tags
from _gamedir import DEFAULT_GAME_ROOT, game_modules

REPO_ROOT = Path(__file__).resolve().parent.parent
MODULE_DATA = REPO_ROOT / "Main" / "_Module" / "ModuleData"
LANGUAGE_DIR = MODULE_DATA / "Languages"
OVERRIDES_DIR = REPO_ROOT / "tools" / "translation_overrides"
CACHE_DIR = REPO_ROOT / "tools" / "translation_cache"
SETTLEMENT_NAME_PREFIX = "Settlements.Settlement.name."
CATEGORIES = ("kingdom", "minor_faction", "clan", "culture", "settlement", "lore")
LORE_NAMES = REPO_ROOT / "Main" / "Features" / "Namegiving" / "LoreNames.cs"

# name="{=KEY}English" or, inside an XSLT literal result element, name="{{=KEY}}English".
_INLINE = re.compile(r'\b(name|short_name|title)="\{\{?=([^}]+)\}\}?([^"]*)"')
_XSL_ATTR = re.compile(r'<xsl:attribute name="(name|short_name)">\{=([^}]+)\}([^<]*)</xsl:attribute>')
_CULTURE_OPEN = re.compile(r'<Culture\b[^>]*?\bname="\{=([^}]+)\}([^"]*)"', re.S)
_CLAN_NAMES_BLOCK = re.compile(r"<clan_names>(.*?)</clan_names>", re.S)
_LIST_ENTRY = re.compile(r'name="\{\{?=([^}]+)\}\}?([^"]*)"')
_LORE_NAME = re.compile(r'"\{=(taom_namegiving_lore_[a-z_]+)\}([^"]*)"')
_SETTLEMENT_NAME = re.compile(r'\bname="\{=(' + re.escape(SETTLEMENT_NAME_PREFIX) + r'[^}]+)\}([^"]*)"')


@dataclass(frozen=True)
class NameKey:
    id: str
    category: str
    attr: str
    english: str


def _read(path: Path) -> str:
    return Path(path).read_text(encoding="utf-8-sig")


def collect_repo_names(module_data: Path = MODULE_DATA):
    """Kingdom, minor faction, clan and culture names declared in the repo's data XML. First id wins."""
    found: dict[str, NameKey] = {}

    def add(sid, category, attr, english):
        found.setdefault(sid, NameKey(sid, category, attr, english))

    for fname in ("taom_spkingdoms.xml", "spkingdoms.xslt"):
        for attr, sid, english in _INLINE.findall(_read(module_data / fname)):
            add(sid, "kingdom", attr, english)

    for attr, sid, english in _INLINE.findall(_read(module_data / "characters" / "clans.xml")):
        add(sid, "clan", attr, english)

    for attr, sid, english in _XSL_ATTR.findall(_read(module_data / "spclans.xslt")):
        add(sid, "clan" if sid.startswith("TAOM_clan_") else "minor_faction", attr, english)

    cultures = _read(module_data / "taom_spcultures.xml")
    for sid, english in _CULTURE_OPEN.findall(cultures):
        add(sid, "culture", "name", english)
    xslt = _read(module_data / "spcultures.xslt")
    for _attr, sid, english in _XSL_ATTR.findall(xslt):
        add(sid, "culture", "name", english)
    for block in _CLAN_NAMES_BLOCK.findall(cultures) + _CLAN_NAMES_BLOCK.findall(xslt):
        for sid, english in _LIST_ENTRY.findall(block):
            add(sid, "clan", "clan_names", english)

    return list(found.values())


def collect_lore_names(path: Path = LORE_NAMES):
    """The town names a player can give a settlement from the Namegiving feature's lore list. They live in the repo, so no install is needed."""
    return [NameKey(sid, "lore", "town", english) for sid, english in _LORE_NAME.findall(_read(path))]


def settlement_kind(sid: str) -> str:
    """town, castle, village, castle_village, hideout or other, from a settlement id (prefix optional)."""
    sid = sid.removeprefix(SETTLEMENT_NAME_PREFIX)
    for kind in ("castle_village", "town", "castle", "village", "hideout"):
        if sid.startswith(kind):
            return kind
    return "other"


def collect_settlement_names(settlements_xml, cache_ids):
    """Settlement name keys: from the live settlements.xml when given, else from the cache's keys.

    With the install present the cache is not consulted: a cache key the map no longer declares is stale."""
    found: dict[str, NameKey] = {}
    if settlements_xml is not None:
        for sid, english in _SETTLEMENT_NAME.findall(_read(settlements_xml)):
            found.setdefault(sid, NameKey(sid, "settlement", settlement_kind(sid), english))
    if settlements_xml is None:
        for sid in cache_ids:
            if sid.startswith(SETTLEMENT_NAME_PREFIX):
                found.setdefault(sid, NameKey(sid, "settlement", settlement_kind(sid), ""))
    return list(found.values())


def language_rows(lang: str, language_dir: Path = LANGUAGE_DIR) -> dict:
    """{id: text} over every std_taom_*.xml of one language, unescaped; a later file wins."""
    rows: dict[str, str] = {}
    for f in sorted((Path(language_dir) / lang).glob("std_taom_*.xml")):
        for el in ET.parse(f).getroot().iter("string"):
            rows[el.get("id")] = el.get("text") or ""
    return rows


def resolved_rows(names, overrides: dict, cache: dict) -> dict:
    """What rebuild_translation_files.py would write: override, else cache, else the English default."""
    return {n.id: overrides.get(n.id) or cache.get(n.id) or n.english for n in names}


def reconcile_live(live_ids, cache: dict):
    """Live ids the translation cache lacks. The rebuild writes the English default for these."""
    return [sid for sid in live_ids if sid not in cache]


def equal_to_english(keys, rows: dict) -> int:
    """How many of `keys` have a row that is still their English text once its grammar tag is set aside."""
    return sum(1 for n in keys if rows.get(n.id) and name_tags.strip_leading_tags(rows[n.id]) == n.english)


def live_settlement_rows(modules: Path, lang: str) -> dict:
    """{id: text} of the live TAOM_Map loc_settlements.xml for one language, each text without its grammar tag.

    Settlement tags are judged on this text, because it is what the apply tags. Empty when the file is absent.
    """
    f = modules / "TAOM_Map" / "ModuleData" / "Languages" / lang / "loc_settlements.xml"
    if not f.exists():
        return {}
    return {el.get("id"): name_tags.strip_leading_tags(el.get("text") or "")
            for el in ET.parse(f).getroot().iter("string") if el.get("id")}


def load_json(path: Path) -> dict:
    if not path.exists():
        return {}
    data = json.loads(path.read_text(encoding="utf-8"))
    return {k: v for k, v in data.items() if not k.startswith("_")}


def live_settlements_xml(modules: Path) -> Path | None:
    f = modules / "TAOM_Map" / "ModuleData" / "settlements.xml"
    return f if f.exists() else None


def build(modules: Path | None = None):
    """(names, cache_by_lang, overrides_by_lang, settlements_xml) for the whole inventory."""
    settlements_xml = live_settlements_xml(modules) if modules else None
    cache = {lang: load_json(CACHE_DIR / f"{lang.lower()}.json") for lang in name_tags.TAGGED_LANGUAGES}
    overrides = {lang: load_json(OVERRIDES_DIR / f"{lang.lower()}.json") for lang in name_tags.TAGGED_LANGUAGES}
    names = collect_repo_names()
    names += collect_lore_names()
    names += collect_settlement_names(settlements_xml, [i for c in cache.values() for i in c])
    return names, cache, overrides, settlements_xml


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--no-install", action="store_true", help="ignore the game install (settlements come from the cache)")
    p.add_argument("--json", metavar="PATH", help="also write the full inventory to PATH")
    args = p.parse_args(argv)

    modules = None if args.no_install else game_modules(DEFAULT_GAME_ROOT)
    if modules is not None and not modules.exists():
        modules = None
    names, cache, overrides, settlements_xml = build(modules)
    print(f"Settlement source: {settlements_xml if settlements_xml else 'translation cache keys (no install)'}")

    by_cat = Counter(n.category for n in names)
    print("\nName keys by category:")
    for cat in CATEGORIES:
        print(f"  {cat:14} {by_cat.get(cat, 0):5}")
    print(f"  {'total':14} {len(names):5}")

    repo_rows = {lang: language_rows(lang) for lang in name_tags.TAGGED_LANGUAGES}
    print("\nRows present per category and language (repo language files; settlements: override/cache/English):")
    out = {"names": [n.__dict__ for n in names], "rows": {}}
    for lang in name_tags.TAGGED_LANGUAGES:
        settle = [n for n in names if n.category == "settlement"]
        rows = dict(repo_rows[lang])
        rows.update(resolved_rows(settle, overrides[lang], cache[lang]))
        out["rows"][lang] = rows
        for cat in CATEGORIES:
            keys = [n for n in names if n.category == cat]
            have = sum(1 for n in keys if rows.get(n.id))
            same = equal_to_english(keys, rows)
            print(f"  {lang} {cat:14} keys={len(keys):5} with a row={have:5} equal to English={same:5}")

    if settlements_xml is not None:
        live_ids = [n.id for n in names if n.category == "settlement"]
        for lang in name_tags.TAGGED_LANGUAGES:
            missing = reconcile_live(live_ids, cache[lang])
            print(f"\n{lang}: {len(missing)} live settlement name(s) missing from tools/translation_cache/{lang.lower()}.json:")
            for sid in missing:
                print(f"  {sid}")

    if args.json:
        Path(args.json).write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
