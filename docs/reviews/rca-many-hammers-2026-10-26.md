# RCA: Many Hammers (#911), deep review of 2026-10-26

## Top line

The tier 1 review of Many Hammers (Codex at ultra, then two Codex fix passes at high; Lenses 5 Data flow, 2 Engine and
7 XML; a second wave of Lenses 1 Standards, 3 Efficiency, 4 Completeness and 6 Design; one convergence pass that confirmed parity on every changed path) found one
HIGH and fixed it: with "Hide locked items" on, smelting the last unlocked weapon while a locked one was in the party
threw in vanilla's reselect, after the weapon was consumed. Codex and Lens 5 found it independently; Lens 2 showed the same
throw on a co-op guest through BannerlordCoop's refresh. The bulk loop itself was found sound on every path: each unit
goes through the engine's own `DoSmelting` or `DoRefinement`, prices come from the model slot (so #910's cap and the
cost feats apply), and vanilla always does the last unit and every refresh. All other confirmed findings are fixed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `FilterLocked` left a gone, unlocked row selected when it emptied the list; vanilla's reselect then passed `OnItemSelection(null)` and threw at `SmeltingVM.cs:222`, outside every TAOM catch (Codex, Lens 5; Lens 2: also on a co-op guest) | A postfix broke an invariant vanilla enforces before it | Vanilla clears the selection for an empty list at `:207-210`, before the postfix runs; the rule was written as "move a LOCKED selection", and no test covered "every row locked, selection unlocked" | No row left always clears a selection; tests on the rule and on real view models (`SmeltingListFilterTests`); lesson (harmony-il) |
| 2 | MEDIUM | Clearing the selection from the lock-click postfix left the Smelt button and the preview stale: the parent's selection callback is private (Codex) | Postfix without the caller's refresh | The lock click has no follow-up refresh, unlike every `RefreshList` caller | The lock-click postfix is deleted on the maintainer's decision (finding 9) |
| 3 | MEDIUM | The locked-selection guard that keeps bulk smelting behind vanilla's locked-item confirmation lived only in the patch, untested (Lenses 1 and 4) | Feature rule in a patch | The plan placed it in the prefix | `DesiredCount(..., selectionLocked)` returns 1; tested |
| 4 | MEDIUM | Three binding tests constructed `Crafting.RefiningFormula` (engine code) without `RequiresGame`, so the reference-assembly unit step would fail (Lens 1) | Test category | Repeat of the testing-qa lesson; the builder brief did not name the category rule | Tagged; recurrence line (testing-qa) |
| 5 | LOW | Trunk took `Patch119` while the branch was open (Lens 5) | Patch number collision | Numbers are picked from trunk at plan time | Renumbered `Patch121`; re-checked at merge |
| 6 | LOW | After a locked selection, the filter picked the first unlocked row, not the same weapon, so the next Ctrl click could smelt a different stack (Lens 5) | Selection drift | Vanilla's own reselect is by item; the filter did not follow it | Same item first; tested |
| 7 | LOW | The loop re-planned the whole remainder before every unit (quadratic); only "two still fit" is read (Lenses 3 and 6) | Wasted work | Written as a full re-plan | `PlanUnits(2, ...)`; the RunExtras tests are unchanged |
| 8 | LOW | Simplicity: `bool` prefixes that never return false, a literal category repeated beside the module constant, a forwarding service method, a `Kept` list nothing used, a reflection test the compile already proves (Lenses 1 and 6) | Unneeded code | Builder defaults | Void prefixes, the constant, direct calls, a selection-only result; the test deleted |
| 9 | LOW | The lock-click postfix only hid an unselected row a moment early, at the cost of a patch on a private method and a misclicked lock that could not be undone in place (Lens 6) | Patch with little value | Built to the spec | Deleted on the maintainer's decision (2026-10-26): a row locked in the smithy hides at the next smelt or tab switch |
| 10 | LOW | Labels in sentence case, beside Title Case labels in the same group (Lenses 7 and 1); no test tied the hints' defaults and "five" to the code (Lenses 7, 1, 4) | Wording and missing tests | Copied from a sentence-case sibling | Title Case in all 13 rows; wording and settings tests |
| 11 | LOW | Docs: wrong `RefreshList` callers, "a level can change the next price" (none can in v1.5.5), a malformed feature-map row, stale lines (Lenses 2, 4, 5) | Wrong text | Written before the engine read | Corrected |
| 12 | LOW | The fix pass left the patch registry with a duplicated block: Patch111 to Patch117 repeated, two `Patch121_ManyHammers` sections, the first spliced with Patch110's bard text (Codex, second fix pass) | A doc edit duplicated a block | An edit that inserted text by anchor matched once and wrote the section twice; `PatchCategoryNumberTests` reads headings as a set, so duplicates pass | Repaired: one Patch121 section, every heading once; the diff against the base is the section alone |
| 13 | LOW | After the fixes, two comments and a doc line still described the deleted lock-click path or the old label case, and the doc read as if Yes on a locked selection bulk-smelted (convergence pass) | Stale text after a deletion | A deletion's search covered code and tests, not every comment that explained it | Rewritten; a repo-wide search for the deleted name leaves only the line that says vanilla's own method records the lock |

## Root-cause pattern

**A postfix that edits a view model's state must keep every invariant vanilla's caller relies on after it returns (1, 2).**
Vanilla's `SmeltSelectedItems` assumes "empty list means no selection" because its own `RefreshList` makes it so; the
postfix changed the list after that check ran. Before a postfix changes a list, a selection or a flag, read what the
caller does with it next (here `SmeltingVM.cs:273-279` and BannerlordCoop's handler) and re-establish the same invariant.

## Why each reviewer missed what it missed

- **Codex (ultra):** found 1 and 2 by running the filter against the installed view models; it did not check test
  categories or docs.
- **Lens 5:** found 1 independently, 5, 6 and 11; it did not see 4.
- **Lens 2:** added the co-op guest path to 1, the right shape for the fix of 2, and 11.
- **Lens 7:** found 10; the XML itself was clean.
- **Second wave:** Lens 1 found 3, 4 and 8; Lens 3 found 7; Lens 4 found the missing tests behind 1 and 3; Lens 6 found
  7, 8 and 9.

## Not run

No game session has run this build: the Shift and Ctrl reads inside the smithy, the empty-list case, the locked-weapon
confirmation under Ctrl and a co-op guest are owed in game (`docs/features/many-hammers.md`).
