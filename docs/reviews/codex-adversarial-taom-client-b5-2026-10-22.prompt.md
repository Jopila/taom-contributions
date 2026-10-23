Adversarial review: the UNCOMMITTED batch B5 of ADR-013 in E:\repos\TAOM, branch bannerlord-1.5.x, HEAD b5d6a026f. REVIEW ONLY: do not edit any file, do not run git commands that change state, do not run ./build.ps1 or any deploying build. Builds and tests, if any, only with `-p:DisableModuleCopy=true -p:ModuleId=`. Other reviews read the tree at the same time.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/adrs/013-server-safe-core-client-ui-submodule.md
The plan with decompile citations: E:\Temp\claude\e--repos-TAOM\ec2ae0bd-3056-47de-9090-304830ca23ab\scratchpad\b5-build-plan.md (its UIExtenderEx 2.13.3 decompile is under that scratchpad's uiext-2.13.3 folder; taom-src resolves a different UIExtenderEx copy, do not use that one).
The change: `git status --short`, `git diff HEAD -M --stat`, `git diff HEAD -M`, and the untracked files.
Prior batches: commits 10f1f0b80, a15563a3c, c91380274 and their RCAs docs/reviews/rca-taom-client-b4*-2026-10-22.md.
Engine v1.5.5: `pwsh tools/taom-src.ps1 path <Type>`; decompile dump E:\Decompiled_Bannerlord\_categories_v1.5.5\.

DESIGN IN ONE PARAGRAPH:
B5 moves the 11 ViewModelMixins, 2 widgets, 15 prefab extension classes (11 files), the three Patch109_SPInventoryVM postfixes and CoopUiRegistrationPolicy from TAOM.dll into TAOM.Client.dll, so TAOM.dll has no type that needs a client assembly (ServerLoadGateTests allowlist empty, milestone M1 in source). UIExtenderEx registration moves from Main/SubModule.cs RegisterUiExtensions (deleted) to TAOM.Client: ClientSubModule.OnSubModuleLoad calls UIExtender.Create("TAOM"), ClientUiExtensions.Register(extender, client assembly) (the old body, co-op suppression included), then Enable. The name stays "TAOM" because HarmonyCensusReportBuilder and PatchShieldPolicy match the Harmony id. PatchCategoryIndex.Build now takes the assemblies from ClientComposition.LoadedTaomAssemblies() (core, plus the client when ClientComposition.Current is set by the client constructor), so Patch109 in TAOM.Client joins its category. TAOM.dll gains InternalsVisibleTo("TAOM.Client"). ClientIsolationGateTests' tripwire now forbids only save definers and MCM settings in TAOM.Client; new gates say core declares no UI extension and the client declares all 26.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code):
S1. Load order: the client's OnSubModuleLoad must run after core's (core logs, IoC and CoopPresence ready) and before any screen or VM the mixins target is built. Check Module.LoadSubModules / InitializeSubModuleBases ordering in the installed engine, the submodule order in Main/_Module/SubModule.xml, and what the client's registration reads from core (IoC.Resolve, CoopPresence) at that moment.
S2. UIExtenderEx 2.13.3: Register/Enable from a second assembly under the name "TAOM"; anything in UIExtenderEx that keys on the calling assembly, the module id, or the order relative to other mods' Enable; what happens if the client throws inside Register (the old code never threw; the new path's try/catch).
S3. PatchCategoryIndex with two assemblies: duplicate class names, a category spread over both assemblies, SkippedClasses reporting, Harmony ids; and that LoadedTaomAssemblies() returns the client at the time core's OnSubModuleLoad runs Build.
S4. InternalsVisibleTo: an unsigned friend assembly; does it change anything the server sees, or let a server-side core path call into a client type?
S5. Co-op suppression ([CoopSuppressedUi]) still filters the same set, read from the client assembly; the info log line still prints exactly once.
S6. The widened and relaxed gates: each fails for its stated reason (mutate mentally), the tripwire still forbids save definers and MCM settings, and no scanner still reads one assembly.

REQUIRED SECTIONS: 1. Engine, UIExtenderEx and TAOM code you rely on, as code blocks with paths and lines. 2. Per suspect: CONFIRMED / DISPUTED / UNVERIFIED with a concrete scenario and the deciding line. 3. Other findings. 4. FINDINGS table: severity, file:line, defect, scenario, fix. Every claim cites a file and line you read in this run; mark unproven claims UNVERIFIED; no style-only nits.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to docs/reviews/raw/codex-adversarial-taom-client-b5-2026-10-22.md; do NOT write that file yourself.
