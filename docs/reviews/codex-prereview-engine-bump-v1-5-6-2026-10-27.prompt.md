# Codex pre-review: Bannerlord v1.5.6 engine bump and the Hidden Hosts field-parley veto

REVIEW ONLY. Do not edit, create, delete or format any file in this checkout. Do not run git write commands. Do not run ./build.ps1. Read, search, decompile and run read-only commands only. Return the full report as your FINAL MESSAGE. The dispatcher redirects your stdout into docs/reviews/raw/codex-prereview-engine-bump-v1-5-6-2026-10-27.md: do NOT write that path yourself.

## Feature

TAOM (a Lord of the Rings total conversion for Bannerlord, .NET Framework 4.7.2) moves from Bannerlord v1.5.5 to v1.5.6.125053 (issue #1036), and adds a Hidden Hosts veto on the new v1.5.6 field parley (issue #1037). The change is uncommitted in this worktree (branch chore/engine-bump-v1.5.6, base 59251a28d). Read `git diff` and the untracked files listed below.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- AGENTS.md and .ai/review-reference.md (rules, ADRs, severity and evidence format).
- gh issue view 1036 and gh issue view 1037 (the intent and the verified analysis).
- docs/migration/v1.5.6-impact.md and docs/migration/v1.5.6-diff-ranked.md (the bump record; check its claims, do not trust them).
- docs/features/hidden-hosts.md, section "Field parley (v1.5.6)".
- Engine code: the v1.5.6 decompile is in E:\Decompiled_Bannerlord\_shipping_build and _modules_build; the v1.5.5 decompile is in the same folders with the suffix _v1.5.5. Ignore every non-vanilla module file in _modules_build (Coop__*, TAOM__*, TAOM.Dependencies__*, DualWield*, Diplomacy, DOTS, AWOIAF): they are not the engine. The installed DLLs under "E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client" and Modules\SandBox\bin are the authority if a decompile disagrees.

## Known suspects (try hardest to break these)

1. The new postfix Main/Features/HiddenHosts/Hooks/DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch.cs on DefaultEncounterModel.CanMainHeroDoParleyWithParty(PartyBase, out TextObject) (v1.5.6 TaleWorlds.CampaignSystem.cs near :61099). Does HiddenHostsService.MasksParley give exactly the verdict the party nameplate and tooltip use, for every party the four callers pass (tooltip hint row in TooltipRefresherCollection.AddTooltipHintProperties, party and settlement nameplate CanParley in SandBox.GauntletUI, MapScreen.TryParleyWith)? Can it veto a party the player can actually see named, or let through a party the plate shows as "Unknown party"? What happens with Hidden Hosts off, a settlement, a non-mobile party, a party with no leader, the player's own army, a party at sea?
2. The same postfix on the hot path: the nameplate view calls the model per party plate per frame while Ctrl is held. Any allocation, LINQ, IoC.Resolve or snapshot build on the common path (Hidden Hosts off, vanilla already false, an unmasked party)? Any exception path that escapes the try/catch, or a fault budget that can trip on a normal frame?
3. The explanation swap: the postfix replaces vanilla's refusal text for a masked host and returns early only when vanilla refused with no text. Read every vanilla caller: does any caller read the explanation when __result is true, set variables on it, or compare it by reference? Does the replacement text leak anything?
4. Co-op and the dedicated server: TAOM supports BannerlordCoop and a headless dedicated server (docs/features/coop-interop.md "Co-op gating", docs/features/coop-dedicated-server.md). Is the new patch safe to apply on a server with no UI, and correct for a co-op client whose map state comes from the host? Does any other installed mod replace EncounterModel so the postfix never runs?
5. Patch123 retirement (Main/Features/Encyclopedia/Hooks/HeroHelper_GetLastSeenText_Patch.cs and LastSeenState.cs deleted; the SubModule.cs registration removed). Prove or refute that v1.5.6 Helpers.HeroHelper.GetLastSeenText (v1.5.6 TaleWorlds.CampaignSystem.cs near :1891) can no longer throw for any hero state: a prisoner of a leaderless party, a prisoner held in a settlement, a leaderless army, an army whose leader party has no hero, at sea, never seen, dead, LastKnownClosestSettlement null. Player crash 9b2a103e was the reason the guard existed (docs/reviews/rca-lastseen-guard-2026-10-26.md).
6. The two new GameText strings in Main/_Module/ModuleData/taom_module_strings.xml (str_last_seen_encyclopedia_entry_leading_army and str_last_seen_encyclopedia_entry_at_sea_leading_army). Do the variables match exactly what v1.5.6 sets on each TextObject? Is the {?IS_IN_SETTLEMENT}...{\?} syntax valid inside an XML attribute and in TaleWorlds' text processor? Does TAOM's file load as GameText so FindText finds the id? What happens if a later TaleWorlds patch defines the same ids?
7. The v1.5.6 API fixes: UsableMachine.GetDescriptionText(WeakGameEntity, Agent) in TaomHowdahMachine and TaomMumakilPlatform; MobileParty.HasPerk without the removed out-Hero overloads in TaomSiegeEventModel and ForestSightContributor; PerkHelper.AddPerkBonusForCharacter with BattleEnvironment moved to a trailing optional parameter in PrisonerEscapeAdapter. Is each call semantically identical to the v1.5.5 one (same environment, same secondary-role rule)? Are there other call sites the compiler accepts but whose meaning changed, in Main, Client, Dependencies or TAOM.Tests?
8. BattleEnvironment lost the None member in v1.5.6, so Land, Naval and Any moved from 1, 2, 3 to 0, 1, 2. Does TAOM persist a BattleEnvironment value anywhere (save data, a save definer, MCM settings, JSON config, a co-op message, a cache), so an old value now reads as a different member? Does any TAOM code compare it numerically?
9. The party nameplate prefab clones (Main/_Module/GUI/PreFabs/Nameplate/PartyNameplateItem.xml and PartyPlayerNameplateItem.xml) were re-based on the v1.5.6 ParleyIconWidget placement. Diff each against the installed vanilla file in Modules\SandBox\GUI\Prefabs\Nameplate: is every vanilla v1.5.6 widget, id and binding the widget code dereferences present in the clone?
10. Anything in the v1.5.6 engine change that TAOM depends on and that the record misses: read the file-level diffs between the two decompiles for TaleWorlds.CampaignSystem, TaleWorlds.MountAndBlade, SandBox and SandBox.View, and match changed members against TAOM's Harmony targets (docs/reference/taleworlds-api-snapshot/patch-targets.md) and GameModel overrides (gamemodel-bases.md). Note: the snapshot records only the first target of a TargetMethods() patch.

## Files

C#: Main/Adapters/PrisonerEscapeAdapter.cs, Main/Features/Elephant/TaomHowdahMachine.cs, Main/Features/Mumakil/TaomMumakilPlatform.cs, Main/Features/Siege/Models/TaomSiegeEventModel.cs, Main/Features/UnderTheEaves/ForestSightContributor.cs, Main/Features/HiddenHosts/HiddenHostsService.cs, Main/Features/HiddenHosts/HiddenHostsTexts.cs, Main/Features/HiddenHosts/Hooks/DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch.cs (new), Main/SubModule.cs, deleted Main/Features/Encyclopedia/Hooks/HeroHelper_GetLastSeenText_Patch.cs and Main/Features/Encyclopedia/LastSeenState.cs.
Tests: TAOM.Tests/Features/HiddenHosts/HiddenHostsParleyTests.cs (new), HiddenHostsWiringTests.cs, CoopInterop/CoopVetoClassificationTests.cs, UnderTheEaves/ForestSightContributorTests.cs, Migration/ReflectionSiteBindingTests.cs, MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs, deleted Encyclopedia/HeroHelperLastSeenTextGuardTests.cs.
XML: Main/_Module/GUI/PreFabs/Nameplate/PartyNameplateItem.xml, PartyPlayerNameplateItem.xml, Main/_Module/ModuleData/taom_module_strings.xml, Main/_Module/SubModule.xml, the 12 Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml.
Tools: tools/validate_moduledata.py, tools/audit_mbproj_registration.py, tools/check_handbook_attributes.py, tools/handbook_attribute_manifest.json, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py.
Docs and pins: .claude/pinned-game-version.txt, AGENTS.md, .ai/review-reference.md, .claude/skills/engine-bump/SKILL.md, README.md, docs/migration/v1.5.6-impact.md and v1.5.6-diff-ranked.md (new), docs/features/hidden-hosts.md, encyclopedia.md, far-watch.md, skeleton-buffer-guard.md, bannerlord-together-compat.md, docs/reference/harmony-patch-registry.md, feature-map.md, taleworlds-api-snapshot/*.md.

## REQUIRED SECTIONS

### VANILLA CODE
Quote the v1.5.6 bodies you rely on (CanMainHeroDoParleyWithParty, its four callers, GetLastSeenText, UsableMachine.GetDescriptionText, MobileParty.HasPerk, PerkHelper.AddPerkBonusForCharacter and AddPerkBonusForParty) as code blocks with their file and line, and the v1.5.5 body wherever you claim a difference.

### Deep analysis
For suspects 1 to 8, walk at least one concrete scenario each (state the party, the settings, the frame) to its result.

### CONFIG CROSS-REFERENCE
Check every id the change adds or references (string ids, localization keys, patch category names, widget ids) against its definition and its consumers.

### FINDINGS OR OBSERVATIONS
One entry per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, the defect, the concrete failure scenario, the proof (code you read), and the fix. Mark anything you could not prove UNVERIFIED. If you find nothing in a suspect, say what you checked.

## QUALITY GATES

- Cite file:line for every claim; a claim with no source is not a finding.
- Do not flag code that matches vanilla behaviour as a bug unless TAOM's intent says otherwise.
- Do not skip the hard sections (suspects 1, 3, 5 and 8).
- Engine signatures come from the installed DLLs or the v1.5.6 decompile, never from memory.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.
