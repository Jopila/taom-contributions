# RCA: Lindon banners, 2026-10-28

## Top line

Mike saw the dark Gundabad orc crest over Harlond and Forlond on the campaign map. All six Lindon clans flew it
(banner icons 22000 and 22001): `generate_new_faction_kingdoms.py` clones every new-faction clan from
`clan_gundabad_1`, banner key included, unless the realm names a `banner`, and Lindon's entry named none; the #1039
one-off `author_lindon_clans.py` then copied `clan_lindon_1`'s key into the four new clans. The fix gives each house
its own Noldor or Sindar sigil in gold (171) on light blue (286), Mike's recommended codes, and records the keys in
the one-off. `/deep-review` ran at tier 2 (repo clan data, a one-off, tests) with lenses 5 Data flow, 7 XML and
Tooling; Codex did not run (no paid dispatch was authorised). No lens found a CRITICAL or HIGH. It confirmed 3
MEDIUM and 6 LOW findings, all fixed here except two older Banner Injection limits and one gate, which are
follow-ups.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 0 | (report) | All six Lindon clans flew the Gundabad orc crest | Cloned data kept its template's heraldry | Every gate checks that a key parses and its icons resolve, not whose emblem it is; #1039 copied the key without looking at it | Per-house keys; `test_every_lindon_clan_flies_its_own_elven_banner`; lesson in `data-content-cultures.md` |
| 1 | MED | `Kingdom.lindon` flew `Kingdom.rivendell`'s banner word for word, and a ruling clan shows its kingdom's banner, so Mithlond and the Falathrim would have shown Rivendell's banner after the fix | Same as 0, one level up | The fix looked at clans only | The kingdom flies the Falathrim key (15000); `test_the_lindon_kingdom_flies_an_elven_banner_of_its_own` |
| 2 | MED | The new test read six hard-coded ids, so a seventh Lindon clan with a copied or orc key would pass | Gate scoped narrower than its name | Written from the six ids of this fix | The test selects every `super_faction="Kingdom.lindon"` clan |
| 3 | MED | The new test passed a key with no sigil layer | Loop with no assertion on an empty input | Same as 2 | It asserts at least one sigil layer |
| 4 | LOW | The test parsed XML by regex: an equivalent `<Faction></Faction>` row or reordered `<Icon>` attributes failed it, a commented-out row was read | Regex over XML | Copied from the file's other tests | ElementTree |
| 5 | LOW | The comments and the doc said the kingdom was cloned from Gundabad; the clans were (from `clan_gundabad_1`), the kingdom from Rivendell | Claim not traced to the generator | Written from the symptom | Reworded in the one-off, the test, the feature doc and the lesson |
| 6 | LOW | The lesson pointed at emblem PNGs that look blank (white on transparent) | Instruction not tried | Written without opening one | "View them on a dark background" |
| 7 | LOW | `recipe-add-a-kingdom.md` said 38 clans share `clan_gundabad_1`'s banner, "all six Lindon elf clans included" | Measured count went stale | The doc's count is not re-run by any gate | Re-measured with its own command: 32 |
| 8 | LOW | No test required a regeneratable realm to name a `banner` and one icon per clan, the gap that let Lindon's clans keep the crest | Generator default not pinned | The four regeneratable realms all set them by hand | `BannerTests` in `test_generate_new_faction_kingdoms.py` |
| 9 | LOW | No gate checks a banner key's background mesh id | Gate covers icon layers only | `test_banner_icon_ids.py` starts at the first sigil layer | `FOLLOW-UP` no issue: every shipped key uses an existing background, and the engine behaviour for an unknown one is unverified; file it if a key ever needs a new background |
| 10 | LOW | In a save made before this change, Banner Injection sets the new banner but not the clan's three saved banner colours, which the engine uses only when a Lindon clan leaves its kingdom; and it never updates a ruling clan's own banner | Older Banner Injection limits, all realms | Out of this change's scope | `FOLLOW-UP` no issue: affects every banner the feature changed, Mike decides whether to file it; written as a known limitation in the Lindon feature doc |

Owed: a deploying build (the installed `Modules/TAOM/ModuleData` still holds the Gundabad keys, and Banner Injection
reads the installed copy), the in-game check in the Lindon feature doc's step 7, and a dedicated-server re-stage.

## Root-cause pattern

**A gate that answers "does it render" stood in for "is it right" (0, 1).** The shape gate and the icon lookup both
passed an orc crest on elves and Rivendell's banner on Lindon. Looking at the sigil (the PNG or the sheet name)
catches both; the new tests pin the sheet and that the key is not another realm's.

## Why each lens found what it found

- **XML** found 1 by following the ruling clan's banner to the kingdom row and comparing it with every shipped key.
- **Tooling** found 2, 3, 4, 5 and 8 by running the new test against 20 edited copies of the data and reading the
  generator.
- **Data flow** found 7 and 10 by re-running the recipe's count and tracing Banner Injection into old saves.

## Feedback memories to codify

None beyond the lesson in `lessons/data-content-cultures.md`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)

<!-- backlinks-end -->
