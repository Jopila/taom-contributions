Adversarial review: an UNCOMMITTED build and release change in E:\repos\TAOM, branch bannerlord-1.5.x, HEAD 4ab005dd1. REVIEW ONLY: do not edit any file, do not run git commands that change state, do not run ./build.ps1 or any deploying build. Builds and tests, if any, only with `-p:DisableModuleCopy=true -p:ModuleId=`. Other reviews read the tree at the same time.

READ FIRST: `git status --short`, `git diff HEAD`, the untracked TAOM.Tests/Infrastructure/StubModuleDeploymentTests.cs; docs/reviews/rca-taom-client-b3-2026-10-22.md and commit e0a6970dd (the deploy design this changes); Bannerlord.BuildResources 1.1.0.129 build/Basic.targets and Basic.props (C:\Users\mikew\.nuget\packages\bannerlord.buildresources\1.1.0.129\build\ or E:\nuget\packages\...); installed v1.5.5 engine via `pwsh tools/taom-src.ps1 path <Type>`.

THE CHANGE (Mike's decisions, 2026-10-22):
1. Game Pass is not supported: Main/TAOM.csproj, Client/TAOM.Client.csproj and Dependencies/TAOM.Dependencies.csproj no longer stage or deploy a `Gaming.Desktop.x64_Shipping_Client` bin; the B3 target DeleteStaleGamePassTaomClient is deleted; tools/package_release.py drops that bin from CLIENT_PLATFORM_BINS and leaves any such folder out of a release with a notice.
2. Mike builds in Visual Studio, whose fast up-to-date check can skip Main and still deploy Client: DeployTaomClientToTaomModule now hashes the core TAOM.dll the client was built against (from @(ReferencePath)) and the installed Win64_Shipping_Client\TAOM.dll with GetFileHash, and fails with an Error when they differ (not on a first deploy); a second Error when the core reference is not found.
3. The release gate (require_build) reads the DLL names from the release commit's SubModule.xml (git show) and refuses when the installed manifest names a different set.
4. Issue #969: DeployTAOMDependenciesStubs now deploys Stubs\<ID>\_Module\SubModule.xml to Modules\<ID>\SubModule.xml (the launcher reads only that path: ModuleHelper.GetPhysicalModules).

KNOWN SUSPECTS (CONFIRM or DISPUTE with code):
S1. The VS guard fires wrongly: during a normal solution build or ./build.ps1 (Main deploys first, then Client; are the hashes equal at that moment?), on a non-deploying build, when Main is built Release and Client Debug, when the ReferencePath is the obj/ref assembly, or with incremental builds where the installed TAOM.dll was written by CopyBinariesWindows from a different path than the reference.
S2. The VS guard never fires in the Visual Studio case it exists for (Main skipped, installed core older): trace which TAOM.dll ReferencePath names when Main is skipped.
S3. With the Game Pass bin gone, can any path still write TAOM.Client or TAOM.dll into an installed Gaming.Desktop folder (CopyModule of _Module/bin), and does a stale installed Gaming.Desktop folder now get shipped or refused correctly?
S4. require_build: a release where the commit's manifest and the installed one differ only in case or order; TAOM.Dependencies; a commit without the file; shallow history.
S5. The stub deploy: %(RecursiveDir) with Replace('_Module\',''); a stub with nested folders; MSBuild metadata evaluation inside the target; existing installs with the old _Module folder (does the launcher now see two modules with the same id? does ModuleHelper look in subfolders?).
S6. Each new or changed test can fail for its stated reason.

REQUIRED SECTIONS: 1. Code you rely on, as code blocks with paths and lines. 2. Per suspect: CONFIRMED / DISPUTED / UNVERIFIED with a concrete scenario and the deciding line. 3. Other findings. 4. FINDINGS table: severity, file:line, defect, scenario, fix. Every claim cites a file and line you read in this run; mark unproven claims UNVERIFIED; no style-only nits.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to docs/reviews/raw/codex-adversarial-deploy-gamepass-vsguard-2026-10-22.md; do NOT write that file yourself.
