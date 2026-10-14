# RCA: Bannerlord v1.5.5 engine bump (deep review, 2026-10-14)

## Top line

The v1.5.5 bump (`/engine-bump`, branch `chore/engine-bump-v1.5.5`, first commit `4f9985852`) went
through an eight-lens `/deep-review` (Opus 5.5, two waves of four) after its first commit. No finding
touched player-facing behaviour: a fresh build, the binding gate and the member body diff were clean.
The review confirmed three HIGH findings in the two new bump tools, two MEDIUM findings about a
dead TAOM.Dependencies guard and its false documentation, and a set of LOW precision items. All are
fixed in the follow-up commit. The theme: the bump's checks, and the tools written for it, reported
"0 of what I read" as clean, and a guard counted as working because it installed.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH | `check_engine_member_refs.ps1` exited 0 when a `-EngineDir` or `-Baseline` folder was mistyped or empty: it dropped the folder, skipped every reference into the assemblies it no longer indexed, and a shrunken baseline turned a real NEW miss into PRE-EXISTING. A same-build baseline also passed | Tool fails open on unread input | The tool was tested on correct inputs only; its negative control proved "no false alarm", never "no false clean" | Exit 2 on a missing or empty folder, an unindexed engine assembly (`UNCHECKED`), a non-.NET DLL or a same-version baseline; PRE-EXISTING only when the baseline holds the assembly; a test per case on Roslyn-built fixtures. Lesson in `lessons/build-tooling-workflow.md` |
| F2 | HIGH | The same tool keyed engine types by name alone, so a type that moved between assemblies without a forwarder passed, though the CLR throws `TypeLoadException` | Identity without scope | A member check was taken to cover type identity | Keys are `assembly|type`; type references are checked too; generic-type parents are resolved instead of skipped |
| F3 | HIGH | `engine_body_diff.py` exited 0 with rows it never compared: a missing build folder, a mistyped `--taom-src`, or both sides from the same build all ended "unresolved" or "same" with exit 0 | Tool fails open on unread input | Exit code tied only to `unparsed`; no check that the two sides differ | All four folders required; `BuildInfo.GameVersion` read and compared; exit 1 on any unresolved row; stale `.diff` output cleared |
| F4 | MED | `SubModuleConstructionGuard` could never catch anything: `MBSubModuleBase`'s constructor is 7 bytes with no fields, C# runs a derived SubModule's field initialisers before the base call, and `Install()` ran from `OnSubModuleLoad`, after `Module.LoadSubModules` had constructed every SubModule. The diag log shows 526 installs and 0 swallows since 2026-05-27. The bump first removed only its `AddSubModule` site and kept the other on a false premise | "Installed" taken for "effective" | The 2026-05-27 work accepted "installed on 2 sites" as proof; the bump repeated the premise without reading the IL or the call order | Guard deleted (maintainer decision); lesson "a Harmony guard needs proof it can see the failure it targets" |
| F5 | MED | Comments and docs described the dead guard as protection: `CoopPresence.cs` "KNOWN GAP", `dr3-maintenance.md`, the impact doc, the skill | Doc claim beyond the code | Same as F4 | Rewritten against the installed v1.5.5 `Module` code |
| F6 | MED | The two new test modules were pytest-style, so `/verify`'s unittest runner ran none of them, and the ratchet `test_no_new_tool_test_module_needs_pytest` failed | Test runner mismatch | The implementer ran `pytest`, which passes; `/verify` runs `python -m unittest discover` since CI was removed | Both rewritten as `unittest.TestCase`; the ratchet passes |
| F7 | MED | `check_handbook_attributes.py` never compared a committed manifest with the dump in dump mode, so the v1.5.4 manifest (stale helper lists) passed | Gate blind spot | `MANIFEST_STALE` existed only in manifest-only mode | Dump mode now reports `MANIFEST_STALE`; a repo test ties `dump_root_name` to the default dump |
| F8 | MED | The first body-diff run reported 509 of 521 rows changed: the installed Coop module bundles 40 v1.5.4 engine DLLs, and `decompile_bannerlord.ps1` decompiles every module's bin, so `_modules_build` held a second, stale copy of every engine type | Third-party engine copies | No tool distinguished a vanilla module from a mod that ships engine DLLs | The body diff reads engine assemblies only from vanilla modules; lesson appended; `taom-src` has the same exposure (follow-up below) |
| F9 | LOW | Precision: per-version numbers copied into the skill; a hard-coded `if version == "v1.5.5"` in a pin test; impact-doc counts from before the snapshot regeneration (139/52 against 144/53); stale "v1.5.2"/"v1.5.3" labels in the snapshot README, the handbook tool and `.ai/review-reference.md`; "allied party" where the code says "not at war with the player" | Edit precision | First-pass text written from memory of the run, not re-read against the outputs | All corrected; per-version data lives in pin rows and the impact doc |

## Root-cause pattern: a check that reports what it read, not what it was asked to read

F1, F2, F3 and F7 share one shape: each tool or gate counted the inputs it managed to read and
reported success on those, while a missing, mistyped or stale input silently shrank the work. F4 is
the same shape at run time: a guard's install log was read as proof that it worked. The fix in every
case is to make the check fail on what it could not read or cannot reach, and to prove a guard by
the path of the failure, not by its registration.

## Why each lens caught what it did

- **Standards (1)** and **Engine compatibility (2)** each proved F4 from the installed IL, independently; lens 2 also corrected five engine statements.
- **Data flow (5)** traced F4 through `CoopPresence`'s consumers, demonstrated F1 with real runs, and found the stale `.diff` output.
- **XML (7)** cleared every manifest and stylesheet against v1.5.5 and found the manifest gap behind F7.
- **Completeness (4)** found the missing lessons, issue and commit-body correction.
- **Design (6)** found F6 and proposed the type-reference, generic-parent and wildcard improvements.
- **Tooling** reproduced F1 to F3 and F7 with prototypes and tests that failed first.
- **Efficiency (3)** measured four output-identical speed-ups (about 42 s to 10 s).

## Follow-ups (not in this change)

- `tools/taom-src.ps1` treats any module whose bin holds a `TaleWorlds.*.dll` as engine, so Coop's
  bundled v1.5.4 copies win some lookups; `~/.taom-src/dll-index.json` already routes 23 types there.
  Same rule as the body diff: count only official modules. Environment clean-up (the index entries
  and their cache files) is the maintainer's.
- `tools/decompile_bannerlord.ps1` pipes ilspycmd through the console, so its output encoding depends
  on the host (the v1.5.3/v1.5.4 trees lost non-ASCII letters, v1.5.5 did not).
- `tools/snapshot_api_surface.ps1` records only the first target of a `TargetMethods()` patch.
- The pytest ratchet misses a module with column-0 `def test_` that never imports pytest.
- Nine TAOM GUI brush names resolve to nothing in the v1.5.5 install (pre-existing, not from this bump).

## Feedback to codify

Three lessons appended to `docs/reviews/lessons/build-tooling-workflow.md`: engine types only from
vanilla modules; "installed" is not "effective" for a guard; a check fails on inputs it could not
read. No new rule file: `evidence-over-claims.md` already says to prove a result before claiming it,
and the misses were in applying it to tools and guards.
