#!/usr/bin/env python3
"""Reset the language rows of renamed map settlements that still hold their pre-rename name.

tools/Apply-MapVillageNames.py renamed 438 settlements (2026-05-26 and 2026-08-29) and never removed
them from tools/translation_cache, which keys on the string id alone. A later translator run then
wrote the old wording back: on 2026-10-27, 421 of the 438 ids showed it in 11 languages, either
the placeholder ("Dorf E2_1" for Linenhold) or the old vanilla name ("Usanc" for Mearhtun, and
"Усанк" in Russian; #1042). This one-off sets each such row to the current English name, with its BR or IT
grammar tag, and removes it from the cache, so the next translator run (#763) translates the real
name. Proper nouns staying English until then is the house rule (Apply-MapVillageNames.py).

Which rows are stale: the German file is the witness. An id whose German row already equals the
English was translated after its rename (the 17 ids tools/add_map_villages.py and the 2026-09-11
batch wrote), so every language's row for it stands, transliterations included. For any other id,
a row that differs from the English holds pre-rename text, except the pairs in KEEP. Measured with
the witness on 2026-10-27: 4,633 rows (421 in each of BR, CNs, CNt, DE, FR, IT, JP, KO, RU, SP and
TR, 2 in PL). EXTRA_IDS adds four Dunland fortifications outside that table with the same fault
(town_EN1 Galtrev showed "Epicrotea"; review of #1042).

RETIRED: it refuses to run without --rerun-retired. Once the translation backlog (#763) translates
these names, a German row that differs from the English is a translation, and a re-run would turn
every language's row back into English. Run it again only to repair a reinstall of an older
TAOM_Map package (tools/oneoff/write_village_descriptions.py --check detects one).

Write order: the cache rows go first and the German witness file last, so a run that fails part-way
leaves the witness stale and a re-run finishes the other languages. Byte discipline: binary read and
write, each file's BOM and CR CR LF kept, every output parsed before any file is written, a non-.xml
backup of each file it changes.

Usage:
  python tools/oneoff/reset_stale_village_loc_rows.py --rerun-retired            # dry run: counts per language
  python tools/oneoff/reset_stale_village_loc_rows.py --rerun-retired --apply    # writes the 12 loc files and the cache
"""
import argparse
import datetime
import os
import re
import runpy
import sys
import xml.etree.ElementTree as ET

TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, TOOLS)
import add_map_villages as amv  # noqa: E402
import name_tags  # noqa: E402

WITNESS = "DE"
# Rows that differ from the English and are still right: translations of the CURRENT name.
KEEP = {
    ("PL", "castle_village_E1_1"),  # Młotowisko, "hammer place": Hammerstand
    ("PL", "castle_village_E1_2"),  # Gliniana Warownia, "clay stronghold": Cleyhold
    ("PL", "castle_village_E1_3"),  # Solne Sioło, "salt hamlet": Saltstead
}
# Renamed by an older tool, outside Apply-MapVillageNames.py's NAMES, with the same stale rows.
EXTRA_IDS = ["town_EN1", "town_EN2", "town_EN3", "castle_EN5"]
ROW = re.compile(r'id="Settlements\.Settlement\.name\.([^"]+)"\s+text="([^"]*)"')


def target_ids(names):
    """The ids the repair looks at: the renamer's table, then EXTRA_IDS."""
    return list(names) + [sid for sid in EXTRA_IDS if sid not in names]


def bare(text):
    return name_tags.LEADING_TAGS.sub("", text)


def stale_rows(ids, master, rows_by_lang, witness=WITNESS, keep=KEEP):
    """{lang: [id]} of the rows to reset: ids whose witness row is not the English, and whose own row is not either."""
    witness_rows = rows_by_lang[witness]
    pending = [sid for sid in ids if sid in witness_rows and bare(witness_rows[sid]) != master[sid]]
    return {lang: [sid for sid in pending
                   if sid in rows and bare(rows[sid]) != master[sid] and (lang, sid) not in keep]
            for lang, rows in rows_by_lang.items()}


def reset_rows(loc_text, lang, names, table):
    """`loc_text` with each id in `names` set to its English name, keeping the row's grammar tag; (text, count)."""
    count = 0
    for sid, name in names.items():
        key = "Settlements.Settlement.name." + sid
        pat = re.compile(r'(id="' + re.escape(key) + r'"\s+text=")([^"]*)(")')
        loc_text, n = pat.subn(lambda m, k=key, v=name: m.group(1) + name_tags.carry_tag(lang, k, m.group(2), v, table) + m.group(3), loc_text)
        count += n
    return loc_text, count


def main(argv=None, cache_dir=amv.CACHE_DIR):
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--apply", action="store_true", help="write the 12 loc files and the cache (default: dry run)")
    ap.add_argument("--rerun-retired", action="store_true", help="run this finished one-off anyway (see RETIRED)")
    args = ap.parse_args(argv)
    if not args.rerun_retired:
        raise SystemExit("reset_stale_village_loc_rows.py is a finished one-off: a re-run after #763 would turn "
                         "translated names back into English. Pass --rerun-retired to run it anyway.")

    ids = target_ids(runpy.run_path(os.path.join(TOOLS, "Apply-MapVillageNames.py"), run_name="names_only")["NAMES"])
    _, master_text = amv._read(amv.LIVE)
    master = amv.master_names(master_text)
    ids = [sid for sid in ids if sid in master]
    locs = {lang: amv._read(amv._loc_path(lang)) for lang in amv.LANGS}
    rows = {lang: dict(ROW.findall(text)) for lang, (_, text) in locs.items()}
    plan = stale_rows(ids, master, rows)

    table = name_tags.load_table()
    out = {}
    for lang, (bom, text) in locs.items():
        new, n = reset_rows(text, lang, {sid: master[sid] for sid in plan[lang]}, table)
        if n != len(plan[lang]):
            print(f"REFUSING: {lang} planned {len(plan[lang])} row(s), matched {n}", file=sys.stderr)
            return 1
        ET.fromstring(new.encode("utf-8"))  # every output parses before any file is written
        out[lang] = new
        print(f"  {lang:3}: {n} row(s) to reset" + (f", e.g. {plan[lang][0]} -> {master[plan[lang][0]]}" if n else ""))
    print(f"Total: {sum(len(v) for v in plan.values())} row(s) across {sum(1 for v in plan.values() if v)} language(s).")
    if not args.apply:
        print("\nDRY RUN: re-run with --rerun-retired --apply to write the live loc files and remove those rows from the cache.")
        return 0

    removed = amv.evict_cached_names(plan, cache_dir)
    tag = "reset_stale_" + datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
    for lang in [lang for lang in locs if lang != WITNESS] + [WITNESS]:
        if plan[lang]:
            amv._write(amv._loc_path(lang), locs[lang][0], out[lang], tag)
    print(f"\nApplied; backups *.bak_{tag}; {sum(removed.values())} cache row(s) removed. "
          "Then run python tools/generate_name_tags.py --check and python tools/check_external_loc_coverage.py.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
