# RCA: Tharbad and Minas Eithel name swap, deep review 2026-10-27

## Top line

Mike reported that `town_CA1` ("Minas Eithel") stands where Tharbad is on Tolkien's map, so the two
Cardolan town names were swapped, with the names of the two clans that hold them. `/deep-review` ran
at tier 1 (live `TAOM_Map` data) with four lenses in one wave: 2 Engine compatibility, 5 Data flow,
7 XML and ModuleData, and Tooling. Codex did not run: no paid dispatch was authorised for this change.
No lens found a CRITICAL or HIGH, so no second wave ran. The review confirmed 3 MEDIUM and 4 LOW
findings; 5 are fixed in this change and 2 are owed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `generate_new_faction_kingdoms.py:127-128` still wrote the old clan names, so `test_generator_regions_in_sync.py` failed and an `--apply` would have reverted `clans.xml` alone | Generated data hand-edited (**repeat**) | The plan checked the one generator it knew (`add_map_fortifications.py`) and did not grep `tools/` for every writer of the clan names. The gate table row for clans listed only `validate_moduledata.py`, and the regions test lives in the pytest folder the gates did not run | Generator rows swapped; `--check` exits 0 and the regions test passes. The clan row of the gate table in `moduledata-validation.md` now names `test_generator_regions_in_sync.py` |
| 2 | MED | `add_map_fortifications.py --check` compared rows, loc rows, positions and gates but never names: it returned no finding on the pre-swap backups, so a reinstall that restored the old town names would pass every gate | Unversioned data without an in-repo gate | Pre-existing. The trap index row "Unversioned modules" asks for a gate, but the check was written for new rows, where the name cannot drift | `check()` compares each master name with its `FORTS` row; test `test_a_renamed_row_is_named` (RED, then GREEN); the probe on the backups names both towns. The TAOM_Map row of the gate table now lists this `--check` |
| 3 | MED | The note to Mike said an old save "may" keep the old clan names. In English it always does: `Clan.Name` is saved, `clans.xml` loads only for a new campaign, and English never looks a `{=key}` up | Claim made before the evidence | The statement was inferred from the town-name behaviour and not checked against `SandBoxManager.InitializeSandboxXMLs` and `MBTextManager.GetLocalizedText` | Corrected in the report and in `cardolan-rhudaur.md` "Save Compatibility"; lesson in `localization-ui.md` |
| 4 | LOW | The feature doc table, the #978 body and the harness memory card still stated the old mapping | Records of a name not swept | The swap was planned from data files; records in prose were left for the docs pass | All three updated |
| 5 | LOW | The two one-off scripts parsed and wrote file by file, where `tools/README.md` asks that every output parses before any is written | Writer rule not followed in a scratch script | The scripts were judged one-offs; every file was written and parses, so no data is affected | Recorded only; no issue: scratch scripts that do not ship, and the README rule already covers them |
| 6 | LOW, open | The dedicated server stage (`E:\repos\BannerlordCoop.DedicatedServer\.stage-win`) holds copies of TAOM_Map and TAOM with the old names | Copy outside the deploy path | Not part of `./build.ps1` | Owed: re-stage after the deploying build |
| 7 | LOW, open | The orchestrator said `Apply-MapVillageNames.py` would revert later renames; its 438 rows match the live names exactly, so it was safe | Claim made before the evidence | Reasoned from the script's shape, not from its data | Corrected in the report; no data affected. Later found incomplete: only the master was compared, and the 12 language files held pre-rename text (`lessons/localization-ui.md`, #1042) |

## Root-cause pattern

**A rename has more holders than the data files (1, 2, 4, 6).** The name lives in the shipped XML,
in each generator table that writes it, in the gate that should hold it, in prose records and in
staged copies. The plan listed only the XML and one generator. The fix for the class is the search,
not the edit: grep `tools/`, `docs/`, the memory card and the open issues for the old name before
the first edit, and run the gates of every file kind that holds it.

## Why each lens found what it found

- **Engine compatibility (2)** found finding 3 by tracing `Clan.Name` from the save through
  `SandBoxManager` and `MBTextManager`, and confirmed that town names reload from XML on every load.
- **Data flow (5)** found 2, 4 and 6 by following each name to every consumer and copy, the
  dedicated server stage included.
- **XML (7)** found 2 by running `check()` on the pre-swap backups, and proved every file
  byte-faithful.
- **Tooling** found 2 and 5, and proved that the generator fix alone clears the regions test.
- Finding 1 was found by the orchestrator while mapping the blast radius, before the lenses ran.
- **Codex** did not run. **Standards, Efficiency, Completeness and Design** were skipped by tier.

## Environment (reported, not fixed)

Steam updated the game to v1.5.6 at 11:11 on 2026-10-27, during this work; the pin is v1.5.5.
`/engine-bump` runs before the next build. The lenses found no difference between v1.5.5 and v1.5.6
in the engine paths they read.

## Pre-existing, outside this change

- The FR, JP, KO and TR `loc_settlements.xml` files show id-shaped placeholders ("Village EN2_3")
  where the master has real names (129, 27, 24 and 8 rows). No issue: reported to Mike, who decides
  whether to file one.
- Mike chose to keep the village names. Eithelford and Talath Eithel now belong to Tharbad, and
  Swanfleet and Gwathló Ferry to Minas Eithel. The Tooling lens notes that the new Tharbad lies
  east-north-east of Sarn Ford on the live map, where Tolkien puts Tharbad south-east of it
  (UNVERIFIED: the lens cannot see the rivers). Mike's in-game check decides both.
