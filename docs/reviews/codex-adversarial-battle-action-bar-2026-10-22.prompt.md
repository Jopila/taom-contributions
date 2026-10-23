Adversarial review: the UNCOMMITTED fix for TAOM issue #970 in the git worktree E:\repos\taom-actionbar (branch fix/battle-action-bar, from b5d6a026f). REVIEW ONLY: do not edit any file, do not run git commands that change state, do not run ./build.ps1 or any deploying build. Builds and tests, if any, only inside the worktree with `-p:DisableModuleCopy=true -p:ModuleId=`. Do not read or touch E:\repos\TAOM.

READ FIRST: `gh issue view 970`; the plan with engine citations E:\Temp\claude\e--repos-TAOM\ec2ae0bd-3056-47de-9090-304830ca23ab\scratchpad\actionbar-fix-plan.md; the change: `git -C E:\repos\taom-actionbar status --short`, `git -C E:\repos\taom-actionbar diff HEAD`, and the two untracked test files. Engine v1.5.5 signatures from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>` (run from the worktree); decompile dump E:\Decompiled_Bannerlord\_categories_v1.5.5\.

THE CHANGE: BattleActionBarMissionView (TAOM.Client) gets [DefaultView], so MissionScreen.OnAddBehaviors builds it before OnMissionAfterStarting registers views; it attaches its layer on the first screen tick where Mission.IsFieldBattle and Mission.Mode == Battle (field battles start in Deployment, which is why the old init-time gate never attached); the explicit adds of the bar and of AutonomousMovementPlayerController in Main/SubModule.cs are deleted, with both factory seams; a new gate says every TAOM MissionView carries [DefaultView].

KNOWN SUSPECTS (CONFIRM or DISPUTE with code):
S1. [DefaultView] makes the view exist in EVERY mission (towns, arenas, tournaments, conversations, hideouts, sieges, naval if present, custom battle, multiplayer if TAOM loads there): does any of its init or tick code run, allocate, subscribe or throw outside a field battle; is the field-battle check correct for sieges, hideouts, sally-outs and custom battles; does it read services that are null in some missions.
S2. The view is found in TAOM.Client by the engine's [DefaultView] scan (ViewCreatorManager): which assemblies does that scan read and when; is TAOM.Client loaded by then.
S3. Attach on the first Battle-mode tick: what if the mode goes Battle, then Deployment again, then Battle; what if the setting changes mid-mission; is the layer removed on finalize and on mission end without the old null-MissionScreen path; double attach.
S4. Deleting the explicit AutonomousMovementPlayerController add: is the Warg code's controller the [DefaultView] one in every mission it needs, and does anything else look up the deleted second instance.
S5. Co-op and a dedicated server: the view and its hotkeys must not run where the bar must not show (CoopMissionRoles); the server has no MissionScreen and no TAOM.Client.
S6. Hotkey collision: the bar's 1 to 9 keys versus vanilla's order hotkeys in a battle (UNVERIFIED is fine if you cannot settle it; say what you read).
S7. Each new or changed test can fail for its stated reason.

REQUIRED SECTIONS: 1. Code you rely on, as code blocks with paths and lines. 2. Per suspect: CONFIRMED / DISPUTED / UNVERIFIED with a concrete scenario and the deciding line. 3. Other findings. 4. FINDINGS table: severity, file:line, defect, scenario, fix. Every claim cites a file and line you read in this run; mark unproven claims UNVERIFIED; no style-only nits.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout to a file; do NOT write any file yourself.
