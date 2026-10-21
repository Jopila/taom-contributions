# RCA: lords.xslt generator data loss (#959), 2026-10-21

**Top line.** `tools/complete_lords_xslt.py --apply` rebuilt `Main/_Module/ModuleData/lords.xslt` from vanilla `lords.xml`
plus the few parts it parsed back, so every hand-owned part was lost on a run: 389 `<Equipments>` blocks, 19 `race`
attributes (9 nazghul, 6 uruk, 3 orc and Sauron), five hand-laid skills or Traits blocks in three lords, one lord's
attribute order, one explicit `is_female="false"`, the BOM and the final newline. Nothing ran `--apply` automatically,
so no shipped data was lost. After a first repair that carried each part through, review showed the rebuild shape
itself was the defect: the maintainer chose a new contract. `--apply` now never rewrites a byte already in the file
and only inserts templates for vanilla lords the file lacks. On the shipped file it is a byte-for-byte no-op.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (latent) | `--apply` dropped every hand-owned part of `lords.xslt` | Generator owns less than the file | The tools that wrote Equipments were retired and later hand edits (race, layout) were never fed back; #758 and #957 each pinned only the block they added | New contract: insert only; byte-comparison tests on the shipped file |
| 2 | MED | The first repair proved itself with `git hash-object`, which cannot see line endings under `core.autocrlf` | Proof method | The filtered hash looked like a byte check | Proof is a byte comparison; lesson updated |
| 3 | MED | The refusal guard added by the first repair saw only deletions; an inserted vanilla `is_female="true"` would have changed a lord's sex silently | Guard design | A positional line diff was taken as a complete check | Removed with the rebuild; `--apply` no longer touches existing templates |
| 4 | MED | A new vanilla lord with a `skill_template` got vanilla's rows beside it, which the #626 skill gate rejects | Cross-tool rule | The skills fix did not read the #626 gate | Rows only when vanilla names no template; test runs the gate's own scan |
| 5 | MED | Seven code mutations of the generated template survived the tests (no id, no passthrough, no Traits, no skill_template) | Test strength | Tests asserted presence of new lords, not the lines that make a lord load | Template-line test plus an lxml end-to-end test; all mutations but one cosmetic order change are killed |
| 6 | LOW | `--dry-run` and `--apply` parsed templates differently (a self-closing template swallowed the next one) | Two parsers | The rewrite kept the old CSV parser | One parser for every mode |
| 7 | LOW | A non-canonical `match` spelling counted as missing, so `--apply` would add a second template | Parser breadth | Only the generator's own spelling was recognised | Any `match` naming the quoted id counts as present |
| 8 | LOW | Insertion relied on exact blank-line counts | Text layout assumption | The shipped layout is uniform | Bounded search with blank-line backtracking; tests for both odd shapes |
| 9 | LOW | An empty vanilla file read as success | Silent clean | No input-size check | `ValueError` naming the path |
| 10 | LOW | Docs: stale changelog line, README precision, lesson facts (the old writer lost the BOM and final newline, not CRLF) | Docs | Written from memory of the plan | Corrected against the code and a re-run of the old generator |

## Root-cause pattern

A generator that rebuilds a whole file must own every byte of it, and this file had long since gained content no tool
owned. Patching the rebuild to carry each new kind of hand content (findings 1 to 4) kept failing in new ways. The fix
that holds is to stop rebuilding: insert only what is missing and never rewrite what is there.

## Why each agent missed these

- **The first builder** carried each lost part through and proved the result with a filtered hash, so findings 2 to 4
  shipped in the first repair.
- **The Tooling lens** found the escaping, write-path, line-ending and skills gaps (F1 to F10) and the hash-object blind spot.
- **Lens 3 (Efficiency)** found that the positional guard refused a template that only moved.
- **Lens 4 (Completeness)** found the stale issue numbers, the 12 lords whose sex an insertion could flip, and the lesson facts.
- **Lens 5 (Data flow)** found the skill-gate conflict (finding 4) and the insertion blind spot (finding 3).
- **Lens 6 (Design)** proposed the insert-only contract that the maintainer chose.
- **The convergence pass** found the surviving mutations (finding 5) and findings 6 to 10.

## What proves it

- `--apply` on the shipped file writes nothing; a byte comparison with `HEAD` (worktree line endings) is equal, 1,151,095 bytes.
- `tools/tests/test_complete_lords_xslt.py`: 33 tests, OK; nine hand-data shapes survive `--apply` byte for byte;
  a BOM and CRLF copy missing two lords gets exactly those two inserted.
- `python -m unittest discover -s tools/tests -t .`: 4586 tests, OK (8 skipped). C# suite: 22370 passed, 0 failed, 5 skipped.

## Feedback memories to codify

None beyond the lesson in `docs/reviews/lessons/build-tooling-workflow.md` (#959).
