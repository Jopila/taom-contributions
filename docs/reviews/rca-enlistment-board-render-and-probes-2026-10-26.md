# RCA: The service board drawn once per change, and menu probes (#1019, deep-review, 2026-10-26)

## Top-line

#1019 recorded four items the reviews said needed a game run. An architect settled what the code could settle:
a raid reaching the village an enlisted soldier stands in does not strand him (the engine interrupts the join
TAOM seeds and opens its own "interrupted" menu; for the raid itself, when he shares the village's faction it
re-joins him on the defender side, and otherwise it walks him out of the village), so it gets one diagnostic log
line; the per-frame redraw of the service board was safe to fix without a measurement, because the board renders
the same as one finished string; and the other two costs get timing probes in the existing map profiler. The
first `/deep-review` (standards, engine and data-flow lenses) found **no runtime defect**. It found one wrong
decision about the patch shield, docs that described the engine wrongly and tests weaker than their names. A
convergence pass then found doc errors (the shield exclusion's reason and real effect, the raid branch), test
gaps, and one display risk: the flattened board lost its line breaks when the `NEWLINE` global was unset at write
time. All were fixed before the push.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Data flow) | The new menu-tick probe would be the only patch on `GameMenuVM.OnFrameTick`, yet it was left under the patch shield. Profiler-only targets are excluded for parity: with the profiler off nothing TAOM patches this method either, so the profiler must not change what happens when it throws. The exclusion's real effect is that a missing-API error inside it is not caught there but leaves through `GauntletMenuBaseView.OnFrameTick` and `MenuViewContext.OnFrameTick` (neither catches) into the shielded `MapScreen.OnFrameTick`, which skips the rest of the map screen frame and strips Patch36, Patch89 and Patch101 there, ending profiling. Another mod's patch on it loses the shield even with the profiler off. The docs also claimed the view probes were shielded, which they never were. | A decision made by analogy instead of by the rule | The builder copied the view probes, whose methods are TAOM's own and never shielded. | Excluded for parity, with the shared setter left shielded (a swallow there strips Patch64's patch with the profiler's pair); tests extend both lists; docs corrected, including the per-target table. |
| F2 | LOW (Data flow) | The not-serving text was flattened too; it has no variables, so nothing was saved, and it lost its own catch-up after a language switch. | Over-application | Applied to both write paths. | Kept as a text object; a test pins it. |
| F3 | LOW (Engine compat) | Docs named the wrong engine step for the raid trace (the encounter start, not its initialisation, and the interrupt, not the walk-out), a log string carried an engine line number, and the saving was described as one variable lookup instead of one flat pass. | Accuracy | Written from a summary of the trace. | Corrected against the engine lines; no line numbers in runtime strings. |
| F4 | LOW (Standards) | A test compared a probe's target with itself; another depended on test order to load the game assemblies; the central rendering claim was pinned only by a source search; comments repeated code; the API snapshot was stale. | Test strength and hygiene | Routine. | Tests assert real names and load assemblies; a game-category test renders the board and compares; snapshot regenerated. |

## Convergence pass

- **Shield exclusion.** The first write-up gave a wrong reason for excluding `GameMenuVM.OnFrameTick`. Traced
  against v1.5.5: an unshielded error leaves through `GauntletMenuBaseView.OnFrameTick` and
  `MenuViewContext.OnFrameTick` into `MapScreen.OnFrameTick`, which is shielded, so the real effect is a skipped map
  frame and stripped Patch36, Patch89 and Patch101. The reason is parity with the profiler off. The feature doc's
  per-target table now has rows for the excluded method and the shielded `ContextText` setter.
- **Raid branch.** `EncounterManager.StartSettlementEncounter` joins the defender side only when the soldier's
  faction is the village's; otherwise `LeaveSettlementAction.ApplyForParty` walks him out. The feature doc, the
  lesson and this top line now say so, and `sameFactionAsSettlement` in the diagnostic line is documented as that
  test (True: re-joined on the defender side; False: walked out).
- **Line breaks.** `{NEWLINE}` is a global the engine sets only lazily inside its UI builders, and ending a
  campaign clears it. A board flattened while it was unset stayed on one line until the next status change. `Write`
  now sets the global to `"\n"` (the engine's own value) just before the flatten; a game-category test removes the
  global first and asserts the stored board contains a line break (it failed before the change).
- **Test gaps.** The not-serving test checked source text only; it is replaced by a game-category test that
  reads the stored variable and asserts the unflattened `{=taom_enlist_wait_discharged}` text object. A reverse
  check now asserts that no TAOM patch class outside `*_MapProfiler_Patch` targets one of the three profiler-only
  methods.
- **Not changed.** The `ContextText` setter stays shielded. The missing hook-health check for Patch64's patch
  (`UnpatchedCore` covers only core targets) is documented, not added.

## Feedback memories to codify

None.
