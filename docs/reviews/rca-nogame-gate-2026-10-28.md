# RCA: the no-game binding gate and its RefAsm stamp (#1038 item 14, 2026-10-28)

## Top-line

The no-game binding gate in `.ai/verification.md` failed 509 of 593 tests as documented, because
a RefAsm build stamped an empty `TaomGameFolder` and `GameAssemblies` found no game. The fix
stamps the stub `refasm-game` folder and the build's `TaomGameRefs` mode, and a RefAsm test DLL
now loads only that folder, whatever `BANNERLORD_GAME_DIR` says (maintainer decisions
2026-10-28: keep the stub stamp, the stamp wins). Twenty-two tests that ran engine code but
skipped through an `EnsureLoaded` guard carry `RequiresGame`, and two `BindingVerification`
tests that run engine constructors carry `RequiresGameIL`.

`/deep-review` ran at tier 2 (build targets, gates and tests): Data flow and XML (MSBuild) in one
wave, then one convergence pass on the fixes. Codex was skipped on the maintainer's word. The two
lenses found no gap and five LOW items, all fixed here, plus one process point the maintainer
decided.

Final runs on the fix, with `BANNERLORD_GAME_DIR` set to the install:

- RefAsm build 0 errors; the binding gate 591 of 591; the unit step 7 failed, 23,334 passed,
  2 skipped, the same 7 as with both variables unset (they are the failures plan 044 hands off).
- Install: strict binding gate 818 of 818; full suite 27,952 passed, 0 failed, 7 skipped.
- An incremental build in either direction regenerates both stamps and swaps
  `TaleWorlds.Library.dll` between the stub (129,536 bytes) and the real DLL (274,784 bytes).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `BuiltGameFolder_TestAssemblyAsCompiled_CarriesTheTaomGameFolderMetadata` asserted only `IsNotNull`, so the empty RefAsm stamp passed it, and the prototype's second test could not catch swapped csproj conditions once a RefAsm build had written `refasm-game` (the folder survives Clean) | A test that passes against the trivial value | deep-review-008 F2 ruled a non-empty assert a false positive, because plan 010 wanted an empty stamp; the prototype added a test beside the weak one instead of replacing it | One test now asserts the `TaomGameRefs` mode and that only a RefAsm build stamps its own `refasm-game` folder; shown red (3 of 10 failed) before the logic. Repeat of the #1040 pattern in `lessons/testing-qa.md` ("shown red once against a constant stub") |
| 2 | LOW | `.ai/verification.md` said a no-game run "cannot execute" the `RequiresGame`, `RequiresGameIL` and `LiveInstall` tests; the unit step now runs four tests tagged only `RequiresGameIL`, and the gate runs 86 `BindingVerification` tests tagged `RequiresGame`. `GameAssemblies.cs` comments still named "CI without a game install" as the case that resolves nothing and said the stamp could be empty | Doc rot from a behaviour change | The prototype rewrote the sentences it changed, not the unchanged sentences beside them that described the old behaviour | Rewritten. The engine-bump record rule added in the same session (rewrite the whole section when one of its facts changes) is the general form |
| 3 | LOW | Four tests outside `BindingVerification` carry only `RequiresGameIL`, which no unit-step command acts on; a future vanilla-IL ban test tagged that way would pass on the stubs' throwing bodies | Tag contract gap | `tests.md` scoped `RequiresGameIL` to `BindingVerification` and nothing enforced it; before the stamp, `EnsureLoaded` returning false hid the gap | `tests.md`: `RequiresGameIL` removes a test only from the gate, so a test outside `BindingVerification` that reads vanilla IL or data carries `RequiresGame` |
| 4 | LOW | #1038 item 14 and the v1.5.6 impact record named `ManyHammersWiringTests` as the ninth no-game failure; it was `TelcharsMark.Patch109WiringTests`, whose method has the same name | Record error | The failure list printed method names only, and the class was filled in from memory of an earlier report | Corrected in the impact record and the #1038 item 14 comment; failure lists now come with class names (`trx_transitions.py`, the stack frame). `evidence-over-claims.md` C2: a missing value is a stop sign, not a guess |
| 5 | LOW | The `GameReferences.targets` comment and the snapshot README said a set variable still wins over the stamp | Doc rot from a decision | The precedence decision came after the docs were written | Rewritten with the decision |
| P | process | The stamp reverses plan 010's designed empty stamp and deep-review-008 F2's ruling | Decision ownership | The prototype solved the gate without reading the decision records | The Data flow lens traced the records; the maintainer decided on 2026-10-28 (keep the stamp; the stamp wins); this RCA and the commit body name the superseded records |

## Root-cause pattern

Findings 1, 2 and 5 share one shape: a decision changed (an empty stamp became a stub stamp, then
the stamp outranked the variables), and the text and tests written for the old decision stayed.
The weak test is the dangerous one, because it stayed green. The general rule is already in the
session's lessons: a test of a value is shown red against the trivial value, and a record is
rewritten as a whole when one of its facts changes.

## Why each lens caught or missed these

- **Data flow (Agent 5):** found 2, 3 and 4, and the process point, by tracing the stamp to every
  consumer and reading the decision records. It found 1 as well (T7).
- **XML (Agent 7, run on MSBuild):** found 1, with the evidence that `refasm-game` survives Clean,
  and proved the two-condition `_Parameter2` shape and the incremental regeneration from the SDK's
  own targets.
- **Standards, Engine compatibility, Efficiency, Completeness, Design:** skipped by tier 2; no
  CRITICAL or HIGH called for a second wave.

## Superseded records

- `plans/010-ci-on-hosted-windows.md:298-302`, `:806-810` and `:1196-1199` (the empty RefAsm stamp
  and the checks that expect it).
- `docs/reviews/deep-review-008-binding-gate-no-silent-skips-2026-09-24.md:68` (F2, a non-empty
  assert ruled a false positive).
- D46's follow-up in `docs/reviews/deep-review-010-ci-on-hosted-windows-2026-09-24.md:275-279`
  (tagging the failing tests was a new decision for the maintainer; he made it on 2026-10-28).

## Owed

- FOLLOW-UP #1038: the 7 no-game unit failures and an optional `/verify` row for the RefAsm unit
  step, handed off as plan 044.
