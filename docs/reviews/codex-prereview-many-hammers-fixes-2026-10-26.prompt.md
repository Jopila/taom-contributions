# Codex fix review: Many Hammers (#911) review fixes, 2026-10-26

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` first. Scope is the uncommitted working tree of this worktree on top of `069962aa4`.
Engine source for v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\`; BannerlordCoop source is in
`E:\repos\BannerlordCoop\source`. Mark anything you cannot prove from code as UNVERIFIED. The earlier
pass is `docs/reviews/raw/codex-prereview-many-hammers-2026-10-26.md`.

## The fixes to verify (do not re-review the rest)

1. HIGH (yours): `BulkCraftRules.FilterLocked(locks, hasSelection, selectionIsLocked, sameItem)` now
   returns `Clear` whenever no row is kept and a selection exists, before the locked-selection check.
   `Client/Features/ManyHammers/Hooks/SmeltingListFilter.cs` passes `CurrentSelectedItem != null`.
   Earlier: smelting the last unlocked weapon with a locked one in the party left a gone row selected
   in an empty list, and vanilla's reselect called `OnItemSelection(null)` (`SmeltingVM.cs:273-277`).
2. MEDIUM (yours): the `ProcessLockItem` postfix no longer runs the whole filter. It removes only the
   clicked row, and only when `BulkCraftRules.RemovesOnLockClick(isLocked, inList, isSelected)` holds
   (locked, in the list, not the selection). A locked selection stays for vanilla's confirmation; the
   next `RefreshList` moves it through `ExecuteSelection()`, which refreshes the parent.
3. A locked selection now moves to the first kept row of the same item (by `EquipmentElement.Item`),
   else the first kept row.
4. Renumbered `Patch119_ManyHammers` to `Patch121_ManyHammers` (trunk took 119; 120 is Harvest Road).

## Questions

- Is each fix right and complete on every `RefreshList` caller: the constructor, `SmeltSelectedItems`,
  `CraftingVM.ExecuteSwitchToSmelting`, and BannerlordCoop's `SmithingVMsHandler.Handle_NetworkRefreshSmelting`?
  Does any caller still reach `OnItemSelection(null)`, or leave the parent stale after a `Clear`?
- After fix 2, can any lock click (on the selection, on another row, on a row while the selection is
  locked, unlock clicks) leave the list, the selection or the Smelt button inconsistent, or let a
  locked item be smelted without vanilla's confirmation?
- Does fix 3 ever pick a row that is locked, out of range, or of a different modifier when the same
  modifier is available? Is preferring the same item over the same element the right key?
- Any test that now asserts a state the engine cannot produce; any count, category or binding gate the
  renumber breaks.

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact and proof.
Say PASS for each fix you verified.
