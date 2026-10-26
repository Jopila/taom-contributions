# Codex pre-review: Many Hammers (#911), 2026-10-26

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Scope is the uncommitted working tree of this
worktree on top of `069962aa4` (`git diff` and `git status`; new files are untracked:
`Client/Features/ManyHammers/`, `Main/Features/ManyHammers/`, `Main/Adapters/*Bench*.cs`,
`Main/Adapters/ICraftBenchUnit.cs`, `TAOM.Tests/Features/ManyHammers/`, `docs/features/many-hammers.md`).
Engine source for v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\` (for example
`TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.Smelting.SmeltingVM.cs`,
`...WeaponCrafting.Refinement.RefinementVM.cs`, `...WeaponCrafting.CraftingVM.cs`,
`TaleWorlds.CampaignSystem.CampaignBehaviors.CraftingCampaignBehavior.cs`,
`TaleWorlds.CampaignSystem.GameComponents.DefaultSmithingModel.cs`; list the directory for exact names).
Mark anything you cannot prove from code as UNVERIFIED.

## The change

In the smithy, holding Shift while smelting or refining does 5 at once and Ctrl does the whole stack
(Ctrl wins). Locked items leave the smelting list. Patch119 (TAOM.Client, four classes):

- Prefix on `SmeltingVM.TrySmeltingSelectedItems(Hero)` and on `RefinementVM.ExecuteSelectedRefinement(Hero)`:
  with a modifier held, plan N units from a snapshot (stamina, the model's energy cost, the model's
  smelting output including its negative charcoal slot, the main party's material stock, the stack
  count), run N-1 extra units through `ICraftingCampaignBehavior.DoSmelting` / `DoRefinement`
  (re-planning from a fresh snapshot before each and continuing only while at least 2 still fit),
  then return true so vanilla does the last unit and every refresh. A locked selection, a count of 1,
  a co-op guest (`ShouldDeferToHost`) or the switch off pass through untouched.
- Postfix on `SmeltingVM.RefreshList` and on private `SmeltingVM.ProcessLockItem(SmeltingItemVM, bool)`:
  remove locked rows and fix the selection (reselect the first remaining row or clear it).
- Pure rules in `Main/Features/ManyHammers/BulkCraftRules.cs`, the service in `ManyHammersService.cs`,
  the engine side in `Main/Adapters/SmeltBenchUnit.cs`, `RefineBenchUnit.cs`, `CraftBenchStock.cs`.
- Two MCM toggles, both default on.

## Questions to answer with evidence

- Affordability: does the plan's rule (stamina > 10 and stamina >= cost, no material slot negative,
  bounded by the stack) match the gate vanilla applies to a single click (`CraftingVM` main-action
  enable, about :1038-1046)? Can the extra units, or vanilla's last unit after them, run when vanilla's
  own gate would refuse (negative charcoal, an item no longer in the roster, stamina below cost)?
  Can the plan smelt an item the player did not select, or a different modifier variant of it?
- Side effects per unit: XP, research points and piece unlocks, events, notifications. Is anything
  done once per click by vanilla (the confirmation for a locked item, sorting, `UpdateAll`) that the
  extra units skip or double? Does the screen show stale numbers after a bulk run?
- The lock filter: is removing a row from the list inside `RefreshList`'s postfix and inside
  `ProcessLockItem` (called from the item VM's constructor and from a lock click) safe? Can it recurse,
  remove the wrong row, leave a selected item that is no longer in the list, or let a locked item be
  smelted? Is the vanilla lock store read correctly?
- Harmony: parameter names bound correctly (`currentCraftingHero`), private target resolved, return
  values right, every body exception-safe; a throw never skips vanilla's unit or leaves state half done.
- Co-op: a guest's DoSmelting is forwarded to the host by BannerlordCoop (UNVERIFIED unless you can read
  it); is bulk correctly off for a guest, and is any path wrong for a host?
- Assemblies: core code (Main) must not name client-only assemblies (ADR-013, ServerJitGateTests); the
  patches live in Client. Any registration, settings-count or classification test that breaks?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the
proving code or a reproduction. Say PASS for a question you checked and found sound.
