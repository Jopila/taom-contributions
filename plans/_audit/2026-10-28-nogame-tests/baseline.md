# Baseline at `370b4bf2b` (2026-10-28)

Measured in `E:\repos\taom-nogame-gate` at `370b4bf2b`, Git Bash, `BANNERLORD_GAME_DIR` set to the install. Logs:
`E:\Temp\claude\e--repos-TAOM\70752524-80d4-4812-90df-e9a680920c14\scratchpad\wf-research\rebased\`.

| Check | Command | Result |
|---|---|---|
| Install build | `dotnet build TAOM.Tests --no-incremental -p:DisableModuleCopy=true -p:ModuleId=` | 0 errors |
| Install full suite | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | 27,952 passed, 0 failed, 7 skipped (1 m 15 s) |
| Install strict gate | `... --settings TAOM.Tests/binding-gate.runsettings --filter TestCategory=BindingVerification` | 818 of 818 (59 s) |
| RefAsm build | `dotnet build TAOM.Tests --no-incremental -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` | 0 errors |
| RefAsm unit step | `.ai/verification.md`, second command | 7 failed, 23,334 passed, 2 skipped (47 s) |
| RefAsm gate | `.ai/verification.md`, third command | 591 of 591 (2 s) |

The 7 RefAsm unit failures, by class: `CoopMissionOwnershipTests` (4 `IsOwnedHere_*`),
`EnlistmentHourlyTickTests.OnHourlyTick_WithoutAuthority_TouchesNothing`,
`CampActivitySettingsTests.TheCampRecord_SavesTheFourWorkFields_UnderUniqueIds_ContinuingTheBlock` and
`TelcharsMark.Patch109WiringTests.Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses`.

Python tool tests: 5,923 ran, OK (11 skipped), on the tree before the rebase onto `f4d3ff6e6`, which changed two tool
test files; not re-run at the base. `validate_moduledata.py` and the hook suite: not measured, because the plan
changes test files only.
