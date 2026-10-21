# RCA: coronation title and Sauron's face set (#955, #956, deep review 2026-10-20)

## Top-line

Two defects found while testing Crown Guard (#825) with `taom.play_coronation`, both confirmed from the
installed v1.5.5 code and data:

- **#955:** the coronation title asks `GameTexts.FindText("str_liege_title[_female]", <kingdom culture>)`, and
  no module defined a variation for the 17 TAOM-only kingdom cultures, so the title read the engine's
  "Text with id ... doesn't exist" error. Dunland's fixed `empire` branch read vanilla's "Emperor of
  Calradia" line. The same text is read by dialogs through a tag-scored lookup, so each new row carries a
  tag of its own, defined by 17 `ConversationTag` classes.
- **#956:** the `sauron` race had no `as_sauron_facegen` pair, so every scene notification that posed
  Sauron logged a missing action set and fell back to set 0. The pair is now a copy of the elf pair, in the
  live Armory and the in-repo snapshot, with an in-repo gate.

The review ran six lenses; two (completeness and design) were stopped by the account's spend limit partway,
and the orchestrator did those checks itself. No lens found a runtime defect. It found two MEDIUM findings
and eleven LOW ones, all in tests and text. All were handled in the same session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | MEDIUM | The race-scan refactor dropped the two floors (a race from `lords.xml` and from `lords.xslt`), so both race gates could pass on a broken scan, and `sauron` (reachable only through `lords.xslt`) would drop out of the new gate silently | Lost guard | The brief said "behaviour unchanged"; the builder moved the scan into a helper and left the asserts behind | Floors moved into the shared helper, so both gates keep them. Lesson in `testing-qa.md` |
| C2 | MEDIUM | The docs said the Sauron pair makes him "stand in the proper pose"; his coronation pose comes from `as_human_warrior` either way (the engine's fallback set 0 and the new set's `base_set`), so the only effect is the missing-set log line | Engine claim from the plan | The plan inferred a visible pose change from the missing set without reading which set holds the scene's pose action | Docs corrected; the in-game check is the rgl log line. The #956 issue's encyclopedia check (which reads `_warrior`, not facegen) is dropped |
| C3 | LOW | The gate's comment and message said the engine does not follow `base_set` for facegen actions; the native parser copies the base's slots at parse time | Engine claim from a doc | Copied from the snapshot README's rule (README.md "declare everything"), which is itself imprecise | Comment and message corrected, and the README rule too (Follow-up below) |
| C4 | LOW | A test comment said a bad tag throws "at first conversation"; `InitializeTags` runs in the `ConversationManager` constructor, at every campaign start and load | Engine claim | Written from the method name | Corrected; a rule line in `.claude/rules/csharp-patterns.md` names the gate |
| C5 | LOW | The tag file said the classes must be "directly derived"; the engine requires no shared base because it instantiates every subclass, an abstract base included | Imprecise reason | Same | Reworded to the real reason |
| C6 | LOW | Three tag class names broke the house spelling (`Bluecraig`, `Dolguldur`, `Mistymountainorcs`) | Naming | Derived mechanically from the culture ids | Renamed (`BlueCraig`, `DolGuldur`, `MistyMountainOrcs`); the engine keys on `StringId`, unchanged |
| C7 | LOW | The gate read SandBoxCore's absent `monsters.xml` and not SandBox's or the Armory's `Monsters/LOTR/*.xml`, and followed `base_monster` one hop | Gate reads less than the engine | The file list was guessed | The engine's files; the chain is followed |
| C8 | LOW | The snapshot README called the engine's fallback "silent" (it logs the miss), its restore check named only the elf pair, and the rewritten line kept two dashes | Docs | Rewrite kept old wording | Corrected |
| C9 | LOW | An unused `using`, two fully qualified `HarmonyLib` calls, a hand copy of the six vanilla-id cultures, and a test name that does not say what is expected | Test hygiene | Builder habit | Cleaned up; the list derives from `ExpectedTitles` |
| C10 | LOW | The new `Main/Features/LiegeTitles` folder had no feature doc or feature-map row | Docs duty | The plan put the docs into Crown Guard's | `docs/features/liege-titles.md`, its row and index line |
| C11 | LOW | The coronation keys the title on the kingdom's culture, dialogs on the ruler's own; a ruler of another culture gets two titles | Undocumented engine split | Not traced | Documented; a kingdom-culture tag would tie with vanilla tags, so the vanilla behaviour stays |
| C12 | LOW | Both issue bodies predated the plan and the build | Docs drift | Filed before the plan | Rewritten from what ships, under the new commit-time step |
| C13 | LOW | The race test parsed the stylesheets twice | Test cost | Refactor | One parse per test, passed to the helper |

## Root-cause pattern

C2 to C5 and C8 are engine claims written from a plan, a method name or an older doc rather than from the
code that decides them: the same pattern as the Crown Guard and Hall of Fire reviews this day. The widened
`harmony-il.md` lesson (any doc that states how an engine hook behaves) covers them; C2 adds the case of a
fix whose visible effect was assumed.

## Why each agent missed these

- **The planner:** read the facegen naming and the missing set, and inferred a pose change without reading
  which set holds the scene's pose action (C2); took the README's base_set rule as fact (C3).
- **The builder:** moved the race scan into a helper and lost its floors (C1); named classes from ids (C6).
- **The lenses:** XML found C8 (restore check); Engine C2 to C5, C8; Data flow C1, C7, C11, C10; Standards
  C1, C6, C9; Efficiency C13. Completeness and design were stopped by the spend limit; the orchestrator
  checked the issue bodies (C12), the docs duty (C10) and the design questions below.

## Design decisions (settled by the orchestrator, the design lens being stopped)

- **Verbatim copy vs a two-line alias** (`base_set="as_elf_facegen"`): kept the copy. Every other race uses
  full facegen sets, the alias rests on a native-decompile reading only, and it would couple sauron to later
  elf edits.
- **XSLT removal of vanilla `str_become_king_empire`:** kept. Two lenses note the keyed merge would let TAOM's
  row win anyway; the removal is the file's house pattern and keeps the re-added row from inheriting vanilla
  children. A test pins it.
- **17 tag classes:** forced by the engine (no shared or generic base can exist).

## Not applied

- A `BANNERLORD_GAME_DIR` override for the gate's install path: the test class hardcodes the path in its other
  LiveInstall test too, so it is a class-wide change.
- Vanilla tag ids from StoryMode in the uniqueness check: the `taom_culture_` prefix cannot collide, and the
  test project does not reference StoryMode.

## Follow-up

- `docs/reference/lotrlome-armory-snapshot/README.md` "Each facegen action_set must contain ALL ... directly
  declared" said the engine does not follow `base_set`; corrected in this change on Mike's word (the parser copies
  the base's actions; `as_human_warrior` simply holds no character-creation actions).
