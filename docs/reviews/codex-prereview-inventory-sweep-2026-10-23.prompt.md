# Codex pre-review: inventory bug sweep (#924, #925, #926), 2026-10-23

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Scope is the uncommitted working tree of this
worktree on top of `origin/bannerlord-1.5.x` (`git diff` and `git status`). Engine source for
v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\` (for example
`TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM.cs`); signatures come from the
installed DLLs. Mark anything you cannot prove from code as UNVERIFIED.

## The three changes

1. **#924 (EquipPresets).** `InventoryScreenAdapter.RefreshActiveVM` called `SPInventoryVM.RefreshValues`,
   which only resets labels (SPInventoryVM.cs:3182). It now calls the new
   `InventoryVMAdapter.RefreshPanel(vm, logger, tag)`, which runs the private `UpdateRightCharacter` and
   `RefreshInformationValues` through cached open delegates, then the public `ExecuteRemoveZeroCounts`, in
   the order of vanilla `ProcessEquipItem` (:3475-3486).
2. **#926 (Quick Actions).** `InventoryVMAdapter.RefreshDisplay` now calls the same `RefreshPanel`, so batch
   sells (`ProcessSellItem(item, cameFromTradeData: true)`, which skips the prune at :3565-3568) no longer
   leave count-0 rows.
3. **#925 (Quick Actions).** Removes the MCM options `SellDamagedEquipped` and `SellLowValueEquipped`, their
   provider and interface members, `IInventoryItemAdapter.IsEquipped`, the four tests that faked
   `isEquipped: true`, and updates the co-op settings counts (`SettingsFingerprintTests`, two docs). The
   claim: the options could never act, because `GetRightPaneItems` wraps `RightItemListVM`, which holds only
   `PlayerInventory` rows (:4519-4522, :3885).

## Questions to answer with evidence

- Can `ExecuteRemoveZeroCounts` after a preset load, a batch sell or Unequip All remove a row the player
  still needs (selected row, a row a later command in the same batch expects, the left pane)? Does any
  vanilla or TAOM code hold an `SPItemVM` reference across that call (Telchar's Mark Patch109 in
  `Client/Features/TelcharsMark/`, `_selectedItem`, Quick Actions' item list snapshot)?
- `RefreshActiveVM` no longer calls `RefreshValues`. `Patch33_SPInventoryVMRefresh` postfixes
  `RefreshValues` to capture the VM. Does anything depend on that extra call after a load?
- Is the #925 claim true: can any row in `RightItemListVM` have an equipment `InventorySide`? Does
  removing two settings break MCM load of an existing settings file, co-op settings parity, or a
  localization or settings registry?
- Any caller of the changed members outside the diff that breaks (`Client/` included).
- Data flow: does each refresh run once per batch and on the VM the transfers went to?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving
code. Say PASS for a question you checked and found no defect in.
