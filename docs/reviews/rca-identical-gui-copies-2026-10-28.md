# RCA: GUI prefab copies identical to vanilla (#1051 steps 1, 2 and 5, deep-review, 2026-10-28)

## Top line

TAOM shipped eight GUI prefab copies that were identical to the installed v1.5.6 vanilla files. They carried no TAOM change
and only hid later vanilla changes, which is how the Execute button lost its blood-feud mark (#1031). The change deletes them,
adds a gate against such a copy, and changes the stale-clone procedure from re-sync to delete. `/deep-review` ran seven lenses
in two waves. It found no HIGH or MEDIUM finding; every LOW finding was fixed before the commit. Three lenses proved the
deletion neutral at runtime: Gauntlet v1.5.6 resolves each name to the one vanilla file, which is XML-equal to the deleted copy.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | LOW | The new rule line said the gate catches a "pure stale copy". It catches only a copy identical to the installed file; a copy of an older vanilla differs and passes. | The line was written from the eight cases at hand, which all matched v1.5.6. | The rule says what the gate catches and that step 1's diff finds a stale copy. |
| 2 | LOW | The gate compared text with all whitespace removed, unlike the engine, which ignores comments and keeps attribute values exactly. A copy differing only by a TAOM comment would pass; one differing by a space inside a value would be flagged. | The first version proved the eight cases, not the engine's reading rule. | `AsTheEngineReadsIt` loads with comments removed and compares with `XNode.DeepEquals`; a one-off proof with a TAOM comment added to a restored copy failed the gate, as it should. |
| 3 | LOW | "Delete it so vanilla loads" was incomplete: a deploy never deletes, so the installed copy keeps shadowing vanilla, including at the engine bump's first start. | The live deletion for this change was done by hand and not written into the procedure. | The rule and the engine-bump skill say to delete the deployed copy too. |
| L | LOW | Other LOW findings, all fixed: step 1 still diffed against vanilla 1.4.5; a hand count ("~32 of 48 clones") the change made wrong; the engine-bump filter did not run the gate (now `BindingVerification`, with `RequiresGameIL`); the sibling test's message and the skill said only "re-base"; the pairing walk and its empty-walk assert were copied in two tests (now `ShadowPairs`); `v1.5.2-impact.md` still listed `CompositionSlider` as a shadow. | Hygiene. | Fixed. |

## Accepted limits

- The gate cannot see a copy frozen at an older vanilla version; that is #1051's clone audit, still open with the 12 copies
  that differ from vanilla.
- `XNode.DeepEquals` counts attribute order and empty-element form, so a copy re-serialized by a tool would pass the gate.
  None of the 24 remaining pairs matches even under a lenient comparison.
- Four gate classes keep their own repo locator instead of `RepoPaths.RepoPath`: older code, a separate change.
- A build from another checkout before this merges redeploys the eight copies; players who upgrade in place keep them.

## Lesson

None new: `lessons/localization-ui.md`, "Pin a GUI prefab copy to the vanilla file it replaces" (#1031), already says to
delete a copy with no TAOM edit; this change made it a gate.
