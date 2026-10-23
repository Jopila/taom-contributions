# RCA: B5, UIExtenderEx extensions and Patch109 to TAOM.Client (ADR-013, #968), 2026-10-22

**Summary.** B5 moves every UIExtenderEx ViewModelMixin and PrefabExtension class, two widgets, the three
Patch109 postfixes and the co-op UI registration policy into `TAOM.Client.dll`. UIExtenderEx registration
moves from `Main/SubModule.cs` into `ClientSubModule.OnSubModuleLoad` under the same module name, and the
patch category index reads both assemblies. After B5, `TAOM.dll` holds no type that needs a client
assembly (milestone M1 in source; the server boot that proves it is owed). Risk tier 1, one wave: Codex
(gpt-6-astra, ultra) plus the Data flow, Engine compatibility and Tooling lenses. No CRITICAL or HIGH; no
engine incompatibility. One MEDIUM (Codex) and about twelve LOW items, all test strength or text, were
confirmed and fixed in the same commit.

Sources: Codex output `docs/reviews/raw/codex-adversarial-taom-client-b5-2026-10-22.md` (local, ignored),
prompt `docs/reviews/codex-adversarial-taom-client-b5-2026-10-22.prompt.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The registration test passed with the co-op branch reversed or suppression turned off | Test cannot fail | It counted calls in source text | A registration seam; four branch tests; both mutations seen RED |
| 2 | LOW | "Core registers no UIExtender" was a text search of one file, while UIExtenderEx refuses a second `Register` under one name | Weak pin | Text checks were the B3 pattern | IL check: `UIExtender.Create` has one caller, `ClientSubModule.OnSubModuleLoad` |
| 3 | LOW | `ClientAssemblies.PolicyOnly` was redundant and gave a false server message | Redundant gate | Added from the plan before the core gate was strict | Deleted; the core gate is strict |
| 4 | LOW | Two negative checks could pass while reading nothing without the game | Vacuous pass | Collectors swallow load errors | Inconclusive without the game; strict `GetTypes`; a positive control category |
| 5 | LOW | Dead allowlist, dead locator code, a speculative client sweep | Dead code | Left from earlier shapes | Removed |
| 6 | LOW | No assert on type-load failures in the patch binding scan; tripwire rows lost the qualified cases | Gate strength | | Asserts and rows added |
| 7 | LOW | A lesson and a test comment said a UIExtenderEx refusal leaves no log; ButterLib writes it to its mod log | Engine claim | The trace listener was not read | Corrected; the boot check now reads that mod log |
| 8 | LOW | Stale paths in career-system.md, the registry and TRACKING.md; the snapshot script wording | Drift | | Fixed; the script throws on types that fail to load |
| 9 | LOW | `string?` only on the client-called special-resource members | Inconsistent annotation | Annotated to fix one compile error | Every id parameter and null return annotated; no cascade |

Not changed, recorded: the career button's mixin click handler has no `[DataSourceMethod]` (removed in
`19201d793`), so UIExtenderEx never adds it; core Patch27 handles the click. Pre-existing; a follow-up issue (#971).
Corrected by #971 on 2026-10-23: the click was never silent, because the postfix's `ExecuteDone` runs
vanilla's close callback, which plays the same `event:/ui/default`; only the mixin's own copy was dead.

## Why the reviewers found these

- Codex found #1 by mutating the branch condition against the new test predicates.
- Tooling found #2 to #6 by asking, for each gate, what input makes it pass while the code is wrong.
- Engine compatibility found #7 by reading ButterLib's trace listener and today's mod log.
- Data flow found the career button by checking every prefab `Command.Click` against the methods
  UIExtenderEx actually adds.

## Lesson

A gate is only as strong as the mutation that would make it fail. For each new or moved gate, name one
wrong code change and confirm the gate goes red on it (repeat of the B4a and deploy-batch test findings;
`docs/reviews/lessons/testing-qa.md`).
