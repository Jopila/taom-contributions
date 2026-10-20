# RCA: Crown Guard (#825, deep review 2026-10-20)

## Top-line

Root cause, confirmed from the installed v1.5.5 code (Mike accepted the static proof):
`CampaignSceneNotificationHelper.GetBodyguardOfCulture` switches on seven vanilla culture ids and returns
`fighter_sturgia` for any other, so the become-king scene shows Sturgian fighters beside a TAOM ruler and
Calradian elites beside the rulers of the cultures that reuse vanilla ids. The fix is a void postfix,
Patch112, that replaces the guard with the troop a JSON row names for the culture.

The deep review (six lenses in two waves, then one convergence pass) found no runtime defect. It found
one MEDIUM finding and thirteen LOW ones, all in docs, tests or shape, plus one design change (the
crowned culture's colours for borrowed rows). All were handled in the same session except F13 and
F14, which are documented. The convergence pass found two prose slips in the fix round (F15).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | Issue #825's body still described the dropped design: a prefix, a `SceneBodyguards` folder, startup validation and an adapter | Docs drift | The approved plan went to the issue as a progress comment, which the lesson asked for; the body stayed stale. The Bard Town Guard RCA (F3) had promised this exact check for #825 | Body rewritten from what ships. The lesson now asks for the body's Solution and Files Changed to be rewritten, not only a comment |
| F2 | LOW | Feature doc and registry said the target has one caller; Story Mode's `EmpireConspiracySupportsSceneNotificationItemBase` is a second | Engine claim from the plan | The caller list came from the plan's research, which found the second caller and set it aside as unreachable; the docs dropped it | Both callers named, with why the second is unreachable. Lesson in `harmony-il.md` |
| F3 | LOW | Registry said the scene shows "when a player or lord wins"; only a player win raises it | Engine claim from the plan | Written from the summary, not from `OnKingdomDecisionConcluded` | Corrected, citing the gate |
| F4 | LOW | Registry PatchShield paragraph: an inexact exclusion claim, and no first-game timing or co-op no-install case, which the Patch110 and Patch111 sections state | Template not followed | The builder wrote the paragraph fresh instead of copying the neighbour's | Rewritten in the neighbours' form |
| F5 | LOW | "The kingdom's own culture" and "vanilla's Sturgian fallback" overstated: three rows borrow another culture's troop and so wore that culture's colours; six cultures had Calradian elites, not the fallback | Design and wording | The plan named the borrowed rows but not the scene's colour rule (`OpenScene` tints from `character.Culture`) | The postfix passes the crowned culture's colours (one line; no change for the 20 own-culture rows). Wording corrected in the doc, registry, feature map, index and module summary |
| F6 | LOW | No test applied the category to the installed engine and JIT-compiled it; `HarmonyPatchBindingTests` only resolves the target | Sibling test not copied | The builder copied the sibling's code shape but not its binding test | `CrownGuardBindingTests`. Repeat of the Bard Town Guard F6; lesson in `testing-qa.md` |
| F7 | LOW | The module summary said "no model, behavior, setting or save data"; only save data was pinned | Sibling test not copied | Same as F6 | `TheModule_AddsNoBehaviorAndNoModel` |
| F8 | LOW | The shipped-data troop test was a raw regex over every ModuleData XML file, `Languages` included, run twice; it would pass a troop that exists only inside a comment, and it did not fail on a stale extra key | Weak gate | Written for speed of authoring; the sibling scans differ | One XDocument scan in `[ClassInitialize]` that skips `Languages`; the keys must equal the kingdom cultures |
| F9 | LOW | The new per-kingdom-culture gate was not named in `recipe-add-a-kingdom.md` or `new-culture-authoring.md` | Gate not routed | The completion list does not name the authoring recipes; repeat of the culture-fallback follow-ups F12 | Rows added to both |
| F10 | LOW | A hand-written count ("23 kingdom cultures") in the doc and the feature map, pinned by nothing (the floor is 20) | Hand count | Repeat of the follow-ups F10 | Replaced by "every kingdom culture"; the keys-equal test pins the set |
| F11 | LOW | Feature doc: "Dependencies: None" (it relies on Patch56, this scene's visual net), no Current Values for the borrowed, shared and troll rows, the interface missing from Key Files | Template not followed | Written from the plan | Sections written; the troll exclusion is explained |
| F12 | LOW | `CrownGuardService` was a one-line pass-through with an interface whose only use was its own fake | Over-structure | The brief asked for the LordBanners shape, whose service has real logic | Lookup moved into `CrownGuardMap`; service and interface deleted |
| F13 | LOW | The hook's branch for a mapped troop that is not loaded has no unit test (it reads the static `MBObjectManager.Instance`) | Test gap | No seam, and a seam for a log line fails the simplicity criterion | Documented under Tests in the feature doc; the shipped-data test keeps every troop present |
| F15 | LOW | Convergence pass: the module summary kept the F5 wording, and the feature doc said every row is the top troop of its tree, which four guard rows are not | Fix-round slip | The fix round edited the module file for F12 and missed its summary; the new Current Values sentence was written from the plan's summary of the table | Both corrected; the doc names the four guard rows that sit one tier below the top |
| F14 | INFO | The owed in-game check needs a won king election, and no console command raises the scene | Verification cost | Out of scope for the fix | In-game expectations written (troop, colours, on foot, the scene's own spear). A staging command is offered to Mike as a follow-up |

## Root-cause pattern: docs and tests written from the plan and the sibling's code

F2 to F5 and F11 are statements that came from the plan's summary rather than from the engine code
that was read during the plan: the plan found both callers and the player-only gate, and the docs
kept a shorter version. F6 and F7 are tests the sibling has that the copy lacks: the builder copied
the sibling's code shape and wrote its own tests. Both repeat the Bard Town Guard RCA (F6, F8). The
fix in both cases is to write the docs from the engine lines and to copy the sibling's test list as
well as its code.

## Why each agent missed these

- **The builder:** briefed with the plan and the sibling files, it copied the code shape and wrote
  the docs from the brief. It had no instruction to copy the sibling's test list or to check the
  caller count in the IL.
- **The orchestrator's spot check:** read the hook, the service and the shared-file diffs, which
  were correct; the defects were in the docs and in missing tests, which a code read does not show.
- **All six lenses** reported their part: Standards F2; Engine F2 to F6; Efficiency F8; Data flow
  F2, F3, F8; Completeness F1, F7, F9 to F11, F13, F14; Design F5, F8, F12.

## Lessons recorded

- `lessons/build-tooling-workflow.md`: the issue-body lesson now asks for the body to be rewritten.
- `lessons/harmony-il.md`: a patch doc's caller list and trigger come from the IL and the raising gate.
- `lessons/testing-qa.md`: a feature copied from a sibling copies the sibling's test list.
