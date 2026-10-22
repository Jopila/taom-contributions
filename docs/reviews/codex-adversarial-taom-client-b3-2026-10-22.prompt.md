Adversarial review: TAOM commit 03d9db8dc "feat(coop): v2.0.35 - TAOM.Client submodule for client-only UI" (batch B3 of ADR-013), on branch bannerlord-1.5.x. REVIEW ONLY: do not edit any file, do not run git commands that change state, do not run ./build.ps1 or any deploying build.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/adrs/013-server-safe-core-client-ui-submodule.md
docs/research/taom-headless-server.md (sections "The client hand-off", "The gates", "Batches")
`git show 03d9db8dc --stat` and the full diff.
Engine v1.5.5 decompile: C:\Users\mikew\.taom-src\v1.5.5\ (use `pwsh tools/taom-src.ps1 path <Type>`); dedicated server DLLs: E:\Steam\steamapps\common\Mount & Blade II Dedicated Server\bin\Win64_Shipping_Server.

DESIGN IN ONE PARAGRAPH:
TAOM.dll must load on the co-op dedicated server. B3 adds a second client-only submodule TAOM.Client (Client/TAOM.Client.csproj, ClientSubModule, TaomClientComposition) tagged DedicatedServerType=none and IsNoRenderModeElement=false in Main/_Module/SubModule.xml. Core never references TAOM.Client; the client's constructor sets the static slot Main/Composition/ClientComposition.Current, and Main/IoC.cs calls ClientComposition.Apply(container) after every core registration, through a DryIoc view that shares the registry with IfAlreadyRegistered.Replace. TAOM.Client deploys with its own MSBuild target (Bannerlord.BuildResources cleans the bin), the Win64_Shipping_Server mirror excludes TAOM.Client, tools/package_release.py requires TAOM.Client.dll only where the shipped manifest names it and refuses it in a server bin. Gates: ClientIsolationGateTests (core never references TAOM.Client; a tripwire bans patches/UI extensions/save definers/settings in TAOM.Client until their scans read it), ClientSubModuleManifestTests, ClientDeploymentTests, RepoPaths.CodeRoots/CSharpFiles shared by nine source scans.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code):
S1. Module.LoadSubModules constructs every submodule before any OnSubModuleLoad, in the client, Modding Kit and server builds, so the slot is set before IoC.Configure. Is there any launcher path (BLSE, the Modding Kit, a mod that loads submodules late) where TAOM's OnSubModuleLoad runs before TAOM.Client's constructor?
S2. The DryIoc Replace view: can a client registration leave two live instances of a singleton (a service resolved during core registration), or drop a core decorator or collection member unexpectedly?
S3. Deploy: after ./build.ps1, a lone `dotnet build Main` with deploy, or `-p:DisableModuleCopy=true` without `-p:ModuleId=`, can any bin end with a stale TAOM.Client.dll built against a different core? What happens at load (GetTypes CriticalError abort vs Cannot find box)?
S4. package_release.py: any release state that now passes but should fail, or fails but should pass (pre-B3 tag, Game Pass bin, _wEditor bin, server bin, case of names)?
S5. The gates: can each new gate fail for its stated reason (mutate the guarded code mentally and check)?
S6. Anything in B3 that changes single-player behaviour.

REQUIRED SECTIONS:
1. ENGINE AND BUILD CODE you rely on, as code blocks with paths and lines.
2. Per suspect S1-S6: CONFIRMED / DISPUTED / UNVERIFIED with a concrete scenario and the deciding line.
3. Other findings.
4. FINDINGS table: severity, file:line, defect, scenario, fix.

QUALITY GATES: every claim cites a file and line you read in this run; mark unproven claims UNVERIFIED; no style-only nits.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to docs/reviews/raw/codex-adversarial-taom-client-b3-2026-10-22.md; do NOT write that file yourself.
