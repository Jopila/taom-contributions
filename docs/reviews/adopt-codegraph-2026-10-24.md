# CodeGraph adoption review (2026-10-24)

Review of [colbymchenry/codegraph](https://github.com/colbymchenry/codegraph) against TAOM's AI
harness, through `/adopt-external`. Mike asked what TAOM could improve from it, and on 2026-10-24
chose to build every item useful to the repo in the long term, whatever its tier. Issue
[#995](https://github.com/haterade22/TAOM/issues/995).

## Coverage

- **Repository:** `main` at `69af19ac` (pushed 2026-10-24). MIT, about 75,000 stars, created
  2026-01-18, 504 open issues.
- **Read raw, through `gh api`:** `src/telemetry/index.ts`, `install.ps1`, `package.json`. Read
  through the GitHub pages: the README, the `src/` and `src/resolution/` listings. Nothing was
  cloned, installed or run.

## What CodeGraph is

A local code-knowledge graph for coding agents: a tree-sitter parser (40-odd languages, C# listed
as full support) writes symbols, call edges and imports into a SQLite database with full-text
search at `.codegraph/codegraph.db` inside the scanned project. A file-watcher daemon re-indexes
after a 2 s debounce, and an MCP server exposes one default tool, `codegraph_explore`, which
returns the source of the relevant symbols, the call paths between them and a blast-radius summary.
The CLI adds `affected`, which maps changed files to the test files that import them.

TAOM already runs the same kind of tool: graphify, behind `tools/graphify_taom.py`
([ADR-012](../adrs/012-graphify-code-graph-in-the-workflow.md),
[feature doc](../features/graphify-code-graph.md)). So the question was what CodeGraph does that
graphify, Serena and the wrapper do not.

## Security

**Safe to learn from: yes. Safe to install: no.** Each point was checked in the raw file.

| Finding | Where |
|---|---|
| Telemetry is on by default: `{ enabled: true, decidedBy: 'default' }` unless `DO_NOT_TRACK`, `CODEGRAPH_TELEMETRY=0` or a stored choice says otherwise. It posts to `https://telemetry.getcodegraph.com/v1/events` with a random machine id kept in `~/.codegraph/telemetry.json`, plus version, OS, language names and usage counts. The README's "no data leaves your machine" contradicts this | `src/telemetry/index.ts:30`, `:231`, `:251`, `:267` |
| The Windows installer is meant to run as `irm <url> \| iex`; it downloads a release zip and checks no hash or signature | `install.ps1:6`; no `Get-FileHash` or signature call in the file |
| `codegraph install` writes a marked section into CLAUDE.md and AGENTS.md, an MCP config, and Claude Code auto-allow permissions | README, "Installation" |
| The index lives inside the scanned repo (`.codegraph/`) | README, "Storage" |
| No `preinstall`, `postinstall` or `prepare` script; one `preuninstall` | `package.json:26` |

## Measurements on TAOM's own graph

Taken before any change, on the graph built 2026-10-23 from `12afef4d`.

| Query | Result |
|---|---|
| `affected "FiefSiegeParticipationService" --depth 2` | Its own test file only |
| `affected "Main/Features/FiefGranting/IFiefSiegeParticipationService.cs" --depth 2` | The production callers `TaomSettlementClaimantDecision.cs:L53` and `FiefGrantFactsBuilder.cs:L22`, the class itself through `implements`, and five test files |
| `affected "IFiefSiegeParticipationService"` | `No unique node match`, with no candidates |
| `status` | STALE: 645 code files and 88 commits since the build, one day old. Every query printed the whole list |
| Classes in `Main` and `Client` whose first base is a TAOM interface | 705 (a grep of `class X : I...`) |
| Full `dotnet test TAOM.Tests` without deploy | 123 s wall time, 1 m 42 s test time, 25,930 tests |

The first two rows are the finding that mattered. `/deep-review` Step 1 and `/investigate` Phase 1
run `affected` on the changed class, and TAOM resolves services through interfaces, so the blast
radius they reported for a service class left out every production caller. CodeGraph's README
names this case ("interface→impl" hops that grep cannot follow).

## Decisions

| # | Item | Decision |
|---|---|---|
| A1 | `affected` follows a class to the TAOM interfaces and base types it implements and adds their dependents | Built (#995) |
| A2 | A failed name lookup lists candidates and retries once on a single defining file | Built (#995) |
| A3 | A stale query names first the stale files that the answer itself cites (CodeGraph prepends a warning naming a pending file an answer references) | Built (#995) |
| A4 | A `tests` verb: from changed types or files to one `dotnet test --filter` line (CodeGraph's `affected`) | Built (#995). Worth it because the full suite takes about two minutes |
| A5 | The graph refreshes itself: `refresh --detach`, started by SessionStart when the graph is stale (CodeGraph's watcher, without a daemon) | Built (#995). Skipped while Bannerlord runs; `TAOM_GRAPHIFY_AUTOREFRESH=0` turns it off |
| K1 | CodeGraph's A/B benchmark recipe | Recorded in the deferred D1 row of [adopt-harness2-2026-10-21.md](adopt-harness2-2026-10-21.md) |
| X1 | Install CodeGraph, its MCP server or its watcher daemon | Declined: the security findings above, an index inside the repo, and it duplicates graphify |
| X2 | `codegraph_explore` (source plus call paths in one call) | Declined: Read plus `explain` and `affected` answer the same question, and dumping source costs context |
| X3 | Framework routes, Swift and React Native bridges, full-text search | Declined: out of domain, or Serena and Grep cover it |
| X4 | A measured trial of CodeGraph's C# resolution | Declined: A1 closes the gap the measurement found, at less cost |

## What was built

All in `tools/graphify_taom.py` and `.claude/hooks/session-start.sh`, branch
`taom-995-graphify-codegraph-ideas`; usage in the [feature doc](../features/graphify-code-graph.md).

- **Interface and base-type callers.** `affected` reads `graph.json` once, runs `affected` on each
  TAOM supertype of the target and prints the extra lines under `Via <IFoo> (<path>):`.
- **Member callers.** Building A1 found a second, larger gap: graphify's `affected` never walks
  from a type to its member methods, so a caller that only calls `service.HasRecord()` is missing
  from every query, the interface path included. The graph does hold those edges (`method` links
  from the type to its members, `calls` links into them), so the wrapper prints
  `Member callers of <Type>:` from the loaded graph. For `FiefSiegeParticipationService` this
  added `FiefGrantingCampaignBehavior` (five call sites, `:L58` to `:L135`), the service's main
  production user, which no query had listed before, and `FiefGrantFactsBuilder` (`:L39`, `:L40`).
- **Candidates, answer-scoped staleness, `tests`, `refresh --detach`** as decided above. The
  printed filter for `FiefSiegeParticipationService` named five test classes and ran 100 tests in
  under a second, against about two minutes for the full suite.

| Check (2026-10-24) | Result |
|---|---|
| `python -m unittest discover -s tools/tests -p "test_graphify_taom.py"` | 82 tests pass (38 before). The first 33 new tests were seen failing before the code; the two log-cap tests were written after the cap |
| `affected "FiefSiegeParticipationService" --depth 2` on the refreshed graph | Lists `TaomSettlementClaimantDecision`, `FiefGrantFactsBuilder` and `FiefGrantingCampaignBehavior`; about 5 s, against about 3 s before |
| The filter `tests FiefSiegeParticipationService` printed | 100 passed, 0 failed |
| SessionStart hook, opt-out path | 2.7 s in total, against 3.1 to 3.8 s for the unchanged hook; the start path adds about 1 s (`tasklist`) |
| `python tools/audit_claude_config.py` | HIGH 1, MED 1, INFO 6, the same older findings as the main checkout |
| `python tools/lint_docs.py --fail-on-drift` | exit 0 |

**Accepted limits.** A class with a supertype costs one more graphify call (about 3 s each, since
graphify re-reads the 150 MB graph). Calls through a lambda or a string-keyed registration are not
links in the graph, so the `tests` filter is a lower bound and the full suite stays the run quoted
before review. A background refresh takes about 150 s on a quiet machine (284 s measured beside a
full test run), and it is skipped while Bannerlord runs.
