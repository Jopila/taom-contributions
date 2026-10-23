# Codex pre-review: ServerJit baseline cap must equal the count

Review only; do not edit files. Working tree `E:\repos\taom-s3b`, uncommitted diff against `HEAD` (`git diff`).

`TAOM.Tests/Infrastructure/ServerJitGateTests.cs` guards `ServerJitBaseline.txt`, a list of core methods that may
only shrink. B6a (`6d7e67f3f`) pruned it from 385 to 270 lines but left `MaxBaselineEntries = 385`, and the test
asserted `count <= MaxBaselineEntries`, so 115 pruned lines could come back unnoticed. The diff asserts
`AreEqual(MaxBaselineEntries, count)` and sets the constant to 270. `docs/research/taom-headless-server.md`
restores B7 slice 1 facts that `6d7e67f3f` overwrote.

Questions, with evidence:
1. Can the gate still pass while a pruned or new line is added to the baseline? Consider the writer test
   (`Baseline_Regenerate_WhenAsked`), the sorted/unique/no-`<` checks and `ServerReachableMethods_ReferenceNoClientAssembly`.
2. Does equality break any legitimate workflow (the writer pruning, a rebase where another branch also prunes)?
   Is the failure message enough to fix it?
3. Is every fact in the research-doc diff true at `HEAD`? Check file:line claims, the 270 count, `Main/ClientOnlyAttribute.cs`,
   `FieldCampCampaignBehavior.cs:147` calling `PartyNameplateCampIconPatch.Reset` with no server check while that class
   carries `[ClientOnly]`, and that `CampNameplateIconPresenter`'s static initializer names a Gauntlet type.

Output: findings as CRITICAL, HIGH, MEDIUM, LOW with file:line and proof; UNVERIFIED where unproven; PASS if none.
