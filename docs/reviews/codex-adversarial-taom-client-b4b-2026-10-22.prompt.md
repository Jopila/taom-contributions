Adversarial review: the UNCOMMITTED batch B4b of ADR-013 in E:\repos\TAOM, branch bannerlord-1.5.x, HEAD fd2072a0a. REVIEW ONLY: do not edit any file, do not run git commands that change state, do not run ./build.ps1 or any deploying build. Builds and tests, if you run any, use `-p:DisableModuleCopy=true -p:ModuleId=` only. Other reviews read this tree at the same time.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/adrs/013-server-safe-core-client-ui-submodule.md
The build plan with its decompile citations: E:\Temp\claude\e--repos-TAOM\ec2ae0bd-3056-47de-9090-304830ca23ab\scratchpad\b4b-build-plan.md
The change: `git status --short`, `git diff HEAD -M --stat`, `git diff HEAD -M`, and the untracked files.
The previous batch for comparison: commit 10f1f0b80 and docs/reviews/rca-taom-client-b4a-2026-10-22.md.
Engine v1.5.5 signatures: the installed DLLs via `pwsh tools/taom-src.ps1 path <Type>`; decompile dump E:\Decompiled_Bannerlord\_categories_v1.5.5\.

DESIGN IN ONE PARAGRAPH:
TAOM.dll must load on the co-op dedicated server, where UI and view assemblies are absent. B4b moves six campaign-map types (MomentumIndicatorMapView, RealmBordersMapView, FieldCampMapView, TaomFiefsNavigationElement, TaomMapConversationDataProvider, TaomSettlementPlateWidget) and three helpers (MomentumUIService, RealmBordersKeys, SettlementPlatePresenter) into TAOM.Client.dll. Core callers are inverted through five seams with core null versions that TaomClientComposition replaces through ClientComposition.Apply (DryIoc Replace on a shared registry; instances resolved before Apply keep the null): IMomentumUIService, IRealmBordersMapViewAdapter, IFieldCampOverlayAdapter (handed to FieldCampCampaignBehavior through an internal static set in FieldCampIoC.InitializePatchStatics, because SubModule builds that behavior with new), IFiefsNavigationElementFactory (Patch36 postfix), IMapConversationProviderWrapper (HallOfFireInstaller). The settlement plate reads its settings from a core NameplateRelationWidgetContext. MapProfilerTargets now scans ClientComposition.Assembly as well as core for MapView subclasses (Patch101 map frame profiler).

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code):
S1. A B4b seam is resolved or captured before ClientComposition.Apply (Register*Feature body, static initializer, OnGameStart of a behavior built before Apply, a patch Prepare or TargetMethods), so single player keeps a null: no momentum indicator, no realm borders, no field camp overlay, no Fiefs nav button, no Hall of Fire conversation wrapper.
S2. The FieldCampCampaignBehavior static: a second campaign in one process, a save load, or a behavior instance built before FieldCampIoC.InitializePatchStatics runs sees a stale or null overlay; OnTick now internal changes when or whether the engine calls it.
S3. MapProfilerTargets: Harmony TargetMethods runs at patch time; is ClientComposition.Current (and so ClientComposition.Assembly) set by then in every order (client constructor before core OnSubModuleLoad)? On a server, does the scan of a null assembly behave?
S4. Patch36 postfix: the factory is looked up on every call; what does it do when the factory is the null and the engine expects an element; any exception path inside an engine nav-bar build.
S5. MapView types: the engine creates MapViews by type (MapScreen.AddMapView<T> or similar); a type now in TAOM.Client is created from core code that names it only through a seam: confirm the creation path compiles to no core reference to a client type and still runs.
S6. Moved files' gates: each gate that names a moved file by path or scans TAOM.dll by type still fails for its stated reason.

REQUIRED SECTIONS:
1. ENGINE AND TAOM CODE you rely on, as code blocks with paths and lines.
2. Per suspect S1-S6: CONFIRMED / DISPUTED / UNVERIFIED with a concrete scenario and the deciding line.
3. Other findings.
4. FINDINGS table: severity, file:line, defect, scenario, fix.

QUALITY GATES: every claim cites a file and line you read in this run; mark unproven claims UNVERIFIED; no style-only nits.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to docs/reviews/raw/codex-adversarial-taom-client-b4b-2026-10-22.md; do NOT write that file yourself.
