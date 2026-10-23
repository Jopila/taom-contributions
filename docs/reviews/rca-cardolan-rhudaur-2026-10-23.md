# RCA: Cardolan and Rhudaur (#978) deep review, 2026-10-23

## Top line

`/deep-review` at tier 1 (live `TAOM_Map` and Armory data) ran four lenses in one wave: 2 Engine
compatibility, 5 Data flow, 7 XML and ModuleData, and Tooling. Codex did not run: no paid dispatch
was authorised for #978. No lens found a CRITICAL or HIGH, so no second wave ran. They found 5
MEDIUM and 13 LOW or nit findings; 16 are fixed in this change, and 2 wait on Mike.

The work under review was committed and pushed by another session as `e339df075` at 12:49, while
the lenses were running and before any finding was fixed. That commit's rebase also left the tool
suite red (3 errors), which `0fb0e6c13` fixed. The fixes below are a follow-up to `e339df075`.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The 14 ladies (`lord_CA*_3/_4`, `lord_RH*_3/_4`) had a bare `fighter_gondor` face template. Its hair tags are men's styles that match no adult female hair, and the native roll then falls back to the template keys' hair indices, where female index 0 is Bald: about half would start bald, the rest share one style, for the whole campaign | Replaced mechanism dropped an implicit property | The face template replaced Arthedain's copied face keys to pass the face-variety ceiling. A copied key carries its hair; nobody listed what the old keys supplied. `HeroFaceVarietyDataTests` models faces, not hair | Generator option `female_hair_tag`; gate `FemaleLordHairDataTests`; lesson in `data-content-cultures.md` |
| 2 | MED | `armour_classes.xml` went stale: Cardolan's level 26 troops wear `sk_gd_ano_bracer_noble_heavy_a`, which moves it from elite to heavy | Warning read by name filter | The validator run was read for warnings that name Cardolan or Rhudaur; `ARMOUR_CLASS_TABLE_DRIFT` names item ids, so the filter hid it | Regenerated; lesson: diff the warning set against the base commit |
| 3 | MED | `promote_borrowed_cultures.py --only cardolan/rhudaur --check` exited 1 after an editor save rewrote the live positions with 4 decimals; no gate ran the promoter | Live value copied verbatim; generator outside the sync gate | The "every `--check` exits 0" claim was true when made; the next editor save broke it, and `test_generator_regions_in_sync.py` still excluded the promoter "until #979", a blocker #978 itself cleared | Round to the shipped 3 decimals; the promoter joins the sync gate; lesson in `build-tooling-workflow.md` |
| 4 | MED | `generate_new_faction_kingdoms.py` parsed and wrote one file at a time, so a later parse failure left earlier files written | Writer rule not audited when extending a tool | #978 fixed the tool's region defect but did not check it against the whole XML I/O checklist (`moduledata-validation.md`: parse before write, dry-run default) | Parse every output, then write; test `test_generate_new_faction_kingdoms_writes.py` |
| 5 | MED | The same generator wrote on a plain run; one run in this session wrote at once | Same as 4 | Documented in the README instead of fixed | `--apply` added; docs updated |
| 6 | LOW | The rebase merge left #978's parse loop beside upstream's `unparsable()`, with a raw U+FEFF literal | Merge residue | Auto-merge, not reviewed before push | Deleted |
| 7 | LOW | The `SubModule.xml` registration region replace matched only the first region, so a doubled region passed `--check` | **Repeat** of the #947 lesson the same day ("A fix that removes a false positive must keep what the old code caught by accident") | The in-place rewrite was written before that lesson, and not re-read against it | Uses `_marker_region.replace_region`; `test_promote_register_region.py` |
| 8 | LOW | `realm_troops.py` self-check missed a two-handed axe carried with a shield (`_2h_` ids) | Rule keyed on two id words | Written for Arthedain's weapons, then reused for Dunland's axes | Pattern widened; negative tests |
| 9 | LOW | Rhudaur's kingdom banner was `clan_cardolan_2`'s icon (11006), not its royal house's (11002) | Copy slip | No check compares a kingdom icon to its ruling clan's | Fixed (cosmetic, no gate) |
| 10 | LOW | Cardolan's career group "Sword of the Last Bridge" kept Gondor's name; the Last Bridge is in Rhudaur | Text remap table missing a row | Gondor's own text already said "Last Bridge", so no Gondor word triggered the remap or the flavor gate | "Sword of Sarn Ford", remap row, 12 rows reset |
| 11 | LOW | `ClonedCultureFlavorTests` had no Cardolan or Rhudaur entry, though its docstring requires one per promoted culture | Gate list not extended | The test passes silently for an unlisted culture | Both entries added |
| 12 | LOW | `external-loc-coverage-baseline.json` not re-taken for 47 new map names and 9 bows | Step skipped | `tools/README.md` names the step; it was not in the build checklist | Baseline re-taken |
| 13 | LOW | Feature doc: no Save Compatibility section, a stale settings count (425 against 423), tables it called decided but did not list, fix credits that the rebase moved upstream | Doc drift | Written before the rebase | Fixed |
| 14 | LOW | Armory snapshot `weapon_descriptions.xslt` one line behind the live file; a v1.5.4 line citation in `settlements.md` | Doc drift | Not part of the build checklist | Fixed |
| 15 | NIT | A generator comment called the realms "Natural"; two test comments and one hard-coded list lagged the data | Comment drift | Not data | Fixed |
| 16 | LOW, open | The 12 translated `loc_arnor.xml` files have no row for the 9 new ladder bows, so `sync_ranged_ladder_translations.py` now stops | Owed translation | The free seed tool would also write 87 rows other work owns | Owed in #763 (paid, Mike's word); documented |
| 17 | LOW, open | No `player_career_<culture>_*` kits for any Arnor realm (inherited from Arthedain) | Decision owed | Arthedain shipped without them | Documented; Mike decides |
| 18 | LOW, open | Arthedain's kingdom text, its king's entry and the Remnants of Angmar card still describe Angmar and a lone Arnor realm | Decision owed | Already pending with Mike | Documented in Known gaps |

## Root-cause patterns

**Replacing a mechanism drops what the old one supplied implicitly (1, 7).** Copied face keys carried
hair; the face template did not. A strip-and-append writer reported a doubled region; the first
in-place rewrite did not. Both fixes were tested against the problem they solved, not against the
list of things the old code did. The #947 lesson states this for writers; finding 1 shows it holds
for data too.

**A gate that is green once is not a gate (2, 3, 12).** Each was a check that passed or was skipped at
one moment: a warning filtered by name, a `--check` run by hand before an editor save, a baseline step
outside the checklist. The fix in each case moves the check where it runs every time:
`test_generator_regions_in_sync.py`, a diff of warning sets, the baseline in the same commit.

## Why each lens found what it found

- **Engine compatibility (2)** found finding 1 by following the face template into the native hair
  roll; no data gate models hair.
- **Data flow (5)** found 11, 12, 9, 17 and 15 by comparing every `arthedain` consumer with its new
  siblings.
- **XML (7)** found 2, 12, 16, 13 and 14 by running every gate in `moduledata-validation.md` "Gate per
  file kind", including the ones the build did not.
- **Tooling** found 3 to 8 and 10 by running each writer against broken inputs in memory.
- **Codex** did not run. **Standards, Efficiency, Completeness and Design** were skipped by tier.

## Process note

`e339df075` committed this session's uncommitted files from another session while the review ran.
AGENTS.md "Preserve others' work" forbids committing another session's changes; this session cannot
tell whether Mike asked for that commit. The review followed the code to `e339df075` and the fixes
land on top of it.

## Follow-up

- **Unverified:** Gondor's female notable templates (and the cloned ones, `village_woman_*`,
  `townswoman_*`) also use a bare `fighter_gondor`. Whether notable creation takes the same native
  hair fallback is not traced; an in-game look at Gondor's women decides it.

## Follow-up batch (Mike, 2026-10-23: kits, Arvegil, the Arthedain career group)

Lens 5 and lens 7 reviewed the batch at tier 2 and found no defect in the data: every Arnor career
start reaches its roster, every item resolves, and both translation resets are complete. Their LOW
findings, all fixed in the same commit:

| # | Sev | Finding | Why missed | Preventive action |
|---|---|---|---|---|
| F1 | LOW | Three more "78 kits" counts (a doc line, the feature map, a tool docstring) and two per-culture claims (the axe sidearm list, the saddle-horse count) went stale | The counts were searched by the words "career kits", not by the number | A count nothing computes is removed where it can be (the README now names no number); the rest moved to 96 |
| F2 | LOW | The kit file's header named three cultures without career rosters; nine have none | The comment was written for #629 and no test pinned it | `CareerKitCoverageTests`: a character-creation culture with no kits fails unless listed, so the next new culture fails until it is decided (proved against the HEAD file: it names exactly the three Arnor realms) |
| F3 | LOW | Arvegil's entry is a saved hero property, so an English campaign started before the change keeps the Angmar line | Not a defect; the change did not say so | Stated in both feature changelogs |
| F4 | LOW, open | Arthedain still has Angmar in two career descriptions and five wanderer backstories (Mike: leave for now); its ranger group "Bowman of the Angle" is now "Dúnedain Rangers" | Outside the request | Known gaps; the rename landed with the placeholder kits |

The Arnor kits shipped late for the same reason as F2: a culture can reach a career that has no kit
and nothing fails. The test now closes that.

## Placeholder kits batch (Mike, 2026-10-23: kits for the nine other cultures, "Dúnedain Rangers")

Lens 5 and lens 7 at tier 2. Fixed in the same commit unless marked open:

| # | Sev | Finding | Why missed | Preventive action |
|---|---|---|---|---|
| P1 | MED | Shaghana's Chatak Javelineer started with a bow that needs Bow 100, so a new character could not re-equip it; its Abanissa sibling got javelins | The generator's default ranged rule (bow and arrows) was taken for every new culture; `career-system.md` says a ranged career that is not a bow career needs a `RANGED` row, and the career names were not read | `RANGED["shaghana"]`; the test asserts the javelin rule for Shaghana and Abanissa |
| P2 | MED | The goblin, Misty Mountain and Blue Craig markets never stock the warg and saddle their cavalry kits start on, while Erebor's ram and Mirkwood's elk have guaranteed stock | A starting mount was treated as gear only; its market routing lives in another file | Routed to those three cultures; the rule "a career mount is rebuyable in its culture's market" now stands in the warg comment |
| P3 | LOW | The ranged and infantry section comments said "placeholder mount" on rosters with no mount | Template comment written once for all three archetypes | Fixed |
| P4 | LOW, open | `wire_starter_kit_rosters.py` `BORROW` still builds Khand's no-career start from Rhûn's kit, now duplicating `KIT_SOURCE` | Two tables encode one fact | Output is identical today; removing `BORROW` touches the tool, its tests and the override header, so it waits until Khand has gear of its own (#571) |
| P5 | LOW, open | Lothlorien and Lindon archers start with a Rivendell ladder bow that needs Bow 100, as Rivendell's own kit already does | Elf troops carry no javelin, so the rule cannot pick another weapon | A decision for Mike: lower the elven ladder bows' difficulty, or accept it |

## Career mounts (Mike, 2026-10-23: compare the horses, then the harnesses)

| # | Sev | Finding | Why missed | Preventive action |
|---|---|---|---|---|
| M1 | MED | Dunland's cavalry kit took `bandit_saddle_highland`, vanilla `battania`'s harness, under a rule that said "the harness vanilla gives the culture"; Dunland is vanilla `empire`, whose retainer rides the light harness | **Repeat** of the trap index row "Culture ids": the lore name (Dunland, hill folk) was matched to a vanilla culture by flavour, not by the id the data uses | Caught by lens 7 before commit; Dunland keeps the light harness. When a rule says "vanilla's X for this culture", read the culture's id from `cultures.json` first, then look X up under that id |
| M2 | LOW | The doc said a culture horse is "still weaker than each culture's lowest rider"; Rohan's level 6 steppe war horse has less maneuver, and Harad's riders are slower than `aserai_horse` | A summary written from tiers, not from the stats it quoted | Reworded to the fact checked: every lowest rider sits on a tier-2 horse or a war horse |
