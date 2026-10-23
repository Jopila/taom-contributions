Focused re-review of review FIXES for TAOM issue #970 in the git worktree E:\repos\taom-actionbar (branch fix/battle-action-bar). REVIEW ONLY: do not edit, no state-changing git, no ./build.ps1 or deploying build; builds and tests only in the worktree with `-p:DisableModuleCopy=true -p:ModuleId=`. Do not read E:\repos\TAOM.

The committed fix is HEAD d96f349f1 (`git -C E:\repos\taom-actionbar show HEAD`). The uncommitted review fixes are `git -C E:\repos\taom-actionbar diff` plus the untracked TAOM.Tests/Features/CompanionTactics/BattleActionBar/BattleActionBarInputTests.cs. Your earlier review: E:\Temp\claude\e--repos-TAOM\ec2ae0bd-3056-47de-9090-304830ca23ab\scratchpad\codex-actionbar.out.md (after the last "tokens used"). Engine v1.5.5 from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>`.

Mike's decision: the bar is click-only (no hotkeys) and off by default, because its stances are display-only.

FIXES (check each closes its finding and adds nothing new):
A. Input: the layer now calls SetInputRestrictions(false, InputUsageMask.MouseButtons); BattleActionBar.xml root widget and background panel carry DoNotAcceptEvents="true". Confirm MissionMainAgentController attack and block run with the bar attached (the !MissionScreen.MouseVisible gate), the camera keeps mouse-look, the scene layer still receives mouse buttons outside the bar's buttons (ScreenManager.EarlyUpdate hit test), and the buttons are still clickable while a formation is selected with the cursor hidden (can the player click at all without a visible cursor? If the bar is only clickable with the order menu's cursor, say so).
B. Hotkeys removed; click path resyncs the highlight immediately.
C. Setting renamed EnableBattleActionBar to ShowBattleActionBar, default false; every reader renamed.
D. Disabling mid-battle detaches the layer, releases restrictions and finalizes the VM; re-enabling attaches once.
E. Stances cleared at attach.

REQUIRED: per fix A-E: CLOSED / NOT CLOSED / NEW DEFECT with the deciding file:line read this run, then a FINDINGS table (severity, file:line, defect, scenario, fix). Mark unproven claims UNVERIFIED. No style-only nits.

OUTPUT: the full report as your FINAL MESSAGE; do NOT write any file.
