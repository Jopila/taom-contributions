You are the REVIEWER for TAOM (see AGENTS.md "Start here", .ai/policy.md and .ai/roles/reviewer.md). Review only. Do NOT edit, create or delete any file in this checkout. You may build and run tests (they write only bin/ and obj/).

FEATURE: ADR-013 batch B6, sub-batch 6c, "SubModule.cs headless guards". A Bannerlord dedicated server cannot load the client-only assemblies (SandBox.View, SandBox.GauntletUI, TaleWorlds.MountAndBlade.GauntletUI, the view-model collections, TaleWorlds.MountAndBlade.CustomBattle and others listed in TAOM.Tests/Infrastructure/ClientAssemblies.cs). The CLR compiles a method body at its first call, so a hook whose IL names a client-only type fails on a server. 6c puts every client-only startup call behind `if (!ClientComposition.IsDedicatedServer())` and moves the call into a `[MethodImpl(MethodImplOptions.NoInlining)]` helper that a server never calls.

CHECKOUT: this worktree (E:\repos\taom-b6c), branch feat/b6c-headless-guards, base 40385aa5b, the change is UNCOMMITTED (`git diff` plus untracked files). Sub-batches 6a (the [ClientOnly] class marker, PatchCategoryIndex.Build(dedicatedServer, ...), the Refuge guard, a SubModule IsDedicatedServer() helper) and 6b (client-only feature modules parked on a server) are NOT in this tree; another session lands them separately. Review 6c as if 6a and 6b are present, as the build plan describes them. Do not report "6a is missing" as a finding.

READ FIRST:
1. AGENTS.md, .ai/policy.md, .ai/roles/reviewer.md, .ai/review-reference.md.
2. docs/adrs/013-server-safe-core-client-ui-submodule.md (the JIT rule).
3. The build plan (outside this checkout, read it by absolute path): E:\Temp\claude\e--repos-TAOM\ec2ae0bd-3056-47de-9090-304830ca23ab\scratchpad\b6-build-plan.md. Sections 1.5 (the boot-path table), 1.7, 3 "6c", 4.3, 5 "Main/SubModule.cs, 6c" (G0 to G10), 7 (scope notes) and 9 (UNVERIFIED).
4. docs/reviews/rca-taom-client-b5-2026-10-22.md and docs/reviews/rca-battle-action-bar-2026-10-23.md (code a server runs for the first time is new code; every gate needs a mutation that makes it fail).

ONE DELIBERATE DEVIATION FROM THE PLAN: the plan's guards call a SubModule helper IsDedicatedServer() that reads IoC.Resolve<IDedicatedServerProvider>().IsDedicatedServer. 6a owns that helper, so 6c reads the existing seam ClientComposition.IsDedicatedServer() (Main/Composition/ClientComposition.cs, a static Func<bool> defaulting to GameNetwork.IsDedicatedServer). ManualPatchApplicator reads the same seam. The plan's test C2 is dropped.

FILES (all paths relative to this checkout):
Production: Main/SubModule.cs (6c hunks only), Main/ManualPatchApplicator.cs, Main/Features/CoopInterop/ServerAssemblyReport.cs (new).
Tests: TAOM.Tests/Composition/SubModuleServerGuardTests.cs (new; C1 and C3), TAOM.Tests/Composition/ManualPatchApplicatorServerGuardTests.cs (new; C4), TAOM.Tests/Features/CoopInterop/ServerAssemblyReportTests.cs (new; C5), TAOM.Tests/Infrastructure/IlClientReferences.cs (new; extracted IL token walk), TAOM.Tests/Infrastructure/Dependencies/DependenciesClientIlTests.cs (now uses the extracted walk), TAOM.Tests/Migration/ReflectionSiteBindingTests.cs (label repoint).
Docs: docs/reference/taleworlds-api-snapshot/reflection-sites.md (label repoint).

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with evidence):
S1. Patch95_FactionUI is one of the 13 mixed categories (core and client classes). G3 skips the whole InitializeFactionUi helper on a server, so no Patch95 class applies there, core ones included. List every Patch95 class, its target and what it changes. Does any core class change campaign state or behaviour that a co-op server owns and clients then see?
S2. Patch80_KingdomVoteDeadlock. The trap index says a "frozen" game can be a stuck kingdom vote waiting on an IsKingsDecisionOver edge, fixed by Patch80. G8 skips the whole category on a server. Are all its classes view-model only (KingdomDecisionsVM, DecisionItemBaseVM)? If any seam fixes a campaign-side wait, a co-op dedicated server can still freeze. Read docs/features/diplomacy.md and the Patch80 classes.
S3. Statics left null on a server. Every Initialize call now inside a helper (G3 FactionUIPatchContext and FactionUIWidgetContext, G4 the Patch79 probes, G5 CustomBattlesIoC.InitializeHooks, G6 BannerEditorView_OnTick_Patch, G8 the six Patch80 classes, G9 Patch60, the captain tooltip) leaves statics unset headless. Find every class in a category a server still applies, and every campaign behaviour, console command or service, that reads one of those statics, and say whether it null-guards. The plan names two: BannerlordMissions_CustomBattle_Patch and BannerlordMissions_Siege_Patch (Patch19, applied at OnSubModuleLoad). Which assembly holds their target type, and can a server ever run them?
S4. SubModule fields _timeAccelerationService (G1) and _factionUiTicker (G3) stay null on a server. Every read must be null-safe (OnApplicationTick and anywhere else, including reflection and dev-console reads). The hotkey categories (G2) are not registered on a server: does any server-reachable code call HotKeyManager.GetCategory for TaomTimeControlHotKeyCategory or TaomCareerHotKeyCategory and dereference the result?
S5. The JIT rule. After the change, no SubModule or ManualPatchApplicator.ApplyAll body may name a client-only assembly in IL: method and field tokens, type tokens, locals, catch types, generic instantiations (MethodSpec, TypeSpec), and compiler-generated closures and lambdas in nested display classes. Does C3 (SubModuleMethods_ReferenceNoClientAssemblyDirectly) and the IlClientReferences walk cover all of those? The builder found that `_ = typeof(SandBox.View.Map.MapScreen);` did not turn C3 red (it proved the gate with `System.GC.KeepAlive(typeof(SandBox.View.Map.MapScreen));` instead). Explain why, and say whether the gate is blind to any reference form that the JIT would still resolve.
S6. ClientComposition.IsDedicatedServer is a mutable static seam. Can a test or a production path leave it replaced, so a guard reads a stale value? Is it the same value as IDedicatedServerProvider.IsDedicatedServer (Main/Features/CoopInterop/DedicatedServerProvider.cs) at every guarded site, so 6a's helper and 6c's reads can merge into one?
S7. ServerAssemblyReport: the 16 names equal ClientAssemblies.Names; Log never throws; it runs only on a server; where it sits relative to the once-per-process block of OnGameInitializationFinished.
S8. Engine: GameNetwork.IsDedicatedServer on the client build is `public static bool IsDedicatedServer => false;` (C:\Users\mikew\.taom-src\v1.5.5\TaleWorlds.MountAndBlade.GameNetwork.cs:353). Find out whether a dedicated-server build of TaleWorlds.MountAndBlade.dll exists on this machine (Steam library under E:\Steam\steamapps\common) and what it returns there, and whether the value is valid as early as OnSubModuleLoad. If you cannot find the server DLL, mark this UNVERIFIED and name what settles it.

Also review, as new code on a server: the tail of OnGameInitializationFinished that a server now reaches for the first time (plan section 7), and the order of every moved block (each helper must run at the same point in the hook as the code it replaced, with identical try and catch scope).

REQUIRED SECTIONS:
1. SUSPECTS: S1 to S8, each CONFIRMED, DISPUTED or UNVERIFIED, with file:line and the code or command output that proves it.
2. FINDINGS: each with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, a concrete failure scenario (inputs and state, then the wrong result), proof, and the smallest fix.
3. TEST GATES: for each new test, the mutation that turns it red. Name any guard a mutation can remove or invert without any test failing.
4. OBSERVATIONS: anything else, marked as not a defect.

QUALITY GATES: every claim cites code you read or output you ran in this session. Engine signatures come from the installed DLLs (`pwsh tools/taom-src.ps1 path <Type>` or the decompile cache under C:\Users\mikew\.taom-src\v1.5.5). Missing engine, native or server evidence stays UNVERIFIED. Never run ./build.ps1. Builds and tests only with `-p:DisableModuleCopy=true -p:ModuleId=`. Do not flag code that matches vanilla behaviour as a bug. Do not skip the hard suspects (S2, S3, S5).

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-prereview-b6c-headless-guards-2026-10-23.md. Do NOT write that path or any other file yourself.
