# RCA: yotthani review code items C1 to C4 (deep review, 2026-10-15)

## Top-line

The `/deep-review` of the four code items from the 2026-10-15 yotthani review ran seven lenses in two waves on the
uncommitted worktree `E:\repos\taom-yotthani-1015`: 1 Standards, 2 Engine compatibility, 3 Efficiency and 5 Data
flow, then 4 Completeness, 6 Design and Tooling. XML was not in scope. It found no HIGH or CRITICAL defect, five
MEDIUM findings and a set of LOW ones. Every finding below was checked against the code, the v1.5.5 decompile, or
Harmony 2.4.2's decompiled `PatchClassProcessor` before it was fixed. A convergence pass on the fixes found four LOW
doc and comment defects (below), also fixed. At the maintainer's request, a fresh `/deep-review` and a
`/review-codex` follow on the fixed tree.

The items:

| Item | Issue | Change |
|---|---|---|
| C1 | #806 | The v1.5.5 `KnownBuilds` row for the skeleton-buffer pin; both native pin classes tagged `BindingVerification`, so an unlisted build fails the binding gate; `NativePinGateTests` keeps every table in the gate |
| C2 | #807 | `LifecycleStep.Run` guards `CustomizeMenu` in `OnBeforeInitialModuleScreenSetAsRoot`, which the engine retries every tick after a throw |
| C3 | #808 | `VanillaTuningOverlapContractTests` pins the four TAOM type names VanillaTuning v0.1.1 stands aside for |
| C4 | #809 | A `cameraMs` column: the camera and native PreDisplay time split out of `missionTickMs` and `otherMs` |

Review record: `docs/reviews/adopt-yotthani-2026-10-15.md` (it lands with the knowledge-base batch).

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The camera bracket was declared last in `Patch98_HitchProbe` "so a failure costs only this class". It did not: `PatchCategoryIndex.Apply` throws at the first class that fails, `SubModule.TryPatchCategory` returns false, and `HitchProbeInstaller` then turns off the whole probe and the full profiler for the session (Lenses 2 and 5) | A containment claim not traced to its consumer | The design reasoned about Harmony keeping the earlier classes patched, and never followed the category's result into the installer's flags | The camera class is its own category, `Patch98_HitchProbe_Camera`, applied only after Patch98 and kept out of `ProbeInstalled`; the claim is gone from the code and both docs. New lesson in `lessons/harmony-il.md` |
| 2 | MED | Once the camera category can fail alone, `cameraMs=0.00` would read as zero camera time, and the install line would still list `MissionScreen.UpdateCamera` (Lens 4) | A REPEAT: an install line that claims more than was checked (2026-10-08 finding 22) | Found only after fix 1 was chosen | `CameraNotInstalledLine` (one WARNING), and the install line omits the camera when its category failed. Recurrence added to `lessons/harmony-il.md` ("An install line claims only what was checked") |
| 3 | MED | `perf_runs.py compare` refused logs with and without `cameraMs`, but compare reads no tick-phase column, so the refusal guarded nothing and blocked every A/B across the change (Lenses 3 and 5) | A guard written without listing what it protects | The plan took the refusal from the design checker, and nobody listed which metrics compare reads | Refusal, `_camera_column` and its tests deleted; `test_compare_accepts_rows_with_and_without_camera_ms` added. Covered by the new lesson for finding 1 |
| 4 | MED | The new `/engine-bump` sentence said the pin tables FAIL in the binding gate; the two Python tables still skip on an unknown build and run only with `TAOM_GHIDRA_IT=1` (Lenses 1 and 5) | A list sentence edited for one member | The edit was written for the two C# tables and not re-read against the Python ones in the same sentence | Split into the C# rule and the Python rule (read the skip count). One-off |
| 5 | MED | The new frame-table clause carried yotthani's 1.1 to 3 ms and the culling and posing description under a `[TAOM-verified]` table tag, as "measured upstream" (Lens 1 rated it MEDIUM, Lens 2 LOW) | Attribution: a REPEAT of 2026-10-08 finding 26 (upstream figures stated as TAOM facts), its second recurrence | The implementer copied the figure from the issue body, which names yotthani, but not the name | Tagged `[yotthani, MithrilForge docs/engine/perf.md, measured in game, v1.5.3 and v1.5.4]`; TAOM's own decompile (`pre_display` 0x6FCDB0 is a thunk to 0x7D800 that builds the frame's render context) is stated separately. Recurrence added to `lessons/native-cpp-port.md` ("A count in a reference doc names its method"): a copied figure takes its source tag in the same edit |
| 6 | LOW | Docs said the camera runs "after the behaviour loops" (it runs between them, v1.5.5 `Mission.cs:3741-3753`) and "once per tick" (one call site, made only when the mission is Continuing, not on a dedicated server, and with `updateCamera` true; fast-forward sub-ticks pass false, `MissionState.cs:171-219`) (Lenses 1, 2 and 5) | Engine order written from a summary | The text followed the design's summary of the call chain and was not re-read against `Mission.OnTick` and `MissionState` | Corrected from the decompile |
| 7 | LOW | The doc samples and their pinned literals no longer summed to `wallMs`/`frameMs` (they overshot by `cameraMs`) (Lens 5) | Pinned examples not re-derived | The samples were edited for the new column only | `otherMs` re-derived (4019.50, 13.20, 60289.75) in the doc, `TickProfileLinesTests` and `test_perf_runs.py` |
| 8 | LOW | The cost evidence (benchmark, `ProbeCostMeter`) covered five brackets while the docs quoted it for six (Lenses 3 and 5) | Evidence not extended with the change | The design listed the benchmark as unchanged | Camera added to both; re-measured at 0.70 to 0.75 us per simulated frame (four saved runs, desktop). The fixer's first figures (0.75 to 0.96 us) had no saved output; the convergence pass marked them UNVERIFIED and the runs were repeated |
| 9 | LOW | Two camera tests and the benchmark passed only when an earlier test had loaded the game assemblies; alone they threw `FileNotFoundException` for `TaleWorlds.MountAndBlade.View` (found by the fixer) | Hidden test-order dependency | Every run was the full suite or the feature filter | `GameAssemblies.EnsureLoaded()` guard (Inconclusive without the game) |
| 10 | LOW | `NativePinGateTests` matched the category tag anywhere in the file (a method tag would pass) and scanned the test tree twice (Lenses 3 and 4) | Gate test too loose | Written to pass on the tree it was written for | Tag match anchored to the class declaration, with a test that a method tag does not count; one cached scan |
| 11 | LOW | The main-menu shape test matched the guarded call by its shape, not by a count, so a second, bare call could pass; the `Stop()` wrap guarded a call that only sets a volatile flag (Design lens) | Over-wide guard, under-strict test | The design wrapped both calls by symmetry | `Stop()` is a direct call; the test counts the `CustomizeMenu()` calls |
| 12 | LOW | The warning tag `[Lifecycle]` belongs to Load-Time Stamps' line family (Lens 5) | Log-tag collision | Not checked against `load-time-stamps.md` | Tag `[MainMenuStep]` |
| 13 | LOW | The provenance register had no line for `VanillaTuningOverlapContractTests.cs`, and its MithrilForge row did not cover `mission-perf-heartbeat.md` (Lens 1) | Register not updated with the code | The register pass belonged to the docs batch | Written in the docs batch's register pass, which lands in the same push; the C3 commit body says so |
| 14 | LOW | Smaller items: stale counts in comments ("three shielded targets", "five exits"), a comment filing the camera under decision D13, timing tests with fixed ceilings, missing asserts on `CameraMs`, two lists of the same target names, two redundant tests, an unused general `OPTIONAL_PHASES`, a `__main__` guard above a test class, the hitch probe's MCM hint (all lenses) | Polish | Within normal review scope | Fixed |

## Convergence pass

One `deep-reviewer` re-checked the fix round. It found no runtime or behaviour-parity defect and four LOW defects in
docs and comments, all fixed:

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| D1 | LOW | Four places said a camera category failure "joins the startup notice". The category applies at game init (`SubModule.cs` `InstallIfEnabled` call), whose failures go to a red chat line; the startup inquiry belongs to the MainMenu phase, and `TakeFailureSummary` clears its list after each phase | The orchestrator's fix list (item C4-15) stated the notice from memory, and the fixer copied it | "joins the game-initialization notice, a red chat line" in the code comment, the feature doc (twice) and the registry |
| D2 | LOW | The D13 paragraph added `MissionScreen.UpdateCamera` to its list but still said D13 "took them off" and "those three brackets"; UpdateCamera was never excluded | A list sentence edited for one member, the same shape as finding 4 | D13's three methods kept; one sentence says UpdateCamera is shielded by the 2026-10-15 decision (#809). The code comment's "plan 041 camera bracket" became #809: plan 041 is the original probe |
| D3 | LOW | `HitchProbeInstaller`'s summary still said "Logs one line either way" | Summary comment not re-read after finding 2's fix | Names the install line and the camera WARNING |
| D4 | LOW | The camera cost sentence counted PatchShield's finalizer, then pointed to a benchmark that attaches only the probe's pairs | Pointer not checked against what the benchmark measures | The sentence says the Cost section measures the probe's pair only and points to `PatchShieldPolicy` for the shield's figures |

It also marked the benchmark figures UNVERIFIED (finding 8), and listed cosmetic items (an unwrapped comment and doc
line, three blank lines before a Python test class, a stale step name in `LifecycleStepTests`), all fixed.

## Root-cause pattern: a claim about another component written without reading that component

Findings 1, 3, 4 and 6, and D1 of the convergence pass, share one cause. Each statement described what another piece of code does: the installer
after a category failure, `compare` after a column change, the Python pin tests, `Mission.OnTick` and `MissionState`.
Each was written from the author's picture of that code rather than from the code. The 2026-10-08 RCA found the same
shape twice (items 10 and 11: a log line and an in-game step not traced to their consumers). The preventive rule
already exists in `evidence-over-claims.md`; the new lesson in `lessons/harmony-il.md` makes it concrete for patch
categories, where the cost is a whole diagnostic switched off.

## Why each lens caught what it did

- **Engine compatibility (2) and Data flow (5)** both found finding 1, by following `TryPatchCategory`'s result into
  the installer.
- **Efficiency (3) and Data flow (5)** found finding 3: the refusal cost every A/B and protected no metric that
  compare prints.
- **Completeness (4)** found finding 2, which only exists once finding 1's fix is chosen.
- **Design (6)** chose the fix for finding 1 (a category, not a `Prepare()`, which would cover only a missing target)
  from Harmony 2.4.2's `PatchClassProcessor`, and found finding 11.
- **Standards (1)** found findings 4, 5 and 13 and most of the polish.

## Engine bump

The missing v1.5.5 skeleton-buffer row is recorded as F10 in
[rca-engine-bump-v1.5.5-2026-10-14.md](rca-engine-bump-v1.5.5-2026-10-14.md).

## Follow-ups (not in this change)

- **Decision, not filed:** TAOM's Patch37 finalizer on `Module.OnApplicationTick` swallows a throw that the engine
  then retries every tick, so any module's main-menu throw becomes a loop. Filing it needs the maintainer's word.
- **Shared test helpers:** the tests carry several copies of a brace-matching method-body walker and of an
  obj/bin-skipping source walk; one `RepoPaths` helper each would replace them (Design lens).
- **Python native pins** still skip without `TAOM_GHIDRA_IT=1`; the engine-bump skill now says to read their skip
  count.

## Feedback memories to codify

None. The lessons:

- `lessons/harmony-il.md`: a new lesson for findings 1 and 3 ("A patch category fails as one unit"), and a
  recurrence on "An install line claims only what was checked" for finding 2.
- `lessons/native-cpp-port.md`: a recurrence on "A count in a reference doc names its method" for finding 5, and a
  new lesson for the engine bump's F10 ("A native pin table fails the engine-bump gate on a build it does not list").

Findings 2 and 5 are repeats, so their preventive action is the existing lesson plus a concrete rule for this shape;
no new rule file, since `evidence-over-claims.md` and `provenance.md` already state the general rule.
