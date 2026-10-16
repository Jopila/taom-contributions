ADVERSARIAL REVIEW (focused): the fixes from the second review of the yotthani code items (#806, #807, #809)

REVIEW ONLY. Do not modify, create or delete any file anywhere. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-yotthani-review-fixes-2026-10-15.md, so never write that path yourself.

Scope: `git diff 382f5190a..b3f79a7be` in this worktree (E:\repos\taom-yotthani-1015, branch perf/yotthani-review-20261015). Commits: bf5e69dd7 (MainMenuStep: renamed from LifecycleStep, logs the full exception), bc8733ba7 (NativePinGateTests rewritten to reflection over types that declare KnownBuilds), 3407a8bec (perf_runs.py aggregate_ticks keeps camera time in a mixed row; the _no_camera test helper; comments and docs on the PreDisplay gate, PatchShield timing, the skipped-class path, the cost A/B, the [HitchDetail] sample), b3f79a7be (RCA and lessons). Target: Bannerlord v1.5.5; engine source C:\Users\mikew\.taom-src\v1.5.5\<FullTypeName>.cs. Your previous report on caefaa1f3..382f5190a found F1 (perf_runs mixed schemas) and F2 (NativePinGateTests text match); the RCA's "Second review" section lists every finding and fix: docs/reviews/rca-yotthani-review-2026-10-15.md.

READ FIRST: AGENTS.md, .ai/review-reference.md, the RCA's "Second review" section.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line):
S1. Your F1 fix: tools/perf_runs.py aggregate_ticks now shows cameraMs when ANY window has it and sums only the present values. Is every consumer of phase_ms_per_s (format_rows, the hitch phase pick, JSON output) correct when cameraMs is present in only some windows? Does any output now read as if the camera rate covered all windows?
S2. Your F2 fix: TAOM.Tests/Migration/NativePinGateTests.cs. Can AccessTools.GetTypesFromAssembly drop a pin class silently (a type that fails to load in a no-game or reference-assembly run)? Does CollectionAssert.IsSubsetOf with the two named classes close that? Do the nested fixtures ClassTagged and MethodTagged get picked up by MSTest discovery or by the scan? Is TestCategoryBaseAttribute with inherit false the right read?
S3. MainMenuStep logging `{ex}`: any path where building the message can throw, any very large message risk, and does the once-per-name latch still hold?
S4. The _no_camera helper and the new fidelity test in tools/tests/test_perf_runs.py: do they reproduce what a pre-#809 build wrote, in both modes?
S5. Docs and comments in the range that state engine behaviour (the PreDisplay gate in v1.5.5 MissionScreen.UpdateCamera, PatchShield's pass timing against TAOM's game-init category apply, the empty-category path in PatchCategoryIndex and PatchCategoryApplier.TryApply): any statement the code or v1.5.5 source contradicts.

QUALITY GATES: quote the code for every finding; mark anything unproved UNVERIFIED; "no finding" plainly per suspect.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: findings with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, quoted code, reproduction and fix; "no finding" plainly per suspect. Return the full report as your FINAL MESSAGE.
