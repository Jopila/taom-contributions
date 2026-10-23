# RCA: B7 slice 1, campaign map visuals to TAOM.Client and the server JIT ratchet (ADR-013, #968), 2026-10-23

**Summary.** Slice 1 moves `SupplyRouteVisualService`, `CampVisualService`, `RefugeVisualService` and
`CampLayoutBuilder` (with `CampLayoutMath`) into `TAOM.Client.dll`. Core keeps the interfaces and registers
`NullSupplyRouteVisualService`, `NullCampVisualService` and `NullRefugeVisualService`; `TaomClientComposition`
replaces them on a player's PC. It also lands gate 3, `ServerJitGateTests`: an IL-token scan of every
`TAOM.dll` method against a checked-in `ServerJitBaseline.txt` (385 entries) that may only shrink. Risk tier 1
(DI composition for seams that campaign ticks consume), one wave: Codex (gpt-6-astra, ultra) plus the Data
flow, Engine compatibility and Tooling lenses. No CRITICAL or HIGH. The DI timing was confirmed: no consumer
resolves a camp, refuge or supply route graph before `ClientComposition.Apply` (`Main/IoC.cs:225`). The
Engine compatibility lens confirmed the gate's premise with JIT probes on .NET Framework 4.8.1 and CoreCLR
10: a method whose IL names a type from an absent assembly fails when it is compiled, not at `GetTypes`.
Three MEDIUM and about ten LOW items, all in the new gate, its tests or text, were confirmed and fixed in the
same commit.

Sources: Codex output `docs/reviews/raw/codex-adversarial-b7-slice1-map-visuals-2026-10-23.md` (local,
ignored), prompt `docs/reviews/codex-adversarial-b7-slice1-map-visuals-2026-10-23.prompt.md`; plan
`E:\Temp\claude\handoffs\b7-slice1-plan.md` (local).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | A client type only in a call-site signature (an `__arglist` call, `calli`) escaped the walker | Gate blind spot | The walker reads the resolved method's declared signature; `InlineSig` was skipped | Both shapes fail closed: the walker reports a marker name, so the method is an offender; a fixture for each, both seen RED first |
| 2 | MEDIUM | Three detectors (generic method arguments, constructor signature, locals) had no fixture that failed when removed | Test cannot fail | Fixtures hit several detectors at once (`u == null` calls `Uri.op_Equality`) | One fixture per detector; each of 16 walker branches short-circuited once, the fixture test RED every time |
| 3 | MEDIUM | "The baseline only shrinks" had no test: a new offender plus a hand-added line passed, and the writer rewrote the whole set when the file was missing | Ratchet unenforced | The plan limited the no-file branch to slice 1, the code kept it | `MaxBaselineEntries = 385` asserted; the writer fails without the file and only prunes |
| 4 | LOW | An async lambda's state machine has no `[CompilerGenerated]`, so its key did not fold into the declaring method | Key folding bug | No async-lambda fixture | Nested types named `<...` fold too; `AsyncLambdaOwner` fixture |
| 5 | LOW | The scan count was a 90 percent floor and unreadable bodies were ignored by two tests | Weak pin | Floor chosen from the plan | `scanned` equals the MethodDef row count exactly (29,552 at review, 29,562 after the rebase); every core scan asserts no unreadable body |
| 6 | LOW | `NullMapVisualServicesTests` built `Vec2` without `RequiresGame`; two map-visual classes lacked the Inconclusive guard | Test environment | Siblings had the same gap | Category, `default(Vec2)` and the guard added |
| 7 | LOW | The composition test checked types, not that the service and its teardown callers share one singleton | Weak pin | | `AreSame` on the `_visuals` fields |
| 8 | LOW | A missing code root passed silently in `test_prefab_asset_packages.py` | Vacuous pass | `rglob` on a missing folder returns nothing | The scan raises on a missing root; a temp-tree test covers both roots |
| 9 | LOW | Text: the null refuge "costs one call" (it also computes the party position); "mark it [ClientOnly]" before the attribute exists; "the server's JIT would fail them" without its condition; the gate's under-approximation undocumented | Wording | | Reworded; one sentence in `ServerJitScan` names the limit and the gates that close it today |

Not changed, recorded (pre-existing, outside the diff, UNVERIFIED): `IoC.Configure` runs
`RefugeIoC.InitializePatchStatics` in every process, and writing `RefugeClanScreenPatch`'s static field runs
its static constructor, which names `ClanPartiesVM` (a client-only assembly). On a dedicated server that would
throw `TypeInitializationException` at boot. The same shape exists at game over through
`PartyNameplateCampIconPatch.Reset`. Both keys are in the baseline; the owed server boot settles it, and B6 or
slice 2 owns the fix.

## Rebase onto B6

B6c (`8960a0c24`) landed first and extracted the same walker as `IlClientReferences`, with `calli`
reported as a marker name. The rebase keeps that one walker: `ServerJitScan` and `DependenciesClientIlTests`
call it, slice 1's copy is deleted, and the vararg check joins it as a second marker
(`UndecodedVarargCallSite`; removing it fails `Offenders_VarargCallSite_FailsClosed`). The gate then met two
upstream changes on its first run: B6c moved a client reference from `ManualPatchApplicator::ApplyAll` into
its guarded `ApplyCaptainTooltip` helper, and the equip-preset fix (`89e7af6c0`) added
`InventoryVMAdapter::RefreshPanel` while the quick-actions fix removed `InventoryItemAdapter::get_IsEquipped`.
The baseline swaps those two keys and stays at 385 entries.

## Why the reviewers found these

- Codex found #1 by compiling fixtures in memory against the scanner and reading `offenders=0`.
- Tooling found #2, #5 and #8 by removing each detector or root alone and asking which test still failed.
- Data flow found #3, #6, #7 and the pre-existing boot hazard by tracing from the baseline file and from
  `IoC.Configure` to each assert or static constructor.
- Engine compatibility found the wording in #9 by running JIT probes with an assembly removed.

## Lesson

A fixture that several detectors hit proves none of them. Give each detector a fixture that only it can see,
and remove each detector once to watch its fixture fail (`docs/reviews/lessons/testing-qa.md`).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/testing-qa.md](lessons/testing-qa.md)

<!-- backlinks-end -->
