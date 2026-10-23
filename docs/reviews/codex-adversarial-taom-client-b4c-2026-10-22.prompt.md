Adversarial review: the UNCOMMITTED batch B4c of ADR-013 in the worktree E:\repos\taom-b4c, branch feat/b4c-client-moves, base fd2072a0a. REVIEW ONLY: do not edit any file, do not run git commands that change state, do not run ./build.ps1 or any deploying build. Builds and tests, if you run any, use `-p:DisableModuleCopy=true -p:ModuleId=` only. Never touch E:\repos\TAOM (another session works there). Other reviewers read this tree at the same time; that is fine.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia".

READ FIRST:
docs/adrs/013-server-safe-core-client-ui-submodule.md (decision 3: no TAOM mission behaviors on a dedicated server)
docs/research/taom-headless-server.md (sections "The client hand-off", "The gates", "Batches", problem rows 11 and 13)
The change: `git status --short`, `git diff fd2072a0a -M --stat`, `git diff fd2072a0a -M` (new files are intent-to-add, so they show in the diff).
The build plan with its decompile citations: E:\Temp\claude\handoffs\b4c-build-plan.md (sections 1.1 to 1.6 hold the engine claims; section 12 the base-state notes).
Engine v1.5.5 signatures: the installed DLLs via `pwsh tools/taom-src.ps1 path <Type>`; decompile dump E:\Decompiled_Bannerlord\_categories_v1.5.5\ (it can lag; the DLLs win).

DESIGN IN ONE PARAGRAPH:
TAOM.dll must load on the co-op dedicated server, where UI assemblies are absent. B4c moves the seven mission, CustomBattle-engine and WinForms types into the client-only submodule TAOM.Client with git mv and unchanged namespaces: AutonomousMovementPlayerController (a [DefaultView] MissionView, the warg rage-mode player controller), BattleActionBarMissionView, HostReviewVisualOrderProvider, TaomShaderGameManager (the parked shader walk), ShaderCompileNoticeWindow and WindowHandleOwner, and the nested ShaderCompileNotice+NoticeContext, extracted as the top-level ShaderCompileNoticeContext (the static ShaderCompileNotice class stays in TAOM.dll for a by-name contract). Core inverts its callers through five seams, each a core interface with a null implementation registered in core and a real one registered by TaomClientComposition through ClientComposition.Apply: IAutonomousMovementControllerFactory and IBattleActionBarViewFactory (SubModule.AddTaomMissionBehaviors keeps its add order and the co-op filter; each factory returns MissionBehavior?), IHostReviewOrderProviderRegistrar (HostReviewModule.OnPhase ProcessLoad), IShaderPrecompileGameManagerFactory (ShaderPrecompileRunner.StartCurrentItem; returns MBGameManager?), IShaderCompileNoticeWindowHost (ShaderCompileNotice.Run on its STA thread; resolved at Main/SubModule.cs:130 right after IoC.Configure). The Warg code (WargMissionBehavior, WargRiderHandManager, FinishRageMode, PrepareOnFirstCall) now finds the controller with Mission.GetMissionBehavior<IAutonomousMovementController>(), a core interface the client controller implements; the vanilla half of the controller swap (MissionMainAgentController Disable/Enable) moved into the client class. CoopMissionRoles keys the two moved views by string literal. A new guard in AddTaomMissionBehaviors logs and adds no TAOM behavior on a dedicated server. Main/TAOM.csproj drops System.Windows.Forms and System.Drawing. The ServerLoadGateTests allowlist drops from 26 to 19.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code):
S1. Mission.GetMissionBehavior<IAutonomousMovementController>() does not return the same instance as the old GetMissionBehavior<AutonomousMovementPlayerController>() (an exact-type comparison, a per-type cache, or a different first match), so rage mode drives the explicit instance instead of the [DefaultView] one, or finds none. Read the v1.5.5 implementation.
S2. The engine's [DefaultView] scan (ViewCreatorManager) does not see the controller in TAOM.Client: TAOM.Client's reference to TaleWorlds.MountAndBlade.View does not match by AssemblyName.ToString(), or the scan runs before TAOM.Client.dll is in the AppDomain. Then the default instance is missing and rage mode logs "[Warg] AutonomousMovementPlayerController not found".
S3. A singleton in the graph of any of the five seams is resolved before ClientComposition.Apply (inside a Register*Feature method, a static initializer, an earlier eager Resolve, or SubModule code that runs before IoC.Configure), so single player keeps a null implementation: no shader notice, no battle action bar, no explicit controller, no Host Review order bar.
S4. A core method body still names a moved type, MissionMainAgentController, VisualOrderFactory, CustomGameManager or System.Windows.Forms, so it JIT-fails on the server, or compiles only because the test project references TAOM.Client.
S5. Behaviour change in single player: the add order of mission behaviors, the count of controller instances, the pre-tick order, the co-op role of the two moved views, the [BattleLoad] bracket, or the dedicated-server guard's placement (it must not skip the loading-window closer, and must not run before work that a server needs).
S6. The shader notice: the Func<bool> stop flag keeps its volatile read; the STA thread, the start/stop flags and the 30-minute cap behave as before; an exception in the client host is still caught; the notice still starts before the engine's shader compile (resolve timing at SubModule.cs:130).
S7. The parked shader walk: a null game manager follows the same path as a StartNewGame failure (counters, BeginEnd, generation), and re-enabling the feature is still "uncomment three".
S8. The gates: each changed or new test can fail for its stated reason (a source pin that reads a file that moved, a reflection test that resolves from the wrong assembly, a count that matches by accident).

REQUIRED SECTIONS:
1. ENGINE AND TAOM CODE you rely on, as code blocks with paths and lines.
2. Per suspect S1-S8: CONFIRMED / DISPUTED / UNVERIFIED with a concrete scenario and the deciding line.
3. Other findings.
4. FINDINGS table: severity, file:line, defect, scenario, fix.

QUALITY GATES: every claim cites a file and line you read in this run; mark unproven claims UNVERIFIED; no style-only nits.

Prior review lessons:
SUCCESSES: Lifecycle tracing caught DI resolves before ClientComposition.Apply (B4a). Vanilla decompilation caught missing gates. Codex found the B3 defects seven Claude lenses missed.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to docs/reviews/raw/codex-adversarial-taom-client-b4c-2026-10-22.md; do NOT write that file yourself.
