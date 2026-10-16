ADVERSARIAL REVIEW: yotthani review code items C1 to C4 (#806, #807, #808, #809) and their review fixes

REVIEW ONLY. Do not modify, create or delete any file anywhere. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-yotthani-review-2026-10-15.md, so never write that path yourself.

Scope: `git diff caefaa1f3..382f5190a` in this worktree (E:\repos\taom-yotthani-1015, branch perf/yotthani-review-20261015). Commits: 49cd7ad46 (C1), 0d1ff1710 (C3), 5b4bab937 (C2), 548c3804d (C4), 382f5190a (RCA and lessons). Target: Bannerlord v1.5.5; engine source C:\Users\mikew\.taom-src\v1.5.5; installed game E:\Steam\steamapps\common\Mount & Blade II Bannerlord. A seven-lens Claude deep review and a convergence pass already ran; their findings and fixes are in docs/reviews/rca-yotthani-review-2026-10-15.md. Find what they missed; do not re-report a finding the RCA lists as fixed unless the fix is wrong.

The change:
- C1 (#806): v1.5.5 row in TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs KnownBuilds; that class and ClipBudgetSignatureInstalledBinaryTests tagged BindingVerification so an unlisted build fails the binding gate (TAOM.Tests/binding-gate.runsettings maps Inconclusive to failed); new TAOM.Tests/Migration/NativePinGateTests.cs keeps every KnownBuilds class tagged and named in .claude/skills/engine-bump/SKILL.md.
- C3 (#808): TAOM.Tests/Composition/VanillaTuningOverlapContractTests.cs pins the assembly name TAOM and four type names that VanillaTuning v0.1.1 (a separate mod by yotthani, not in this repo) uses to stand aside: TAOM.Features.NameplateCull.NameplateCullModule, TAOM.Features.MapViewRelease.MapViewReleaseModule, TAOM.Features.MissionStartGuard.MissionStartGuardModule, TAOM.Features.SkeletonBuffer.SkeletonBufferModule.
- C2 (#807): Main/Composition/LifecycleStep.cs; Main/SubModule.cs OnBeforeInitialModuleScreenSetAsRoot runs CustomizeMenu through LifecycleStep.Run. v1.5.5 Module.cs (about lines 538-551 and 790-799) calls every module's hook with no try and retries while no state is active; TAOM's Patch37 finalizer on Module.OnApplicationTick swallows a throw.
- C4 (#809): a cameraMs column. Main/Features/MissionPerf/Hooks/Patch98_HitchProbe.cs brackets MissionScreen's explicit IMissionSystemHandler.UpdateCamera in its own category Patch98_HitchProbe_Camera (HitchProbeInstaller applies it only after Patch98); HitchProbeHooks.Camera.cs; MissionTickProfiler subtracts cameraMs from missionTickMs (probe mode) or otherMs (full mode); TickProfileLines and HitchProbeLines write it; tools/perf_runs.py reads it.

READ FIRST: AGENTS.md, .ai/review-reference.md, docs/features/mission-perf-heartbeat.md (the probe, modes, status lines, Cost), docs/features/skeleton-buffer-guard.md (After an engine bump), docs/reviews/rca-yotthani-review-2026-10-15.md.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with TAOM and v1.5.5 engine file:line):
S1. Camera time accounting. Find every path where the camera bracket records time that is not inside a measured Mission.OnTick bracket, or where Mission.OnTick is measured but the camera call belongs to another mission or screen (a second MissionScreen, a mission ending inside the frame, replay or editor missions, photo mode, a paused mission, fast-forward sub-ticks): can missionTickMs or otherMs go negative or double-count? Check MissionTickProfiler's subtraction in both modes and in [Hitch], [TickProfile] and [TickSummary].
S2. Partial apply and PatchShield. If the camera category fails inside Harmony's class processor, can the prefix be patched without its finalizer, or the reverse? PatchShield strips a shielded owner's prefixes after a missing-API exception and never its finalizers: does the camera finalizer then record nothing and warn at most once, as for the other five brackets (ProbeState via __state)?
S3. C2 behaviour change. Before, a CustomizeMenu throw ended the hook early: Patch55, the MainMenu phase's category applies, EndPhase and the persistent startup report did not run on that call, and the engine retried the whole hook each tick. Now the rest of the hook runs on the first call. Is anything after CustomizeMenu unsafe to run when the customization failed, or unsafe to run again if the engine still retries for another module's throw? Is the once-per-process logic for the MainMenu phase still correct?
S4. LifecycleStep: thread safety of the once-per-name set, a logger factory or logger that throws, a null name, and whether "not latched" can make a throwing step spam the log or re-run side effects each tick.
S5. NativePinGateTests: can its class-level tag regex give a false pass (attribute on another class in the same file, a comment, a partial class, a renamed class) or a false fail? Does the binding gate as documented actually select these classes (filter and runsettings)?
S6. tools/perf_runs.py: a log mixing lines with and without cameraMs inside one run; the hitch phase pick with ties; aggregate rates; old logs still parse; compare unaffected.
S7. Docs and comments that state engine behaviour (the camera call site and conditions in v1.5.5 Mission.cs and MissionState.cs; PreDisplay attribution): any statement the v1.5.5 source contradicts.

QUALITY GATES: quote the TAOM code and the v1.5.5 engine code for every finding; mark anything you could not prove UNVERIFIED; say "no finding" plainly per suspect.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: findings with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, quoted code, reproduction and fix; "no finding" plainly per suspect. Return the full report as your FINAL MESSAGE.
