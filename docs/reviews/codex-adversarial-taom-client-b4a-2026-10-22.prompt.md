Adversarial review: the UNCOMMITTED batch B4a of ADR-013 in E:\repos\TAOM, branch bannerlord-1.5.x, HEAD e0a6970dd. REVIEW ONLY: do not edit any file, do not run git commands that change state, do not run ./build.ps1 or any deploying build. Builds and tests, if you run any, use `-p:DisableModuleCopy=true -p:ModuleId=` only. Another review is reading this tree at the same time; that is fine.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/adrs/013-server-safe-core-client-ui-submodule.md
docs/research/taom-headless-server.md (sections "The client hand-off", "The gates", "Batches")
The change: `git status --short`, `git diff HEAD -M --stat`, `git diff HEAD -M`, and the untracked files.
The build plan with its decompile citations: E:\Temp\claude\e--repos-TAOM\ec2ae0bd-3056-47de-9090-304830ca23ab\scratchpad\b4a-build-plan.md
Engine v1.5.5 signatures: the installed DLLs via `pwsh tools/taom-src.ps1 path <Type>`; decompile dump E:\Decompiled_Bannerlord\_categories_v1.5.5\.

DESIGN IN ONE PARAGRAPH:
TAOM.dll must load on the co-op dedicated server, where UI assemblies are absent. B3 added the client-only submodule TAOM.Client (DedicatedServerType=none, IsNoRenderModeElement=false). B4a moves 15 front-end files into Client/ with git mv and unchanged namespaces: seven FactionUI widgets, BannerWidget, FactionImageWidget, MapContainerWidget, PolygonWidget, RuntimeSprite, RuntimeSpriteFactory, FrontEndRuntimeSprite, TaomFactionSelectionVM. Core callers are inverted through three new core interfaces, each with a null implementation registered in core and a real one registered by TaomClientComposition through ClientComposition.Apply (a DryIoc view sharing the registry with IfAlreadyRegistered.Replace): IFactionMapWidgetAdapter (FactionSelectionVM, CultureStageViewCreatedHook), IFrontEndSpriteTableAdapter (FrontEndResourceAdapter.RegisterSprite), ICustomBattleFactionSelectionFactory (CustomBattleSideVM_Constructor_Patch via CustomBattlesIoC.InitializeHooks and Main/SubModule.cs). Because DryIoc keeps instances resolved before a Replace, FactionMapIoC no longer resolves its culture-stage hooks while registering; FactionMapIoC.InitializePatchStatics does it after Apply (Main/IoC.cs). FactionUIWidgetContext.ThemeIcons became public. Gates widened to read Client/; ServerLoadGateTests' allowlist drops from 40 to 26.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code):
S1. A singleton in the graph of any of the three seams is still resolved before ClientComposition.Apply (inside a Register*Feature method, a static initializer, or an earlier eager Resolve), so it keeps the null adapter in single player: blank sprites, no faction-map hover, or no TAOM factions in Custom Battle.
S2. Gauntlet does not find a widget type in TAOM.Client at the time a prefab first instantiates it (widget discovery order versus submodule DLL load; a prefab loaded before TAOM.Client's types are scanned), giving "builtin widget type not found".
S3. A core method body still names a moved type or a client-only assembly, so it compiles only because TAOM.Client is referenced by tests, or it JIT-fails on the server (the server never loads TAOM.Client).
S4. Moving the culture-stage hook resolve later changes single-player behaviour: something between registration and InitializePatchStatics needed the hook, or a patch static is now null during an early call.
S5. The null implementations change single-player behaviour if TAOM.Client fails to load (for example a silent no-op where an exception used to surface), or hide a client load failure.
S6. The widened gates: can each fail for its stated reason after the move (a gate that now reads nothing, or reads Client/ but asserts the old Main/ path)?

REQUIRED SECTIONS:
1. ENGINE AND TAOM CODE you rely on, as code blocks with paths and lines.
2. Per suspect S1-S6: CONFIRMED / DISPUTED / UNVERIFIED with a concrete scenario and the deciding line.
3. Other findings.
4. FINDINGS table: severity, file:line, defect, scenario, fix.

QUALITY GATES: every claim cites a file and line you read in this run; mark unproven claims UNVERIFIED; no style-only nits.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to docs/reviews/raw/codex-adversarial-taom-client-b4a-2026-10-22.md; do NOT write that file yourself.
