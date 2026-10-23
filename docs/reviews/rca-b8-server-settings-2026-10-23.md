# RCA: B8 server settings file (ADR-013 decision 2), 2026-10-23

**Summary.** B8 lets a co-op dedicated server read `taom-server-settings.json` into fresh instances of
TAOM's four MCM settings classes, which each hide MCM's `Instance`. The tier 1 `/deep-review` (Codex at
`ultra`, lenses 5 Data flow and 2 Engine compatibility) found no CRITICAL or HIGH. It found two MEDIUM
defects and seven LOW ones. Both MEDIUMs came from the same move: B8 made the server hand out real objects
where it used to hand out null, and two pieces of older code had quietly depended on the null.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The co-op fingerprint rendered floats with `G9` (`SettingsFingerprint.cs:181`). The client runs .NET Framework 4.8 and the server runs .NET 6, and they print the same bits differently: `1.001953125f` is `1.00195313` on 4.0.30319 and `1.00195312` on 10.0.12 (both run on the desktop). Identical settings could give different hashes | Text rendering used as an identity across runtimes | The comment said "both peers run the same framework", which was true until a server existed. The plan read the fingerprint as already correct and only checked that the file values reach it. Lens 5 checked the float path on one runtime | Fixed: floats and doubles hash as their IEEE bits, -0 folded to 0. Tests `Float_RendersAsItsBitPattern_NotRuntimeDecimalText`, `NegativeZero_RendersAsZero`. Lesson in `lessons/misc.md` |
| 2 | MEDIUM | On a server with no file, `EnableHitchProbe` went from the provider's `?? false` (`BattleLoadDiagnosticsSettingsProvider.cs:77`) to the compiled `true` (`BattleLoadDiagnosticsSettings.cs:87`), so Patch98 and its `MissionScreen` camera patch installed on every server boot. The provider's contract (`IBattleLoadDiagnosticsSettingsProvider.cs:55-57`) promises fail-closed | A null source replaced by a default object flips every differing `??` fallback | The plan and the feature doc assumed every provider fallback equals its compiled default. Nobody compared the 285 fallback reads against the initializers until lens 5 scripted it | Fixed: the server set keeps `EnableHitchProbe = false`; the file can set it true. Tests `Server_HitchProbe_DefaultsOff`, `Server_HitchProbe_FileCanEnable`, `DefaultsReference_ShowsTheServerHitchProbeDefault`. Lesson in `lessons/gamemodels-services.md` |
| 3 | LOW | `ServerSettingsFileStore.WriteIfMissing` caught `IOException when File.Exists(path)` around the write as well as the create, so a write that failed after this process made the file reported success and left an empty or partial template that later boots keep | Race handler too wide | The race case was the only failure anyone pictured | Fixed: the benign catch covers creation only; a failed write deletes the partial file. Test `WriteIfMissing_WriteFailsAfterCreate_DeletesThePartialFile_AndReportsTheError` |
| 4 | LOW | A thrown load hands out no instance, so providers use their own fallbacks, but its message said "every setting keeps its compiled default" (`ServerSettingsSource.cs`, `ServerSettingsReport.cs`) | Message claims more than the code does | Written before finding 2 showed fallbacks and defaults differ | Fixed: the message names the provider fallbacks, and after finding 10 the boot line does too (test `Module_OnServer_LoaderThrow_BootLineSaysNoInstanceIsServed`) |
| 5 | LOW | B8's hide made `McmSettingsCollector` (`Public | Static`, no `FlattenHierarchy`, `:112`) find TAOM's settings classes for the first time; its `StringifyValue` then printed the six dropdowns as ``MCM.Common.Dropdown`1[System.String]`` | Newly reachable old code | The plan named the collector side effect but did not trace what the collector then renders | Fixed: dropdowns render the selected value. New `McmSettingsCollectorTests` |
| 6 | LOW | ModuleData config comments (`signature_strikes_config.json`, `uncapturable_heroes_config.json`, `armour_acquisition_config.xml`) and provider comments (`CombatMechanicsSettingsProvider.cs`, `FieldCommissionSettingsProvider.cs`) promised the config applies when MCM is absent; on a server it no longer does | Stale comments after a contract change | Same root as finding 2 | Fixed: comments reworded; the feature doc says customized configs must move into the server file |
| 7 | LOW | `server-settings.md` called the collector MCM's and said the file covers 453 settings (449 are settable; 2 buttons, 2 parked) | Doc inaccuracy | Counted reflected properties, not settable ones | Fixed |
| 8 | LOW | Operator traps left undocumented: Newtonsoft reads `030` as octal and `08` breaks the file; a `ThrowOnNext*` QA trigger in the file fires on every boot; a bare engine run puts the file in `Modules\TAOM`, which the server's `robocopy /MIR` re-stage purges; `FlattenHierarchy` lookups of `Instance` now throw `AmbiguousMatchException`; provider invariants can change a logged value | Missing operator guidance | The plan covered validation, not the operator's environment | Fixed: one note each in `server-settings.md` |
| 9 | LOW | After fix 2, log lines still said "compiled default" for a value that is the server default | A fix that changes a default leaves its labels behind | Found by the fix agent | Fixed: runtime messages say "server default" |
| 10 | LOW | After a thrown load the boot line still said "every setting is at its server default", because the thrown load reused the `Unreadable` state that an IO error also reports (`ServerSettingsSource.cs`, `ServerSettingsReport.BootLine`). A thrown load serves no instance | One state for two outcomes | Fix 4 changed the error line but not the boot line built from the state. Found by the Codex fix review and the lens 5 fix re-review | Fixed: a thrown load has its own state, `ServerFileState.LoaderFailed`, and its own boot line naming the provider fallbacks. Tests `Module_OnServer_LoaderThrow_BootLineSaysNoInstanceIsServed`, `LoaderThrow_ReportsLoaderFailed_NotUnreadable`, `IoError_StillReportsUnreadable_AndServesTheSet` |

**Follow-up:** `McmSettingsCollector` skips a third-party settings class that only inherits
`GlobalSettings<T>.Instance` (for example `Diplomacy.Settings`), because it looks `Instance` up without
`FlattenHierarchy`. This predates B8. MEDIUM; an issue is owed on Mike's word.

## Root-cause pattern

Findings 1, 2, 5 and 6 share one cause: **B8 changed what `X.Instance` returns on a server from null to a
real object, and older code had meaning attached to the null.** Finding 2 is a fallback that differed from
the default. Finding 5 is a reflection path that had never seen the object. Finding 6 is comments that
described the null world. Finding 1 is the same move at the runtime level: the fingerprint was correct only
while both peers ran one framework, and B8 made the first peer that runs another.

The plan did check the obvious readers of `Instance` (null gates such as `TaomSettingsProvider.IsAvailable`),
and the review confirmed those were intended. It did not compare the value every reader got before with the
value it gets after, which is the check that catches this class.

## How the review lenses found these

- **Codex (`ultra`)** found 1 by running the formatter on two runtimes and decompiling the staged .NET 6
  `Dragon4`, and found 3 from the exception flow.
- **Data flow (5)** found 2, 4, 5 and 6 by scripting a comparison of all 285 `?? fallback` reads and 90 clamp
  helpers against the compiled initializers.
- **Engine compatibility (2)** proved the hide is invisible to MCM, found the `FlattenHierarchy` and octal
  traps in 8 and the doc errors in 7.
- Codex and lens 5 disagreed on 1 (lens 5: "G9 on both sides"). The orchestrator settled it with a run on
  both runtimes; lens 5 had compared code paths, not runtimes.
- Lenses 1, 3, 4 and 6: SKIPPED BY TIER (no CRITICAL or HIGH).

## Feedback to codify

- **Lesson** (`lessons/gamemodels-services.md`): when a source that returned null starts returning an object,
  diff every reader's old fallback against the object's value before shipping.
- **Lesson** (`lessons/misc.md`): a value two processes compare is hashed from its bits, never from runtime
  formatted text, unless both processes provably share one runtime.
