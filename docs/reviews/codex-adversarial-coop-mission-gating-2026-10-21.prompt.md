Adversarial review: TAOM co-op batch #967 C1 (per-player special-resource earning) and C7 (co-op gating of mission behaviors), on Bannerlord v1.5.5 with BannerlordCoop (bannerlord-1.5.x). This is a REVIEW ONLY task: do not edit any file, do not run git commands that change state, do not run ./build.ps1.

SCOPE: commit 8ae036bc5 (its subject is wrong: it holds C1 and C7; review its content with `git show 8ae036bc5`) PLUS the uncommitted working-tree follow-up (`git diff`, untracked files), which applies the deep-review findings. Review the combined current state.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/features/coop-interop.md (the C7 section "Mission behaviors in a co-op session" and the CoopPlayerControlQuery rules)
docs/research/bannerlordcoop-internals.md (C7 section)
docs/research/coop-compat-gap-ledger.md
docs/features/special-resources.md (co-op section)
Co-op source: E:\repos\BannerlordCoop\source (read-only). Engine v1.5.5 decompile: C:\Users\mikew\.taom-src\v1.5.5\ (use `pwsh tools/taom-src.ps1 path <Type>` for a path).

DESIGN IN ONE PARAGRAPH:
Co-op runs one authority process (the server, no human plays on it) and clients that each run their own mission and own their agents; other peers' agents are puppets with Controller None. C7: a role table (Main/Features/CoopInterop/Missions/CoopMissionRoles.cs) classes every TAOM mission behavior as Local, AgentOwner or Unsafe; Unsafe behaviors are not added in a session (Main/SubModule.cs AddTaomBehavior, ~line 2147). AgentOwner behaviors either call CoopMissionOwnership.IsOwnedHere(agent) (registry probe via co-op's INetworkAgentRegistry, compiled delegates, falls back to controller facts) or rely on a named invariant (DreadAgentGate.CanAffect requires IsAIControlled + CommonAIComponent; puppets are kept off PlayerTeam; banner promotion requires IsAIControlled; puppet troop origins have no supplier). C1: special-resource earning on the authority only, for a party/hero the co-op registry marks as a player's (SpecialResourceEarnPolicy, SpecialResourceEarningService, SpecialResourceEventAdapter, SpecialResourcesBehavior).

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code):
S1. The follow-up deleted four morale ownership checks (DreadPulseRunner, RaceAbilityDeaths, SignatureStrikeRunner victim, MissionAbilityExecutionContext burst) relying on DreadAgentGate / CommonAIComponent. Is there a window where a puppet (human OR mount) is AI-controlled with a CommonAIComponent on a non-owner peer (build time, authority handoff, rider-less mount), so a local morale drain could rout it?
S2. The creature-bandit rout backstop is now ungated. Can calling Origin.SetRouted for a non-owned agent on a non-owner peer change the battle outcome or troop counts anywhere (co-op hideout or siege origins, TAOM creature spawn via Mission.SpawnMonster)?
S3. CoopMissionAuthority per-mission link cache and the warn latch reset in BuildLink: any race between an off-thread OnMeleeHit first call and the main thread; any case where a failure now logs on every call.
S4. C1: SpecialResourceEarnPolicy.MayEarn(sessionActive, isAuthority, isDedicated, control) => isAuthority && control == Player && (sessionActive || !isDedicated). On a dedicated server with no session yet, or during a guest's temporary MainHero substitution (BarterPlayerContext, MainHeroSubstitutionScope), can a resource be credited to the wrong hero or to nobody? Trace SpecialResourceEventAdapter callers.
S5. The Unsafe filter: six behaviors are dropped in a session (list in CoopMissionRoles). Does any other TAOM code (campaign side, payout, dialogs) assume one of them ran in the mission and misbehave when it did not (e.g. enlistment merit payout, siege dismount, field commission)?
S6. The co-op binding: CoopMissionBindingTests loads installed Coop DLLs. Do CoopMissionAuthority and CoopSessionProvider bind members that the installed Coop v0.1.6 actually exposes with the expected generic arity and parameter types?

FILES:
C7 code: Main/Features/CoopInterop/Missions/*.cs, Main/Features/CoopInterop/{CoopInteropIoC,CoopSessionProvider}.cs, Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs, Main/Features/Warg/BehaviorTreeElements/OnWargDied.cs, Main/Features/AdvancedCombat/BoneCheck.cs, Main/Features/SignatureStrikes/Hooks/{SignatureStrikeRunner,SignatureStrikesMissionLogic}.cs, Main/Features/Elephant/HowdahCrewSpawner.cs, Main/Features/Mumakil/MumakilCrewSpawner.cs, Main/Features/BannerBearers/Hooks/{BannerBearerAssignmentMissionLogic,FormationBannerHeraldry}.cs, Main/Features/DreadAura/Hooks/{DreadPulseRunner,DreadAgentGate}.cs, Main/Features/CareerSystem/Abilities/MissionAbilityExecutionContext.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityDeaths.cs, Main/Features/CreatureBandits/CreatureBanditMissionBehavior.cs, Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs, Main/SubModule.cs (AddTaomMissionBehaviors).
C1 code: Main/Features/SpecialResources/{SpecialResourceEarnPolicy,SpecialResourceEarningService,SpecialResourcesBehavior,RecruitChargePolicy}.cs, Main/Adapters/SpecialResourceEventAdapter.cs, Main/Features/CoopInterop/{CoopPlayerControlQuery,PlayerControlPolicy}.cs, Main/SubModule.cs SpecialResourcesBehavior construction (~line 1270).
Tests: TAOM.Tests/Features/CoopInterop/**, TAOM.Tests/Features/SpecialResources/**.

REQUIRED SECTIONS:
1. VANILLA AND CO-OP CODE: paste the engine and co-op code each finding depends on, as code blocks with paths and lines (Agent.IsAIControlled, AgentCommonAILogic.OnAgentControllerChanged, PuppetSpawner puppet build, CoopAgentOrigin.SetRouted, NetworkAgentRegistry.IsLocallyControlled, Mission.OnAgentRemoved).
2. Per suspect S1-S6: CONFIRMED / DISPUTED / UNVERIFIED with a concrete scenario (peer, agent, sequence of calls) and the line that decides it.
3. Anything else you find: data-flow gaps, lifecycle (second campaign in one process, load with same ids), thread safety, NaN or cast gates on engine floats in changed code.
4. FINDINGS table: severity (CRITICAL/HIGH/MEDIUM/LOW), file:line, defect, scenario, fix.

QUALITY GATES: every claim cites a file and line you read in this run; mark anything you could not prove UNVERIFIED; do not report style-only nits; do not flag code that matches vanilla or co-op behaviour on purpose unless it causes a defect.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to docs/reviews/raw/codex-adversarial-coop-mission-gating-2026-10-21.md; do NOT write that file yourself.
