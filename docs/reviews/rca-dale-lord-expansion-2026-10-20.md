# RCA: Dale lord expansion (#943; deep review 2026-10-20)

## Top-line

The Dale lord expansion added five noble clans and 27 lords and raised two clans to tier 3, so Dale
fields 30 lord parties at campaign start instead of 18. Mike committed and pushed it as `64345bf3a`
while the review ran, so every fix below lands in a follow-up commit. Seven lenses ran (Standards was
not in scope: no C# or harness file changed). The engine lens confirmed every engine claim against
the v1.5.5 DLLs, and nothing in the shipped data can crash a new campaign once the build is deployed.
The review found 2 HIGH, 9 MEDIUM and 5 LOW findings. All were fixed in the same session except the
owner-reference gate (a follow-up) and the in-game check that remains owed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH | `Assign-SettlementOwners.py`'s Dale SPEC got the new clans and tiers but was never run, while the live castle seating was edited by hand. Any later `--apply`, for any region, would have re-seated 7 Dale castles | Generator bypassed | The brief told the author to "update the spec to match" the hand edit; nobody ran the dry run, which reported "7 differ" at once | Mike chose the tool's own distribution: the dry run is the record again and `--apply` runs after the deploy. Lesson: change generator-owned data through the generator |
| F2 | HIGH | The live `TAOM_Map` named `clan_sturgia_10` and `_11` as castle owners while the deployed TAOM build (2026-10-18) did not define them; a new campaign would have pointed two castles at placeholder clans | Ship order | The live edit was made before any deploy, and no rule says which module goes first | Deploy before the live `--apply`; a Dale ship-together check in `release-process.md`. Lesson |
| F3 | MEDIUM | All 27 faces were exact copies of living Dale lords (Torgrim had King Bard II's face) | Donor rule | The brief asked for donor faces from existing Dale lords; nearest age made the twins as close as possible | Donors are now vanilla Vlandian and Imperial faces that `lords.xslt` replaces, so no campaign character wears them; `test_new_lord_faces_exist_nowhere_else`. Lesson |
| F4 | MEDIUM | 5 of 7 bowman lords had no bow: rosters were rotated a to h, and only `a` and `b` carry one | Kit ignores role | The brief said "rotate rosters"; nobody read what each roster holds | Bowmen get `a` or `b`; `test_bowmen_carry_a_bow_roster`. Lesson |
| F5 | MEDIUM | `clan_sturgia_14`'s donated banner (from `clan_mirkwood_5`) used icon ids no `banner_icons` file defines: a blank emblem | Unchecked donor | `ShippedHeraldryBannerKeyTests` checks only the key's shape | All five fly the Dale banner clans 2 to 9 use. Lesson; the Mirkwood keys are a follow-up |
| F6 | MEDIUM | No gate resolves `owner="Faction.X"` in any of the three modules, which is why F2 passed every check | Gate gap | `taom_schema.py` has no faction ref kind | Follow-up: a `faction` ref kind in `validate_moduledata.py` (needs Mike's word for the issue) |
| F7 | MEDIUM | Merge `70c164bb3` kept the other side of `data-content-cultures.md`, dropping the Dale lesson from the pushed branch | Merge | Two appends at the end of one file conflict, and the resolution took one side | Lesson restored. Optional prevention for Mike: `docs/reviews/lessons/*.md merge=union` in `.gitattributes` |
| F8 | MEDIUM | The Dale block came after the Arthedain generator's block; `generate_new_faction_kingdoms.py` re-inserts its block at the closing tag, so `test_generator_regions_in_sync` failed from `64345bf3a` on | Generator regions | Only the C# suite ran before the commit; the pytest folder did not | Dale block moved ahead of Arthedain in the three files; both generators report no change. Lesson |
| F9 | MEDIUM | No #763 record for the 32 new name keys; closing #943 would have left them untracked | Process | The doc named #763 but nobody posted there | #763 comment posted |
| F10 | MEDIUM | `release-process.md` had no Dale ship-together check | Docs | The Arthedain precedent was not extended | Paragraph added |
| F11 | MEDIUM | `author_dale_lords.py --apply` replaced its blocks wholesale, silently reverting any hand edit made since; the doc told the next author to re-run it | Tool hazard | Written as a reusable tool, used as a one-off | Retired to `tools/oneoff/`; it refuses to run without `--rerun-retired` |
| F12 | LOW | Custom Battle's default commander pick sorts by id, so `lord_S10_*` replaced Dale's three commanders, including a steward lady | Side effect | No curated `sturgia` row | Curated row (Halvard, Ketil, Starkad) plus the test's expected factions |
| F13 | LOW | Three new house names repeated old ones (the Celduin is the Running River; Lake-town is Esgaroth) | Naming | The name grep checked clashes, not meanings | Renamed: House of the Carnen, House of Ravenhill, House of the Forest River |
| F14 | LOW | Doc wording: "lords only add names" (spare fighting lords refill slots), no Talent Magnet, "Home" for a value the engine re-picks weekly, no ship-together note, "the validator checks the new references" | Docs | Written from the plan | Corrected |
| F15 | LOW | 16 of 27 skill pins repeated what the generator derives anyway | Redundancy | Every new lord was pinned by habit | Deleted; parity checked on all 27 |
| F16 | LOW | `author_dale_lords.py` had no README row; sibling docs still said Dale keeps vanilla's clans | Completeness | Normal review yield | Rows and sibling docs updated |

## Patterns

**Data that a tool owns must change through the tool.** F1 and F11 are one habit seen from two
sides: a hand edit beside a generator that does not know about it. The settlement tool would have
undone the hand seating; the author tool would have undone hand fixes. In both cases the tool's own
dry run showed the problem in under a second.

**Copying from a neighbour copies what makes it the neighbour.** F3, F4 and F5: faces from the same
court, kits by rotation, banners from another realm. Each donor passed a shape check (a valid key, a
valid roster id, ten numbers per layer) and failed on meaning. A donor must be checked for what the
player will see, not only for whether the reference resolves.

## Why each agent missed these

The authoring agents built from a brief that specified donor faces, roster rotation and a hand-seated
castle edit, so F1, F3 and F4 came from the brief (the orchestrator's), not from the builder. The
authoring agent ran the C# suite and the validator but not the pytest folder, which is how F8
shipped. No review lens ran before the commit, because the commit came before the review gate.

## Feedback memories to codify

None. Each pattern is recorded as a lesson in `docs/reviews/lessons/`, which is the repo's durable
record.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/dale-lord-expansion.md](../features/dale-lord-expansion.md)

<!-- backlinks-end -->
