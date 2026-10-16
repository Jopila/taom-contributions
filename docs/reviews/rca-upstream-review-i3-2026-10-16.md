# RCA: I3 and the upstream-review process (deep review, 2026-10-16)

## Top-line

The `/deep-review` of two changes made on 2026-10-16 ran seven lenses in two waves on the uncommitted worktree
`E:\repos\taom-y1016`: 1 Standards, 2 Engine compatibility, 3 Efficiency and 5 Data flow, then 4 Completeness, 6 Design and
Tooling. The changes:

- **I3** (investigation 3 of the 2026-10-15 yotthani plan): the engine's `GetMainFps` and `GetRendererFps` are busy-time
  rates on the v1.5.5 client, so `[MissionPerf]` gains `mainMs=` and `renderMs=` and `[PerfContext]` gains `frameLimiter=`
  and `vsync=`; `tools/perf_runs.py` reads them.
- **The upstream-review process:** `docs/reviews/upstream-watch.json`, `tools/upstream_watch.py`, a session-start
  `UPSTREAM AHEAD` block and the `/upstream-review` skill.

No CRITICAL finding. One HIGH (the session-start time budget), and MEDIUM and LOW findings in the hook, the tool, the docs
and the skill. After the fixes, one convergence reviewer checked every fix and found only LOW items (findings 10 and 11),
one of them an incomplete fix of the HIGH. The maintainer then asked for every finding to be fixed: the commit gates
that judged the main checkout for a worktree commit (finding 12) and the pre-existing garrison defect the upstream
data exposed (finding 13). Lens 2 verified every engine API and re-derived every native claim; Lens 3 measured the per-frame read as
two loads and a divide. The knowledge batch of the same day went through its own find-and-verify doc review (record:
[adopt-yotthani-2026-10-16.md](adopt-yotthani-2026-10-16.md), "The doc review").

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The new session-start block ran `timeout -k 1 8` after two 4 s siblings inside one 10 s registration; with two sequential 4 s gh calls the hook could reach about 9.3 s, and a timed-out hook loses every banner (GAME VERSION DRIFT included) (Lenses 1, 3) | A bound sized for its own call, not the shared registration | The fast path was timed (3.1 s cold, 1.0 s cached), not the slow one; `test_hooks.sh` check 3 reads only the file's first bound and the generic run never reaches the block | The block takes the time left of a budget counted from the hook's first command; the tool takes a matching `--budget`, a 2.5 s per-call cap and the 11 KB page-2 compare, and saves each live answer; a new `test_hooks.sh` case runs the block with a stub tool, a hang included. A separate SessionStart registration is the better shape, but `settings.json` is config-protected and needs the maintainer's request. `hook-authoring.md` already holds the rule ("measure the SLOW path"); this is a repeat of its class |
| 2 | MED | The block printed nothing on a non-zero exit with empty output, and nothing when python, the tool or the state file was missing, while its comment said "never silent" (Lenses 1, 5) | A fail-open block that is fail-silent | The sibling's `else` branch was not mirrored | Every outcome prints: the output, an "exited N" note, an overran or budget note, or the missing-tool note |
| 3 | MED | Nothing carried the full head SHA the review read to the step that advances the state; the brief printed 7 characters and the clone's head moves on the next fetch, so an advance could mark unreviewed commits as reviewed (Lens 5) | A data flow with no carrier | The tool was built before the skill decided where the SHA is used | The brief prints the full pinned head; the skill's P1 names it and P7 advances to it, in the record's commit |
| 4 | MED | The doc's reading guide read `renderMs` close to `avgMs` as "gated at Present", leaving out the render thread's own work; it named the wrong figure as stale; and the frame-limiter caveat rested on an override that never applies (Lens 2) | Engine claims written from the design summary | The native notes in the design were correct; the doc lost them in the restatement | Rewritten from the re-derived decompile; the native facts now sit, with build and address, in the engine-cost page |
| 5 | MED | `advance` accepted a record outside the repo (another drive crashed with exit 1) (Tooling) | Weak validation on a write path | Only the SHA format and the record's existence were checked | The record must be inside the repo; tests for both cases |
| 6 | LOW | Smaller tool defects: a 1.25 MB compare for two fields; rename detection fetching blobs in the blobless clone; a 404 on one repo reported as offline and the other skipped; a null `checked_at` stuck in the cache; exit 1 (which means "ahead") for an unexpected error; `re.match` with `$` accepting a newline; an empty repo list reporting clean; the cache rewritten on every run; the "Commits" column counting subjects; feature paths renamed upstream labelled "in scope" without a warning; an unused `--offline` (Lenses 3, 5, 6, Tooling) | Tool robustness | Written to pass the cases its tests named | Fixed with tests |
| 7 | LOW | `perf_runs.py` parsed `mainMs` and `renderMs` and nothing read them; the FRAME_CAP note judged a 1 fps match the engine never promised (Lenses 5, 6, Tooling) | A parse with no consumer; a judgment from an unmeasured rule | The parser was written before the report shape | Busy-time medians in the row and `compare`; the note prints the settings and judges nothing |
| 8 | LOW | Completeness: no feature doc or feature-map row for the process; the I3 and process issue drafts only in the scratchpad; stale field lists in the feature doc, the feature map, culture-doctrine.md and the MCM hint; no test pinning which adapter property lands in which `[PerfContext]` slot (Lens 4) | Docs and tests behind the code | The work list was the design's file list | Feature doc and row; drafts in the record; field lists and hint updated; the slot test |
| 9 | LOW | Skill: P6 to P8 restated `/ship`; P1 read the banner's 20-hour cache and did not treat exit 3 as unchecked; rules from AGENTS.md copied in; a hard-coded engine-source path; the unattended command unproven (Lenses 1, 5, 6) | A procedure that copies other procedures | Written from the first run's steps | P5 hands the build and review to `/ship`; P1 runs a live check; rules linked; the daily run marked UNVERIFIED with the steps to prove it |
| 10 | LOW | Convergence check: finding 1's fix was incomplete. The deadline bounded only the commands before the UPSTREAM block; the worktree, stash, tag, `git log`, `git diff` and TODO commands ran after it, and the stated "1.5 s under" margin left out the 0.5 s kill grace | A bound placed in the middle of the sequence it bounds | The fix timed the block itself, and case 7l runs in a sandbox that is not a git repo, so the tail costs nothing there | The block runs last, so its deadline bounds the whole hook; the hook comment and the catalog state the 9 s end; the misc.md lesson says to run the bounded check last |
| 11 | LOW | Convergence check, smaller items: three doc-review fixes misapplied or half applied (the provenance row, the caravan wording, one "withdrawn"); a clause duplicated in the record; the brief's Range line counted non-merge commits under the plain word "commits" while the banner counts merges too (a wave-1 finding no decision covered); the `compare` table misaligned by the new 16-character metric name; full mode's exit 3 untested; no exit 2 branch in the skill; a test name that contradicted its assertion; two docstrings; the no-latch reason overstated vanilla's call (it runs only during a performance analysis) | Fixes applied in bulk from lists, without a re-read of each result | The doc-review edits were placed by approximate line numbers, and the fix lists did not carry every wave-1 finding | Each fixed; tests now pin the table width and the full-mode exit 3. A separate convergence pass after the fix round found them |
| 12 | MED | Three commit gates (`check-claude-files-tracked.sh`, `check-moduledata-validation.sh`, `check-doc-config-drift.sh`) ran `cd "$CLAUDE_PROJECT_DIR"`, which stays at the main checkout after a session enters a worktree. Another session's untracked skill there denied every commit of this worktree, even after `EnterWorktree`, and the other two would have checked the main checkout's staged files, never the worktree's | A gate judging the wrong repository | Written when every commit ran in the main checkout; their tests used one directory as both the commit's repository and the project root | A shared resolver (a `git -C` or earlier `cd`, then the payload's `cwd`, then the project root), first `taom_commit_repo_dir`, replaced after review by `_pybin.sh` `taom_git_repo_dir` (findings 16 and 17), used by every git gate; `tools/test_hooks.sh` 7b covers each source and pins the three gates; harness-facts row; lesson in misc.md |
| 13 | HIGH (pre-existing, found through upstream data) | The troop-weight penalty deflated the garrison size limit: vanilla desertion then trimmed a quarter of the excess a day while lords refilled to the unweighted limit, and `troop-weight-system.md` said vanilla never enforces that limit | A penalty on a shared engine limit applied to a party kind it was never meant for | The 2026-07-11 rework checked TAOM's own readers of the limit, not the engine's per party kind; the shed's leaderless guard hid it | `TroopWeightService.ApplyPartySizeWeightPenalty` returns for a garrison before it caches or subtracts (test first: `DisplayFrameSourceTests.WeightPenalty_Garrison_ReturnsBeforeItCachesOrSubtracts`); doc corrected; lesson in gamemodels-services.md |
| 14 | MED | The garrison fix exempted the penalty only: the capacity displays (the Manage garrison party screen, the clan screen's garrison rows) kept showing the weighted count over the now unweighted limit, "600/400" for a garrison 100 under its cap (Lenses 2, 5) | A fix to enforcement without its display twin | The fix was written against the defect's description (desertion), and the display hook's rule "the two frames are the same cap" holds only for a party that pays | One predicate, `TroopWeightPenaltyScope.PaysSizePenalty`, read by the penalty and both display frames; `DisplayFrameSourceTests` pins all three |
| 15 | MED (pre-existing) | Patrol parties paid the penalty too: their limit is `10 + 5 x guard house level`, nothing sheds or deserts them, they refill at full size, so an elite patrol sat over its deflated limit for good and under vanilla's over-limit speed factor (Lens 2) | A penalty on a shared limit applied to a party kind it was never meant for | The garrison fix looked at the reported kind only, not at vanilla's other settlement-derived branch beside it (`DefaultPartySizeLimitModel.cs:86-93`) | The predicate exempts `IsPatrolParty`; the gamemodels-services lesson lists the base model's party kinds |
| 16 | HIGH | The new resolver handed the gates a subdirectory when the cwd, a `cd` or a `-C` named one; every gate reads root-relative paths (`git ls-files` pathspecs, `tools/*.py`), so all three passed in silence, which the old `cd "$CLAUDE_PROJECT_DIR"` never did (Lenses 1, 5, Tooling) | A fix that widened the input set without re-checking each consumer | Every 7b case ran at a repository root; the #690 lesson (test a gate from a subdirectory) was not re-read | `taom_git_repo_dir` resolves every candidate with `git rev-parse --show-toplevel`; 7b runs the gates from subdirectories |
| 17 | MED | The resolver was a second, weaker copy of the subject gate's: one unbalanced quote (an apostrophe in a heredoc message) emptied its tokens and dropped the `cd`; it took the `-C` of another git command; it joined a relative path to the cwd, not to an earlier `cd` (Lenses 1, 5, Tooling) | Two answers to one question, the newer written without reading the older | The sibling hook's parser was not read before writing the new one (hook-authoring.md "mirror the sibling") | The subject gate's parser moved into `_shellwords.py` (`git_invocations`, mode `git-repo`), with a running base, subshell scope, PowerShell directory commands and partial tokens; the subject gate imports it; `test_shellwords.py` covers each shape |
| 18 | MED | The subject gate and the broad git add confirm still read the project root: a worktree commit's label was judged on the main checkout's `<Version>`, and the confirm listed the main checkout's dirty files (Lens 5, Tooling) | The same defect class in gates outside the first fix | The first fix covered the gates named in the failing case only | Both use `taom_git_repo_dir`; 7b covers a worktree cwd for each |
| 19 | LOW | The two tool gates would have run another repository's `tools/*.py` when the command named that repository (Tooling) | A gate executing code from a directory it does not own | Widening the repository a gate reads also widened the code it runs | `taom_is_project_repo` (the same git common directory as the project) before the tools run; 7b proves a foreign stub never runs |
| 20 | LOW | 7l did not prove the time bound after the block moved (the sandbox's earlier blocks finish at once), and the comment said the block ends the hook by 9 s whatever came before (Tooling) | A test whose fixture cannot reach the hard case | The sandbox has no repository and no graphify tool | 7l adds a run after a graphify sibling that spends 3.9 s, asserting a budget under 5 s, the note last and the hook back inside 10 s; the comment states the block's own bound |
| 21 | LOW | Smaller items: the lesson's party-kind list; "no hang" attributed to notes that do not say it, and the shipped default (every 20th cover) left out; two labels for the 20.6 k figure; the record's tense for a line this round corrected; the `PerformanceAnalyzer` caller; a test reading an index before its -1 check and an assertion that could not fail; a redundant `FrameWindow` constructor; the skill's exit 1 when one repo is ahead and another unchecked; "his" for yotthani in unchanged lines (Lenses 1, 2, Tooling) | Restatement drift and leftovers | Edits made from findings without re-reading the source line | Each fixed |

## Root-cause pattern

Three findings (1, 2, 4) are the same shape as the 2026-10-15 RCA's pattern: a statement or a bound written about another
component without reading that component under its hardest case. The time budget assumed the hook's other checks were
free; the reading guide summarised the decompile instead of quoting it. The preventive rules exist
(`hook-authoring.md` "measure the SLOW path", `evidence-over-claims.md`); the new `test_hooks.sh` case makes the first
one a gate for this block.

## Rejected proposals

- **A latch on the per-frame reads** (Lens 3): the read is a native field load and a divide; vanilla's own
  `PerformanceAnalyzer.Tick` (v1.5.5 `PerformanceAnalyzer.cs:130`) calls the same getters unguarded on every tick of a
  performance analysis (while `Start` has set its current object, `:94-103`); both are registered on the client and the
  dedicated server; and the behaviour has no seam to test a latch.
- **An overall deadline inside the tool only, or a cache-only startup read** (Lens 6): the first cannot see what the hook's
  other checks spent; the second is silent on a stale "current" and needs a background refresh whose survival is
  unverified. The hook budget plus the tool's `--budget` covers both.

## Lessons

Three lessons in `lessons/misc.md` (their natural home, `lessons/build-tooling-workflow.md`, has another session's
uncommitted edits in the main checkout): "A check inside a shared hook registration gets the time that is left, not a
bound of its own", "A tool that writes a tracked file validates every value it stores" and "A hook that judges a
commit reads the commit's own repository, never `$CLAUDE_PROJECT_DIR` alone". One in `lessons/gamemodels-services.md`:
"A penalty on a shared engine limit reaches every party kind and every reader of that limit".

## Open after the push (2026-10-16)

The maintainer asked to commit and push while the second review's wave 2 was still being applied. These items stay
open, each with what was found; none is HIGH (the subdirectory HIGH, finding 16, is fixed):

- **Fixed before the push:** lens 6 A1 and lens 3 finding 1 (the shared resolver tokenized the whole command, and shlex
  is quadratic in a word's length: on a 1 MB command three gates ran past their registrations and failed open). The
  resolver now reads at most `REPO_SCAN_MAX` (256 KB) of command text after the heredocs are out, and judges a longer
  command in the payload's cwd; `test_shellwords.py` times it. Lens 6 A2 (the subject gate's dead `repo` variable) is
  deleted.
- **Lens 4 M1:** `TroopWeightPenaltyScope.PaysSizePenalty` is pinned by source words only; an inverted predicate passes.
  Add a RequiresGame behaviour test (the `RefugeDamageHooksTests` bare-party pattern) and assert the `!` in the display test.
- **Lens 4 M2:** stale guidance that the gates judge the main checkout: `docs/ai-includes/git-and-commits.md:18-24` and
  `/improve` (`SKILL.md:41`, `references/execute-and-review.md:157-165`, `run-protocol.md:228`, `dispatch-rules.md:93-95`).
- **Lens 4 M3:** the five changed gates are proven by `tools/test_hooks.sh` only; the harness runs the main checkout's
  copies, so the live proof (a worktree commit allowed despite another session's untracked skill in the main checkout;
  `cd <worktree>/Main && git commit -m "no label"` denied; both from the PowerShell tool too) is owed once the main
  checkout carries this change.
- **Lens 6 F1 and F2:** a wrapper before git (`env X=1`, `command`, `timeout 60`) hides a commit from the subject gate
  and an add from the broad-staging confirm; the confirm decides with a bash parser and lists with the shared one.
- **Lens 6 A3:** one payload builder for the 7b tests. **Lens 3** findings 2 and 3: run `taom_is_project_repo` after
  the staged-file filter in the two tool gates (CPU only), and `$PWD` for `$(pwd)` in one deny message.
- **Lens 4 L1-L8:** this RCA's top-line counts, `validate-push.sh` outside the shared resolver, the catalog's "ends by
  9 s", troop-weight doc rows (Tests section, feature map, GameModel registry), the x N row-tag decision written down,
  untested hook paths (the ModuleData foreign-repo guard, `pushd`/`sl`, `cd --`, an empty PYBIN), `--git-dir` and
  `GIT_DIR=` forms undocumented, and the bandit-party check (no weighted troop in caravan, villager or bandit templates
  today).
- **Lens 3 (Efficiency)** measured the change's cost at 170 to 340 ms per commit (all gates in parallel) and accepted it.
- **Not changed, by reason:** `docs/INDEX.md:277` and `Main/_Module/THIRD-PARTY-LICENSES.txt:10` still say "his" for
  yotthani (INDEX.md has another session's uncommitted edits in the main checkout); the `.claude/settings.json`
  SessionStart entry needs the maintainer's explicit request.
