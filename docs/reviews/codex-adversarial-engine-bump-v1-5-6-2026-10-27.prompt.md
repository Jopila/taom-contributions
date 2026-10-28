# Codex adversarial review: v1.5.6 engine bump fixes and the turbo click patch

REVIEW ONLY. Do not edit, create, delete or format any file in this checkout. Do not run git write commands. Do not run ./build.ps1. Read, search, decompile and run read-only commands only (a filtered `dotnet test ... --no-build -p:DisableModuleCopy=true -p:ModuleId=` is allowed). Return the full report as your FINAL MESSAGE. The dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-engine-bump-v1-5-6-2026-10-27.md: do NOT write that path yourself.

## Feature

TAOM (a Lord of the Rings total conversion for Bannerlord, .NET Framework 4.7.2) moves from Bannerlord v1.5.5 to v1.5.6.125053 (issue #1036), adds a Hidden Hosts veto on the new field parley (issue #1037), and fixes a v1.5.6 conflict between the parley modifier and TAOM's turbo time control (issue #1040). An `ultra` pre-review of the bump already ran (docs/reviews/raw/codex-prereview-engine-bump-v1-5-6-2026-10-27.md, final report near the end); four Claude review lenses then ran, and their confirmed findings were fixed. This pass reviews what changed since, and takes a second adversarial look at the whole bump. The change is uncommitted in this worktree (branch chore/engine-bump-v1.5.6, base 59251a28d): read `git diff` and the untracked files.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- AGENTS.md and .ai/review-reference.md (rules, ADRs, severity and evidence format).
- docs/reviews/rca-engine-bump-v1-5-6-2026-10-27.md (what the first review found and how it was fixed).
- docs/features/time-acceleration.md section "Turbo clicks"; docs/features/hidden-hosts.md section "Field parley (v1.5.6)"; docs/reference/harmony-patch-registry.md sections Patch124_TurboClick and Patch105_HiddenHosts.
- docs/migration/v1.5.6-impact.md (the bump record; check its claims).
- Engine code: v1.5.6 decompiles in E:\Decompiled_Bannerlord\_shipping_build and _modules_build; v1.5.5 in the same folders with the suffix _v1.5.5. Ignore non-vanilla module files there (Coop__*, TAOM__*, TAOM.Dependencies__*). The installed DLLs are the authority; for SandBox.View decompile "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\bin\Win64_Shipping_Client\SandBox.View.dll" directly (never a copy under Modules\Coop).

## Known suspects (CONFIRM or DISPUTE each, with evidence)

1. Patch124 inlining. Main/Features/TimeAcceleration/Hooks/MapScreen_IsParleyClickReleased_TurboClick_Patch.cs postfixes the private, parameterless, 61-byte SandBox.View MapScreen.IsParleyClickReleased, whose only caller is MapScreen.HandleMouse. The design relies on Harmony 2.4.2 (the 0Harmony.dll in Modules\TAOM.Dependencies) marking a patched method not inlinable and on the patch applying at GameInit (Main/Features/TimeAcceleration/TimeAccelerationModule.cs, ApplyPhase.GameInit) before the first campaign map frame JIT-compiles HandleMouse. Can HandleMouse be compiled before the category applies (a save loaded from the main menu, a second campaign in the same process, another mod or TAOM code touching MapScreen earlier, tiered or eager JIT)? If it was inlined, does the postfix silently never run? Read how ModuleRunner applies GameInit categories (Main/Composition).
2. Patch124 frame order. The turbo latch `_turboActive` changes in TimeAccelerationService.OnTick (where is it ticked from, and is that before or after MapScreen.HandleMouse in the same frame?). Walk a click released on the same frame Space or Ctrl is released, and a click released on the frame turbo opens. Is any turbo click still a parley, or any ordinary Ctrl click wrongly a move?
3. Patch124 scope. With the postfix answering false, does HandleMouse's else branch (IsMapClickReleased, HandleLeftMouseButtonClick, OnMapClick) give the v1.5.5 result for a party, a settlement, open ground, a double click and an Alt (MapFollowModifier) click? On a gamepad IsParleyClickReleased reads MapParleyClick (ControllerRUp): can TAOM's turbo be active while a controller is used, and does the postfix then eat a controller parley?
4. Patch124 on the dedicated server and in co-op. The class is [ClientOnly] in the Main assembly but names typeof(MapScreen) from SandBox.View in its attribute. Does the server's patch indexing (ServerLoadGateTests, the ClientOnly filter, ModuleRunner) avoid loading SandBox.View types? Can turbo latch on a co-op client that defers time control to the host?
5. The Hidden Hosts veto after the fixes. HiddenHostsService.MasksParley is now `s.IsVisible && IsNameHidden(in s)`, and a masked host gets `explanation = null` (Main/Features/HiddenHosts/Hooks/DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch.cs). Is PartyIntelSnapshot.IsVisible the same visibility the party nameplate (_isVisibleOnMapBind) and the tooltip title use? Does any of the four vanilla callers (TooltipRefresherCollection.AddTooltipHintProperties, the party and settlement nameplate CanParley in SandBox.GauntletUI, MapScreen.TryParleyWith) or the NavalDLC wrapper behave badly with a null explanation on a false result?
6. The new tests. TAOM.Tests/Features/TimeAcceleration/TurboClickPatchTests.cs, TurboClickBindingTests.cs, TimeAccelerationServiceTests.cs (IsTurboActive tests) and TAOM.Tests/Features/HiddenHosts/HiddenHostsParleyTests.cs: can any of them pass against a broken implementation (a constant answer, the wrong visibility field, the postfix never applied)?

## Files

New C#: Main/Features/TimeAcceleration/Hooks/MapScreen_IsParleyClickReleased_TurboClick_Patch.cs, Main/Features/TimeAcceleration/TimeAccelerationModule.cs, Main/Features/HiddenHosts/Hooks/DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch.cs.
Changed C#: Main/Features/TimeAcceleration/ITimeAccelerationService.cs, TimeAccelerationService.cs, Main/Composition/FeatureModules.cs, Main/Features/HiddenHosts/HiddenHostsService.cs, HiddenHostsTexts.cs, and the bump's API fixes (Main/Adapters/PrisonerEscapeAdapter.cs, Main/Features/Elephant/TaomHowdahMachine.cs, Main/Features/Mumakil/TaomMumakilPlatform.cs, Main/Features/Siege/Models/TaomSiegeEventModel.cs, Main/Features/UnderTheEaves/ForestSightContributor.cs, Main/SubModule.cs).
Tests: TAOM.Tests/Features/TimeAcceleration/*, TAOM.Tests/Features/HiddenHosts/HiddenHostsParleyTests.cs, HiddenHostsWiringTests.cs, the other changed test files in git diff.
XML: Main/_Module/ModuleData/taom_module_strings.xml, the 12 Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml, Main/_Module/GUI/PreFabs/Nameplate/PartyNameplateItem.xml and PartyPlayerNameplateItem.xml, Main/_Module/SubModule.xml.
Docs: docs/features/time-acceleration.md, hidden-hosts.md, docs/reference/harmony-patch-registry.md, feature-map.md, docs/migration/v1.5.6-impact.md, docs/reviews/rca-engine-bump-v1-5-6-2026-10-27.md, .claude/skills/engine-bump/SKILL.md.

## REQUIRED SECTIONS

### VANILLA CODE
Quote, with file and line, the v1.5.6 bodies of MapScreen.HandleMouse (the click branch), IsParleyClickReleased, IsMapClickReleased, HandleLeftMouseButtonClick, HandleParleyClick and TryParleyWith, the map frame entry that calls HandleMouse, and the v1.5.5 HandleMouse click branch for comparison.

### Deep analysis
For suspects 1 to 5, walk at least one concrete frame-by-frame scenario each (keys held, the frame the click is released, what the player sees).

### CONFIG CROSS-REFERENCE
Check the patch category name (Patch124_TurboClick) against PatchCategoryNumberTests and the registry, the new module against FeatureModules and its tests, and every localization key the change removed against its remaining references.

### FINDINGS OR OBSERVATIONS
One entry per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, the defect, the concrete failure scenario, the proof (code you read), and the fix. Mark anything you could not prove UNVERIFIED. Say what you checked for each suspect you could not break.

## QUALITY GATES

- Cite file:line for every claim; a claim with no source is not a finding.
- Do not flag code that matches vanilla behaviour as a bug unless TAOM's intent says otherwise.
- Do not skip the hard sections (suspects 1, 2 and 4).
- Engine signatures come from the installed DLLs or the v1.5.6 decompile, never from memory.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.
