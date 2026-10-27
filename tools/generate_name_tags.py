#!/usr/bin/env python3
"""Build the (language, id) -> grammar tag table for TAOM's Portuguese and Italian names (#1023).

The rules live in tools/translation_name_tags.json itself, next to the tags they produce:

    plain             the proper-noun tag: BR {.NS}, IT {.FN} (what vanilla puts on its towns)
    plain_by_kind     a different plain tag for a settlement kind (castle, town, village...). IT castles take
                      {.MN}: the implied noun is "castello", as "regno" is for a kingdom. A castle led by a head
                      noun ("Castello di X") keeps the noun's tag, and IT towns and villages stay {.FN}
    plain_by_category a different plain tag for a category. IT kingdoms, minor factions and clans take {.MN}:
                      the Italian GENDER code reads MN as masculine and FN as feminine, and the implied
                      noun of a kingdom or clan name ("il regno", "il clan") is masculine
    head_nouns        a name that starts with one of these common nouns takes the noun's own tag;
                      the longest phrase wins ("Alto Reino" before "Alto"), and a noun joined to the
                      rest by hyphens ("Prole-das-Rochas") is found through its first segment
    proper_prefixes   first words of multi-word names that are part of a proper name (Sindarin and
                      Dwarvish elements such as "Barad" or "Bit"): plain
    english_heads     English common nouns; a row still equal to its English text and led by one is
                      untranslated, so it is left for the next translation run. Any other multi-word
                      row equal to its English text is a name kept as written ("Dol Guldur"): plain
    articles          a name led by one of these is left for a human: a tag on top of the article
                      would print it twice. Drop the article from the translation and pin its gender
    exclude           ids that stay untagged on purpose, with the reason (an adjective, not a noun)
    pins              human decisions, id -> tag, applied before every rule
    tags              GENERATED: every id the rules decided

A name no rule decides is not tagged; it is listed (--list-undecided) with the reason, for a human to
check. A human settles it by adding a pin, a head noun or a proper prefix, then rerunning --write.

    python tools/generate_name_tags.py                 summary, no file written
    python tools/generate_name_tags.py --list-undecided
    python tools/generate_name_tags.py --write         rewrite the `tags` of the table
    python tools/generate_name_tags.py --check         exit 1 when the tags are not what the rules produce

Settlement ids come from the live TAOM_Map settlements.xml, and their text from the live loc_settlements.xml (both
read only): a tag is judged on the text the apply will tag. Without the install the settlements are left out:
--check and --write then cover the repo names only, and --write keeps the settlement tags already in the table.
With the install, --write prunes every id the inventory no longer lists and --check reports it.
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter
from dataclasses import dataclass
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import name_tag_inventory as inv  # noqa: E402
import name_tags as nt  # noqa: E402
from _gamedir import DEFAULT_GAME_ROOT, game_modules  # noqa: E402


@dataclass(frozen=True)
class Undecided:
    id: str
    category: str
    text: str
    reason: str


def decide(lang: str, text: str, english: str, spec: dict, pin: str | None = None, category: str | None = None,
           kind: str | None = None):
    """(tag, reason) for one row; tag is None when no rule decides it. `kind` is a settlement's kind (castle, town...)."""
    if pin:
        return pin, "pinned"
    body = nt.strip_leading_tags(text or "").strip()
    if not body:
        return None, "empty"
    words = body.split()
    plain = spec.get("plain_by_kind", {}).get(kind) or spec.get("plain_by_category", {}).get(category, spec["plain"])
    if nt.leads_with_article(lang, body, {"languages": {lang: spec}}):
        return None, "leading article"
    english_heads = {w.casefold() for w in spec.get("english_heads", [])}
    if body == english and len(words) > 1 and words[0].casefold() in english_heads:
        return None, "untranslated"
    head = nt.head_noun_tag(lang, body, {"languages": {lang: spec}})
    if head:
        return head[1], f"head noun {head[0]}"
    if len(words) == 1:
        return plain, "plain"
    if words[0].casefold() in {w.casefold() for w in spec.get("proper_prefixes", [])}:
        return plain, "proper prefix"
    if body == english:
        return plain, "name kept as written"
    return None, f"unknown first word {words[0]}"


def build(table: dict, names, rows_by_lang: dict):
    """({lang: {id: tag}}, {lang: [Undecided]}) for `names` read through `rows_by_lang`."""
    tags, undecided = {}, {}
    for lang, spec in table["languages"].items():
        allowed = set(spec["allowed"])
        for sid, tag in spec.get("pins", {}).items():
            if tag not in allowed:
                raise ValueError(f"{lang} pin {sid}: {tag} is not an allowed tag")
        rows = rows_by_lang.get(lang, {})
        exclude = spec.get("exclude", {})
        out, bad = {}, []
        for n in sorted(names, key=lambda k: k.id):
            if n.id not in rows:
                continue
            if n.id in exclude:
                bad.append(Undecided(n.id, n.category, rows[n.id], f"excluded: {exclude[n.id]}"))
                continue
            tag, reason = decide(lang, rows[n.id], n.english, spec, spec.get("pins", {}).get(n.id), n.category,
                                 n.attr if n.category == "settlement" else None)
            if tag:
                out[n.id] = tag
            else:
                bad.append(Undecided(n.id, n.category, rows[n.id], reason))
        tags[lang], undecided[lang] = out, bad
    return tags, undecided


def stale_ids(spec: dict, computable, covers_settlements: bool):
    """Ids in a language's tags and pins that the inventory no longer lists. Settlement ids are judged only with the install."""
    def judged(sid):
        return covers_settlements or not sid.startswith(inv.SETTLEMENT_NAME_PREFIX)
    held = set(spec["tags"]) | set(spec.get("pins", {}))
    return sorted(i for i in held if i not in computable and judged(i))


def merge_tags(spec: dict, computed: dict, computable, covers_settlements: bool) -> dict:
    """The table's `tags` after a --write: the computed ones, plus the ids this machine cannot judge. Stale ids are dropped."""
    stale = set(stale_ids(spec, computable, covers_settlements))
    kept = {i: t for i, t in spec["tags"].items() if i not in computable and i not in stale}
    return dict(sorted({**kept, **computed}.items()))


def pinned_article_warnings(lang: str, table: dict, rows: dict):
    """Pinned ids whose row still starts with an article: the apply would print the article twice."""
    return sorted(i for i in table["languages"][lang].get("pins", {})
                  if rows.get(i) and nt.leads_with_article(lang, rows[i], table))


def inventory_rows(modules: Path | None):
    """(names, {lang: {id: text}}). Settlement names are included only when the live TAOM_Map is readable.

    The cache alone has no English defaults, and without them a multi-word name kept as written cannot be told
    from one that needs a decision, so a machine with no install would disagree with the table about settlements.
    A settlement row is the live text (what the apply tags), else the override, the cache or the English default.
    """
    names, cache, overrides, settlements_xml = inv.build(modules)
    if settlements_xml is None:
        names = [n for n in names if n.category != "settlement"]
    settle = [n for n in names if n.category == "settlement"]
    settle_ids = {n.id for n in settle}
    rows = {}
    for lang in nt.TAGGED_LANGUAGES:
        r = dict(inv.language_rows(lang))
        r.update(inv.resolved_rows(settle, overrides[lang], cache[lang]))
        if settlements_xml is not None:
            r.update({i: t for i, t in inv.live_settlement_rows(modules, lang).items() if i in settle_ids})
        rows[lang] = r
    return names, rows


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--write", action="store_true", help="rewrite the generated `tags` of the table")
    p.add_argument("--check", action="store_true", help="exit 1 when the table's tags differ from the rules")
    p.add_argument("--list-undecided", action="store_true")
    p.add_argument("--no-install", action="store_true")
    args = p.parse_args(argv)

    modules = None if args.no_install else game_modules(DEFAULT_GAME_ROOT)
    if modules is not None and not modules.exists():
        modules = None
    table = nt.load_table()
    names, rows = inventory_rows(modules)
    tags, undecided = build(table, names, rows)
    computable = {n.id for n in names}
    covers_settlements = any(n.category == "settlement" for n in names)

    for lang in nt.TAGGED_LANGUAGES:
        counts = Counter(tags[lang].values())
        print(f"{lang}: {len(tags[lang])} tagged {dict(sorted(counts.items()))}, {len(undecided[lang])} undecided")
        if args.list_undecided:
            for u in undecided[lang]:
                print(f"  [{u.category}] {u.id}: {u.text!r} ({u.reason})")
        for sid in pinned_article_warnings(lang, table, rows[lang]):
            print(f"  WARNING {lang}: the pin on {sid} sits on a row that still starts with an article: {rows[lang][sid]!r}")
    if not covers_settlements:
        print("Settlements: not checked (no TAOM_Map install)")

    if args.check:
        problems = []
        for lang in nt.TAGGED_LANGUAGES:
            spec = table["languages"][lang]
            have = {i: t for i, t in spec["tags"].items() if i in computable}
            if have != tags[lang]:
                problems.append(f"{lang}: tags differ from the rules")
            stale = stale_ids(spec, computable, covers_settlements)
            if stale:
                problems.append(f"{lang}: {len(stale)} id(s) the inventory no longer lists: {', '.join(stale[:10])}")
        if problems:
            print("FAIL:", file=sys.stderr)
            print("\n".join(f"  {m}" for m in problems), file=sys.stderr)
            print("Run python tools/generate_name_tags.py --write", file=sys.stderr)
            return 1
        print("PASS: the table's tags match the rules.")
        return 0

    if args.write:
        for lang in nt.TAGGED_LANGUAGES:
            spec = table["languages"][lang]
            spec["tags"] = merge_tags(spec, tags[lang], computable, covers_settlements)
            stale_pins = stale_ids({"tags": {}, "pins": spec.get("pins", {})}, computable, covers_settlements)
            for sid in stale_pins:
                del spec["pins"][sid]
        nt.TABLE_PATH.write_text(nt.dump_table(table), encoding="utf-8")
        print(f"Wrote {nt.TABLE_PATH}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
