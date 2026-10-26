# Codex fix review: Many Hammers (#911) second-wave fixes, 2026-10-26

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` first. Scope is the uncommitted working tree of this worktree on top of `069962aa4`.
Engine source for v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\`; BannerlordCoop source is in
`E:\repos\BannerlordCoop\source`. Mark anything you cannot prove from code as UNVERIFIED. Earlier
passes: `docs/reviews/raw/codex-prereview-many-hammers-2026-10-26.md` and `...-fixes-2026-10-26.md`
(all four earlier fixes PASS).

## The changes to verify (do not re-review the rest)

1. The `ProcessLockItem` postfix is deleted (maintainer's decision): a row locked inside the smithy
   stays visible until the next `RefreshList`, whose postfix hides it.
2. The locked-selection guard moved from the smelt prefix into
   `ManyHammersService.DesiredCount(shift, ctrl, stack, selectionLocked)`, which returns 1 for a locked
   selection or inactive bulk; the smelt prefix passes `selected.IsLocked`, the refine prefix `false`.
3. Both prefixes are `void` now (they never skipped vanilla).
4. `BulkCraftRules.SelectionAfterHidingLocked` replaces `FilterLocked` and returns only the selection
   decision; `SmeltingListFilter.Apply(vm)` removes rows by their lock flag.
5. `RunExtras` re-checks `PlanUnits(2, snapshot)` before each extra unit instead of the whole remainder.
6. Patch categories use `ManyHammersModule.PatchCategory`.

## Questions

- With 1, can a player now smelt a locked weapon without vanilla's confirmation, by any order of
  lock clicks, selection, Shift or Ctrl clicks, or a tab switch? Is the locked selection still sent to
  vanilla's question with bulk inactive for it?
- Is 2 equivalent to the old patch-side checks on every path (bulk off, co-op guest, locked, null
  selection, null hero)? Does the refine prefix's `false` lose anything?
- With 3, is a void prefix with no `__result` and no return fully equivalent for Harmony, and do the
  catch blocks still keep vanilla running after a throw?
- Is 4 identical to the old outcomes on every input (empty list, no selection, locked selection with
  and without a same-item row, short sameItem)?
- Is 5 equivalent to the old loop for every snapshot sequence, including a snapshot whose cost or
  stock changes between units?
- Any test, count, category or binding gate the changes break; any doc that still describes the
  removed patch.

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact and proof.
Say PASS for each change you verified.
