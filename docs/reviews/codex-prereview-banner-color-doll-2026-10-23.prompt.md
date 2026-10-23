# Codex pre-review: Banner Color Persistence inventory doll (#973), 2026-10-23

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Scope is the uncommitted working tree of this worktree
on top of `origin/bannerlord-1.5.x` (`git diff`, `git status`). Engine source for v1.5.5 is in
`C:\Users\mikew\.taom-src\v1.5.5\` (for example
`TaleWorlds.CampaignSystem.ViewModelCollection.Inventory.SPInventoryVM.cs`); signatures come from the
installed DLLs. Mark anything you cannot prove from code as UNVERIFIED.

## The change

`Main/Features/BannerColorPersistence/Hooks/SPInventoryVM_UpdateCurrentCharacterIfPossible_Patch.cs` moves
its Harmony postfix from `SPInventoryVM.UpdateCurrentCharacterIfPossible(int, bool)` to the private
`SPInventoryVM.UpdateCharacterArmorColor()` (:4421), and skips the stealth case
(`EquipmentMode == Stealth && FirstStealthEquipment.IsStealth`). The class keeps its name because
`Main/SubModule.cs:644` (single owner, not editable here) calls `Initialize`. New tests:
`TAOM.Tests/Features/BannerColorPersistence/SPInventoryVM_ArmorColor_PatchTests.cs`. Docs: the feature doc
and the regenerated row in `docs/reference/taleworlds-api-snapshot/patch-targets.md`.

## Questions to answer with evidence

- Does every vanilla doll colour write go through `UpdateCharacterArmorColor`? `UpdateCurrentCharacterIfPossible`
  also writes `ArmorColor1/2` directly (:4311-4312) before calling `UpdateRightCharacter`: is the final value
  still the clan's? Any other writer of `MainCharacter.ArmorColor1/2` in `SPInventoryVM`, `HeroViewModel.FillFrom`
  or TAOM (`HeroViewModel_FillFrom_Patch` in the same feature)?
- Is the stealth check equivalent to vanilla's `ActiveEquipment.IsStealth` in every mode and for non-hero
  characters? Can `FirstStealthEquipment` throw (non-hero with no culture roster, `Campaign.Current` null)?
- Harmony: does the target resolve on the installed DLL; is it inlined or too small to patch reliably under
  the JIT (it is a private non-virtual method; the reviews measured 125 IL bytes, not the "about ten instructions" first asked here)? Any other mod or TAOM patch on the
  same method? Category `Patch23_BannerColorPersistence` timing and PatchShield interaction.
- Performance: how often does `UpdateRightCharacter` run (per transfer?), and is the per-call `Traverse` cost
  acceptable?
- Co-op or dedicated server: is this patch reachable or harmful there?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact and the proving code.
Say PASS for each question checked with no defect.
