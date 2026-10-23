Focused re-review of review FIXES for ADR-013 sub-batch 6a in E:\repos\TAOM, branch bannerlord-1.5.x, HEAD 0761c01b7, all uncommitted (`git status --short`, `git diff HEAD`; IGNORE TAOM.sln, the maintainer's own change). REVIEW ONLY: no edits, no state-changing git, no ./build.ps1; builds and tests only with `-p:DisableModuleCopy=true -p:ModuleId=`.

Your earlier review of 6a: docs/reviews/raw/codex-adversarial-b6a-clientonly-2026-10-23.md (after the last "tokens used"). Two Claude lenses then found a HIGH you disputed under S1: `Type.IsDefined` resolves the TYPE of every attribute on a class (it does not decode arguments), and two unmarked core classes carry a class-level [GameStateScreen] whose type lives in client-only TaleWorlds.MountAndBlade.View (GauntletCareerScreen, GauntletSupplyOrderScreen). The check ran outside the per-type try in PatchCategoryIndex.Build, so a server threw in OnSubModuleLoad. A real server log shows both types failing in the old try: E:\CoopDS\stage-spike\engine\bin\Win64_Shipping_Server\Logs\taom_debug_2026-10-23_09-23-19.log:88 and :720.

FIXES (check each closes its finding and adds nothing new):
A. Main/PatchCategoryIndex.cs: the marker check is inside the per-type try; a marked class still leaves before its Harmony attributes are read; an unmarked type whose attribute types cannot resolve becomes a SkippedClasses entry. Test: PatchCategoryIndexTests.Build_DedicatedServer_AnUnmarkedClassWithAnAttributeTypeThatCannotLoad_IsSkippedNotThrown with a GoneAttributeProbe helper (an attribute assembly deleted from disk).
B. New gate ServerLoadGateTests.CoreClassesWithAClientOnlyAttributeType_AreTheKnownList (exactly the two screens, a known list for B7).
C. TAOM.Tests/Infrastructure/PatchClientReasons.cs finds target selectors the way Harmony 2.4.2 does ([HarmonyTargetMethod] / [HarmonyTargetMethods] under any name, TargetMethods before TargetMethod, a Harmony parameter), with four fixtures.
D. PatchCategoryApplier.RecordClientOnlyClasses prints full names.
E. Stale line labels repointed in docs/features/mcm.md, docs/reference/harmony-patch-registry.md, docs/reference/taleworlds-api-snapshot/reflection-sites.md.

Also check: is there any OTHER place the server path reads attributes of every TAOM type outside a try (PatchCategoryApplier, HarmonyPatchInspector, CoopUiRegistrationPolicy, the census, anything run in OnSubModuleLoad)? Does the GoneAttributeProbe test really fail on the old code for the stated reason (not for a fixture artefact)?

REQUIRED: per fix A-E: CLOSED / NOT CLOSED / NEW DEFECT with the deciding file:line read this run, then a FINDINGS table (severity, file:line, defect, scenario, fix). Mark unproven claims UNVERIFIED; no style-only nits.

OUTPUT: the full report as your FINAL MESSAGE. The dispatcher redirects stdout to docs/reviews/raw/codex-adversarial-b6a-fixes-2026-10-23.md; do NOT write that file yourself.
