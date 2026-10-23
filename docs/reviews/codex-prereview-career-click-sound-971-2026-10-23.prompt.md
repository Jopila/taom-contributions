# Codex pre-review: career button click sound (#971)

You are an adversarial reviewer for TAOM, a Bannerlord v1.5.5 mod (.NET Framework 4.7.2). Review only. Do not
edit files. Working tree: `E:\repos\taom-s3` (uncommitted diff against `HEAD`; run `git diff` and read the new
untracked test file).

## The change

Issue #971 (`gh issue view 971` if you have access; summary here). The career button on the character developer
screen binds `Command.Click="ExecuteOpenCareerScreen"` (`Client/Features/CareerSystem/UI/CareerButtonPrefab.cs`).
The mixin `CharacterDeveloperCareerMixin.ExecuteOpenCareerScreen` had no `[DataSourceMethod]`, so UIExtenderEx
2.13.3 never added it to the view model, and the click sound and log line in it never ran. The click works through
the core Patch27 postfix on `ViewModel.ExecuteCommand`.

Files:
- `Main/Features/CareerSystem/Hooks/Patch27_CareerSystem/ViewModel_ExecuteCommand_CareerScreen_Patch.cs`: plays
  `UISoundsHelper.PlayUISound("event:/ui/default")` before `ExecuteDone()` and `GauntletCareerScreen.OpenCareerScreen()`.
- `Client/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs`: deletes `ExecuteOpenCareerScreen` and the
  now unused `using TaleWorlds.MountAndBlade.View;`.
- `TAOM.Tests/Features/CareerSystem/CareerButtonClickTests.cs` (new): IL pins for the sound call, its order, the
  event string, and the absence of the mixin method.
- `docs/features/gui-sprite-system.md`: one line.

## Questions to answer with evidence

1. Data flow: is there any other path that invokes `ExecuteOpenCareerScreen` (another prefab, a hotkey, a
   reflection call, the career switch flow) that relied on the mixin method, so deleting it loses behaviour?
   Grep `Main/`, `Client/`, `TAOM.Tests/` and the XML/prefab strings.
2. Does `ViewModel.ExecuteCommand` in v1.5.5 really skip a public mixin method without `[DataSourceMethod]`?
   Verify against the installed DLLs (`pwsh tools/taom-src.ps1 path ViewModel`, and UIExtenderEx's
   `ViewModelComponent` if you can decompile it). If the mixin method WAS reached, the sound now plays twice or
   the screen opens twice; say which.
3. Can the postfix fire more than once per click (for example once per mixin-wrapped view model or for the base
   and the derived call), doubling the sound?
4. Server safety (ADR-013, `docs/adrs/013-server-safe-core-client-ui-submodule.md`): `UISoundsHelper` lives in
   `TaleWorlds.MountAndBlade.View`, a client-only assembly. The postfix already referenced
   `CharacterDeveloperVM` (also client-only). Does the new call change whether TAOM.dll loads or this patch
   applies on a dedicated server? Read `TAOM.Tests/Infrastructure/ServerLoadGateTests.cs`.
5. Do the tests prove the claim, and does each one fail if the fix is reverted (the sound call removed, moved
   after `OpenCareerScreen`, or the mixin method restored)? Check the `ldstr` scan for false positives.

## Output

Findings as CRITICAL, HIGH, MEDIUM, LOW, each with file:line, the failure scenario and the proving evidence.
Mark anything you could not verify as UNVERIFIED. If you find nothing, say PASS and list what you checked.
