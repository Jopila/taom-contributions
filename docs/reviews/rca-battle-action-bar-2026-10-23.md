# RCA: battle action bar never ticked, and would have blocked combat once it did (#970), 2026-10-23

**Summary.** The Companion Tactics battle action bar never worked in any build. Two faults stopped it: its
mission view was added in `Mission.AfterStart`, after `MissionScreen.OnMissionAfterStarting` registers the
views, so it never ticked; and it attached only if the mission mode was Battle at screen init, which is
Deployment in every field battle with a deployment phase. The fix makes it a `[DefaultView]` that attaches
on the first Battle-mode tick, and deletes two factory seams and an inert second controller instance. Risk
tier 1: Codex (ultra) and the Data flow and Engine compatibility lenses, then a re-review of the fixes (Codex
at high and the Engine lens) because the first wave found a HIGH. Mike parked the feature on the evidence:
it is off by default and its stances change nothing in battle.

Sources: Codex outputs `codex-actionbar.out.md` and `codex-actionbar-2.out.md` (session scratchpad), prompts
`docs/reviews/codex-adversarial-battle-action-bar-2026-10-22.prompt.md` and
`docs/reviews/codex-adversarial-battle-action-bar-fixes-2026-10-23.prompt.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Once attached, the layer's default `SetInputRestrictions()` showed the cursor, which stops vanilla attack and block (`MissionMainAgentController` runs them only while the cursor is hidden) and turns the camera into an edge scroll; the full-screen root also took every click | Never-run code made live | The bar never attached, so this code had never run; `gui-ui.md` recommended the parameterless call for mission overlays | Mouse buttons only, `DoNotAcceptEvents` on every non-button widget, a prefab test that checks every one; correct the `gui-ui.md` guidance |
| 2 | MEDIUM | Keys 1 to 8 are vanilla's formation-select keys, read every tick; the bar polled the same keys | Input collision | Never ran | Hotkeys removed (Mike: click-only) |
| 3 | MEDIUM | The badge showed a fixed number, the key picked by list position | Data mismatch | Never ran | Removed with the hotkeys |
| 4 | MEDIUM | Turning the setting off mid-battle left a frozen bar holding input | Lifecycle | Never ran | Detach on disable; re-attach once |
| 5 | MEDIUM | With the cursor hidden, the engine pins it to the screen centre, so no bar button can be clicked in combat | Engine fact | Found only by reading the native input update | Recorded; feature parked off by default |
| 6 | LOW | Stances could outlive a mission that ends without EndMission; an unused field; a short-name attribute match in the new gate | Small gaps | | Fixed |

## Root-cause pattern

A feature that never ran carried every defect a first run would have found. Making it run (the #970 fix)
turned dormant code live at once. When a fix makes old code reachable for the first time, review that code
as new code, not only the lines the fix changed.

## Lesson

Appended to `docs/reviews/lessons/state-lifecycle-save.md` (the late add and the mode gate). The rule above
belongs with them: a fix that makes dormant code reachable reviews that code as new.
