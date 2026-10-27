# Codex review: NRE containment guard batch (uncommitted, 2026-10-27)

You are an adversarial reviewer for TAOM (Bannerlord v1.5.5 mod, .NET Framework 4.7.2, Lib.Harmony
2.4.2). Read `AGENTS.md` and `.ai/roles/reviewer.md`. Review only: no edits, no builds or test runs
(another session shares this checkout and its test bin), no other AI calls. Report defects with
file:line, impact and proof; try to refute each claim; mark anything unverified UNVERIFIED.

## Requested outcome

Each guard must stop its known vanilla crash without hiding unrelated faults, and must not re-open a
crash it used to contain.

## Change under review (uncommitted, on top of HEAD; `git diff HEAD -- <file>`)

- Patch123 `Main/Features/Encyclopedia/Hooks/HeroHelper_GetLastSeenText_Patch.cs`,
  `Main/Features/Encyclopedia/LastSeenState.cs`: the finalizer you reviewed twice is now a bool
  prefix on `HeroHelper.GetLastSeenText` (player crash 9b2a103e).
- Patch49 `Main/Features/ArmyTargeting/Hooks/Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.cs`:
  narrowed to two dead-end states.
- Patch20 `Main/Features/CharacterCreation/Hooks/CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`
  (the `CharacterCreationNarrativeStageView_SpawnNonHuman_Patch` class and
  `NarrativeHorseGuardPatchHelper.CanSpawnMount` only): finalizer replaced by a prefix.
- Patch50 `Main/Features/AdvancedCombat/Hooks/Agent_CheckToDropFlaggedItem_Guard_Patch.cs`, Patch69
  `Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs`, Patch56
  `Main/Features/HeroRace/Hooks/GauntletSceneNotification_OpenScene_Guard_Patch.cs`: finalizers kept;
  non-NRE exceptions now go through `RethrowStackPreserver`; Patch69 logic moved to `Settle`; logging
  changes.
- Tests: `TAOM.Tests/Features/Encyclopedia/HeroHelperLastSeenTextGuardTests.cs`,
  `TAOM.Tests/Infrastructure/ContainmentFinalizerRethrowTests.cs`,
  `TAOM.Tests/Features/ArmyTargeting/Patch49GatheringNreRuleTests.cs`,
  `TAOM.Tests/Features/CharacterCreation/NarrativeMountSpawnGuardTests.cs`, two rows in
  `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs`.
- Docs: those patches' sections in `docs/reference/harmony-patch-registry.md`,
  `docs/reviews/rca-lastseen-guard-2026-10-26.md`, the last entry of `docs/reviews/lessons/harmony-il.md`,
  `/verify` Step 2d in `.claude/skills/verify/SKILL.md`, the `api-snapshot` row in `.ai/verification.md`.

Out of scope: another session's uncommitted edits in `Main/IoC.cs`, `Main/SubModule.cs`,
`Main/Features/AutoResolveDiagnostics/**`, `Main/Features/UnlookedForAid/**`,
`Main/Features/TaomSettings.cs`, CoopInterop, ServerSettings and RacePersistence files.

## Evidence

Full suite 27,526 passed, 0 failed, 7 skipped; binding gate 798 passed, 0 skipped; mutation runs on
Patch123's decision each failed a test. Not run in game.

## Engine sources

`pwsh tools/taom-src.ps1 path <Type>` (installed v1.5.5, cache `C:\Users\mikew\.taom-src\v1.5.5`);
browsable dump `E:\Decompiled_Bannerlord\_categories_v1.5.5`.

## Output

Findings by severity with file:line, impact, proof; then claims checked and correct; then UNVERIFIED.
