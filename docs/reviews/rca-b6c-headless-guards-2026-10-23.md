# RCA: ADR-013 B6c, the startup hooks' dedicated-server guards (#968), 2026-10-23

**Summary.** Sub-batch 6c puts every client-only call in `OnSubModuleLoad` and `OnGameInitializationFinished`
behind `if (!ClientComposition.IsDedicatedServer())`, inside a `[MethodImpl(MethodImplOptions.NoInlining)]` helper a
server never calls, and splits `ManualPatchApplicator.ApplyAll` the same way. Risk tier 1: Codex (ultra) and the
Data flow and Engine compatibility lenses, one wave. No reviewer found a production defect, and nobody found a
CRITICAL or HIGH. The confirmed findings are gaps in the new test gates, one misleading diagnostic and three
comments that gave the wrong reason. Each gate fix was proven by a mutation that turned exactly its test red. The
report move changed runtime order in a tier 1 area, so the fixes had one re-review (Codex at high and the Data flow
lens). That pass found two more LOW gaps in the finding 2 fix, now closed in IL. The
Data flow lens found the same local-function gap, a lost space, one test still named "Absent", two imprecise
wordings and no test pinning the report's new position; all are fixed, and
`ServerAssemblyReport_IsFirstInTheOncePerProcessBlock` turned red when the call was moved back below `ApplyAll`.

Sources: `docs/reviews/codex-prereview-b6c-headless-guards-2026-10-23.prompt.md` and
`docs/reviews/codex-fixreview-b6c-headless-guards-2026-10-23.prompt.md` (outputs under the ignored
`docs/reviews/raw/`); the build plan `b6-build-plan.md` (session scratchpad, ec2ae0bd).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The shared IL walk (`IlCallScanner.ExtractReferencedMembers`, used through `IlClientReferences`) decodes method, field, type and token operands but skips `InlineSig`, so a type named only in a `calli` signature (a C# function pointer; `Main` allows unsafe code) passed C3 and C4 while the JIT still resolves it | Gate blind spot | The walk was built for call sites; nothing listed which operand kinds it leaves out, and no fixture used a stand-alone signature | `ClientReferences` reports a sentinel for any body holding a `calli`, so the gate fails closed; `IlClientReferencesTests` emits one |
| 2 | LOW | C4 matched only the guarded call (`if (!...) ApplyVisualPatches(harmony);` once), so a second, unguarded call passed; and it walked only `ApplyAll`, not the lambdas the compiler moves into nested types. The first fix (a regex total count, a nested-type walk) still missed a named-argument call and a non-capturing local function, which compiles onto the class itself (fix re-review) | Gate blind spot (repeat) | A source test written for the shape the author wrote; C3 already walked nested types, C4 did not copy it | Count call instructions to each helper in IL (exactly one, from `ApplyAll`); walk every method of the class and its nested types except the helpers and their generated methods; mutations for a duplicate, a named argument, a lambda and a local function each turn one test red |
| 3 | LOW | Nothing called `ServerAssemblyReport.Log`, so its "never throws" contract had no test | Missing test | The tests covered the pure `Describe` only | Two `Log` tests: a logger that throws on every level, and a healthy one |
| 4 | LOW | The server report said "absent" for an assembly that is only not loaded yet, and ran after about 400 lines a server runs for the first time, so a throw there would lose the line on the first boot that needs it | Diagnostic wording and placement | Written to answer the plan's question ("which assemblies are resolvable"), which a loaded-assemblies list cannot answer | "not loaded"; the call is now the first statement of the once-per-process block |
| 5 | LOW | The game-init comments (Patch60, `ManualPatchApplicator`) said a server "cannot load" these assemblies at boot. A logged Coop-staged server boot ran the same code at game init without a failure, because the stage loads the view assemblies by then: those guards skip UI-only patches, while G5 and G6 at `OnSubModuleLoad` are the boot-fatal ones | Prediction stated as fact | The comments copied the plan's predicted "fatal" rows (14 to 16) before any boot tested them | Comments reworded; the commit body and the #968 comment state which guards are boot-fatal |
| 6 | LOW | The Patch79 comment called both probe targets Gauntlet views; one is the map-bar view model `MapInfoItemVM` | Wrong comment | Written from the category name | Reworded |

**Not fixed here, by design (Data flow F1, LOW).** After 6a merges, `SubModule` reads the dedicated-server flag
three ways: the `ClientComposition.IsDedicatedServer` seam (the 6c guards), `IDedicatedServerProvider` (the mission
guard), and 6a's `IsDedicatedServer()` helper (the index build). All three read the same build constant, so nothing
differs at runtime. The handoff told 6c to use the seam so the two sessions do not write the same helper; the merge
picks one spelling.

## Root-cause pattern

Findings 1 and 2 are the same blind spot: a gate checks the shapes its author thought of. The IL walk decodes the
operand kinds a call site uses, and the source test matched the guarded spelling only. A gate proven red by one
mutation is still blind to the forms that mutation does not exercise. Findings 4 and 5 are the other half: text
written from a plan's predictions (a crash, "resolvable") states as fact what only a boot can show.

## Why each reviewer missed or caught these

- **Codex** caught 1 to 3 by compiling real C# in memory against a synthetic `SandBox.View` assembly and running
  the scanner and the source predicates on mutated input.
- **Data flow lens** caught 4 by tracing what reads the report line (the plan's UNVERIFIED row) against what the
  line can know.
- **Engine compatibility lens** caught 5 and 6 from a logged server boot of byte-identical code
  (`E:\CoopDS\probe-boot-C2.out` and its TAOM log) and the probe's own IL. It judged finding 1 not worth a change
  because no TAOM method uses a function pointer today; Codex's fail-closed fix costs four lines, so it was applied.
- Lenses 1, 3, 4 and 6 did not run (tier 1 runs Codex plus lenses 5 and 2).

## Lesson

Appended to `docs/reviews/lessons/testing-qa.md`: an IL gate fails closed on an operand kind it does not decode, and
a guard's source test counts every call of the guarded member, not only the guarded spelling. This repeats the
B3 lesson "A regex scan sees one spelling" for IL and for call counts.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/testing-qa.md](lessons/testing-qa.md)

<!-- backlinks-end -->
