# Plan review: 044, round 1

Reviewer: cold plan reviewer (read-only), 2026-10-28. Plan: `plans/044-nogame-unit-step-green.md`.
Code read at `370b4bf2b24bf3edf4881a2613500ded15090a75` in the plan worktree. No earlier review file
for this plan existed in this folder, so nothing is carried over.

Verdict: one blocking item (the count oracles). Everything else is non-blocking polish. With B1
fixed, a weak executor can run the plan.

## Blocking

**B1. Every count oracle is an absolute total from `370b4bf2b`, but trunk has already moved, and
the plan is a handoff to another session.** Lines 22-25, 298, 364, 378, 465-466, 487, 581, 583
and the STOP at 599 hard-code 27,952 / 0 / 7 (Install) and 23,334 / 23,343 / 23,335 / 23,337
(RefAsm). `bannerlord-1.5.x` is at `0f418a90c`, two commits past the base. `f7a6368e5` adds or
changes four test files (`CareerAbilityRegistryWiringTests.cs` +67 lines among them), and its body
records "C# 27,957 passed, 0 failed, 7 skipped". An executor started from current trunk therefore
hits the Step 1 STOP (line 599) before making any edit. Even with that STOP waived, the Step 6 and
Done-criteria totals would be wrong. The drift check (line 8) covers only the in-scope and
excerpted files, so it cannot catch a change in the test count. `PROGRESS.md` line 13 says the plan
is written "so that another session can complete", so a later base is the expected case, not an
edge case.
Fix: make the oracles relative to the executor's own Step 1 and Step 2 runs. Install: Failed 0, and
Step 7's Passed, Skipped and Total equal Step 1's. RefAsm after the edits: Failed 0, Skipped equal
to Step 2's, Total = Step 2 Total minus 6 (minus 7 with the Step 4 fallback), Passed = Step 2
Passed plus 1 (plus 0 with the fallback). Keep the 370b4bf2b numbers as reference values only.
The Step 1 STOP becomes "Install Failed is not 0, or the gate does not pass all it runs", and the
Step 2 STOP stays name-based ("the failures are not exactly these 7 names"). Whether the new
`CareerAbilityRegistryWiringTests` passes on the stubs is UNVERIFIED: if it does not, the name-based
STOP correctly fires.

## Non-blocking

1. Line 70: "commit `8589a3384` added 22 that way". `git show 8589a3384` adds 13
   `[TestCategory("RequiresGame")]` lines and 2 `[TestCategory("RequiresGameIL")]` lines (15 in
   all), none with a trailing comment. The style point holds; the count is wrong.
2. Line 47: `TAOM.Tests.csproj:50-63`. The stamp `ItemGroup` is lines 55-65 (the comment is 47-54).
   The quoted content is lines 57-63, so the excerpt matches but the range does not.
3. Lines 80 and 115: "(69 lines)" and "(46 lines)". `wc -l` and the last numbered line give 68
   for `CoopMissionOwnershipTests.cs` and 45 for `EnlistmentHourlyTickTests.cs`.
4. Lines 253-258 tell the executor to re-run `graphify_taom.py affected` after a refresh and quote it
   in the report, but no Step and no Done criterion runs it. A step-by-step executor will skip it.
   Add it to Step 1 or Step 10.
5. Lines 304 and 423-424: "the RefAsm unit step with `--filter ...`" and "RefAsm run with `--filter
   ...`". The unit-step command already carries a `--filter`. Spell out the full command so a weak
   executor replaces the filter rather than passing two of them.
6. Step 7, lines 483-484 ("all 7 formerly failing tests are among the passed"), and the "no ...
   result appears" checks in Steps 3 and 5 (lines 401-402, 455-456) need passed and run test names.
   Default `dotnet test` output prints only failures and totals. Only Step 4 (line 426) mentions
   `--logger "console;verbosity=normal"`; put it on these runs too, or state the expected per-filter
   totals (Install four-class subset: the four classes' full test count, 0 failed).
7. Step 1, lines 365-368, gives two outcomes for the standalone Patch109 run: it fails with a
   `FileNotFoundException` skip list, or it passes. It does not cover a different failure (another
   exception type, or another Patch109 test failing). Step 5, lines 451-452, then adds the guard
   with a rewritten comment "otherwise", which may not fix the failure. Add: "any other standalone
   failure is a STOP".
8. STOP at lines 607-608 ("The RefAsm build stamps something other than `refasm-game` ...") gives
   no command that shows the stamp or the `GameAssemblies` diagnostics. Name the check (for
   example, the `Inconclusive` text of a guarded test, or reading the assembly metadata) or drop it.
9. Drift check (line 8) omits files whose text the plan quotes or relies on:
   `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionAuthorityTests.cs` (exemplar, line 71),
   `TAOM.Tests/Features/ManyHammers/ManyHammersWiringTests.cs` (lines 216-226),
   `.claude/rules/tests.md` (lines 61-66) and `.ai/verification.md` (line 307).
10. Done criteria: line 591 ("re-checked against the code") is not machine-checkable (acceptable
    under the template's draft-text lesson, but it is the one judgment line). Nothing in Done
    criteria checks the Install four-class subset (Step 7, line 483), which is the proof that the
    tags removed nothing.
11. Step 9 script (lines 527-538): `dotnet build ... | tail -3 && dotnet test ...` uses the exit status
    of `tail`, not of the build, so a failed build still runs `--no-build` tests. The "No totals line
    ... NOT RUN" rule (line 542) catches that, but `set -o pipefail` or a separate build check would
    be cleaner. `git ls-files --others` and `cp --parents` take paths relative to the current
    directory, so the section should say it runs from the repository root. Step 9's verify (line
    551) "only `.claude/skills/verify/SKILL.md` changed by this step" is a judgment after Steps 3-8.
    Make it `git diff --stat -- .claude/skills/verify/SKILL.md` plus a cheap
    `python -B -m unittest tools.tests.test_claude_config_lint`, which lints every SKILL.md's
    frontmatter (`tools/tests/test_claude_config_lint.py:115-167`).
12. Lines 20-21 record that the baseline ran with `BANNERLORD_GAME_DIR` set, but no step tells the
    executor whether to set it for the Install runs. Say so explicitly, or state that the Install
    stamp fallback makes it irrelevant (UNVERIFIED whether the skip count changes without it).
13. Step 8 keeps the rewritten bullet under "## Owed" (line 492), but after this plan nothing in it is
    owed. Say whether it stays there as a record or moves, so the executor does not guess.

## Excerpts checked against `370b4bf2b` (match)

- `CoopMissionOwnershipTests.cs:14-17` (`[TestClass]` 14, class 15, `BareAgent` 17).
- `CoopMissionOwnership.cs:32-36` (`IsOwnedHere`).
- `EnlistmentBehavior.cs:16` (`: CampaignBehaviorBase`).
- `EnlistmentHourlyTickTests.cs` (`[TestClass]` 16, class 17, Setup 23-33).
- `CampActivitySettingsTests.cs:171-184` (`[TestMethod]` 171, lines 174-177 replaced; `Read` at
  144; `System.Reflection` at line 2).
- `CampState.cs:24-39` (ids 101 to 111).
- `Patch109WiringTests.cs:50-73` (using at line 12, `GameAssemblies` at 106, `[TestMethod]` 52,
  method 53).
- `ManyHammersWiringTests.cs:122-128`.
- `PatchCategoryIndex.cs` (`GetFromType` at 64, inside the try).
- `GameAssemblies.cs` (`Diagnostics` 41, `EnsureLoaded` 47).
- The `tests.md:95-115` quote.
- The three RefAsm commands in `.ai/verification.md:25-31`.
- `v1.5.6-impact.md` ("## Owed" at 411, bullet 436-449, quote 439-443).
- `verify/SKILL.md` (Step 2d at 84, Step 3 at 97, `API snapshot:` at 140; Steps 2c and 2d read
  the test bin).

Engine claims checked with `pwsh tools/taom-src.ps1 path`:

- `Agent.cs:14` declaration.
- `CampaignBehaviorBase.cs:12-15`.
- `SaveableFieldAttribute` (one `short` ctor argument).
- `DotNetObject` has a static constructor (line 46).

History checked:

- `c8d69a0dc` is "remove the GitHub Actions workflows".
- `8589a3384` added the ManyHammers method tag.
- `block-dangerous-git.sh:149` asks on `git worktree remove --force`.
- The `toolmisuse-rm-rf-root` regex (`audit_claude_config.py:540`) does not match the Step 2e
  `rm -rf "$wt"`.

Gates checked:

- At the base, the Done-criteria stale grep (line 585) prints only `v1.5.6-impact.md:440-441`.
- The `SaveableFieldAttribute` grep (line 586) matches only `CampActivitySettingsTests.cs:175`.
- The `git grep -c` (line 588) prints nothing (exit 1).
- So all three can fail.
- Arithmetic: 23,343 minus 6 is 23,337, and 23,334 plus 1 is 23,335.
- Suggested subject: 62 characters, with `v2.0.36` matching `SubModule.xml:6`.

Other checks:

- No em or en dash in the plan.
- No secret values.
- No local absolute path.
- No worktree path or branch name for the executor.
- No CHANGELOG step.
- Every dotnet command carries `-p:DisableModuleCopy=true -p:ModuleId=`.
- Protected and single-owner files are out of scope (lines 323-327).
- Step 0 is a maintainer choice with an executor check (line 286).
- The issue is named (line 28).
- Binding ADRs are named (lines 262-270).
- TDD: no new tests; the RED is the Step 2 RefAsm run, and the Step 4 mutation proves the rewrite.

UNVERIFIED (not run, read-only):

- The Install and RefAsm totals.
- That `GetCustomAttributesData()` succeeds on the stubs (Step 4.3 has the fallback).
- The Step 0 timing figures.
