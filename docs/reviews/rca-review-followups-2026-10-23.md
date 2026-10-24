# RCA: review follow-ups #983 and #984 (deep-review, 2026-10-23)

## Top-line

Two tool changes from earlier reviews were built in parallel and reviewed together: #983 made
`tools/audit_mbproj_registration.py` parse `project.mbproj` as the engine does and model both of its loaders; #984 added a
`lint_docs.py` gate that a newly written uppercase `FOLLOW-UP` line in an RCA must cite an issue or a reason. `/deep-review`
ran seven lenses (Standards, Engine, Data flow, Tooling; then Efficiency, Completeness, Design; two stopped at the session
limit and were resumed) and a convergence pass. Two HIGH findings, both fixed before the commit:

- **#983 reopened its own founding incident.** The new rule accepted any row whose `type` appeared in native's table of
  type names (41 entries, plus the types vanilla's own file ships). Native loads rows by type for only 13 of them, never
  `monster`, `action_set`, `action_type` or `monster_usage_set`. Run on the spider-era Armory file, the four rows behind
  the 2026-06 DivideByZero crash passed; the old tool had caught them.
- **#984's gate did not run where it mattered.** The commit hook runs the linter only when a staged path is on its
  trigger list, and `docs/reviews/` was not on it, so an RCA-only commit (9 of the last 60 RCA commits) was never checked.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The native-type exemption used native's whole type-name table instead of the 13 types its loader requests, and unioned in vanilla's types; the spider-era and 2026-08-28 dead rows passed. | A fix that widens an accepted set (REPEAT: `lessons/testing-qa.md` "replay the incident a check names, in its own on-disk layout"; "fixtures carry the shape every real input has"; `lessons/build-tooling-workflow.md` #947, "a fix that removes a false positive must keep what the old code caught") | Every DEAD-ID fixture had no `type`, while every real row has one; the builder decompiled the name table, not its callers; the lens checklist greps for removed checks and allowlist names, not a newly widened set. | The 13-type set, the vanilla fallback deleted, the six historical rows copied verbatim as a regression test, the backup replayed. Lesson. |
| 2 | HIGH | The commit hook's trigger list lacked `docs/reviews/rca-*.md`, so the new gate never ran on an RCA-only commit. | Trigger list for a new check (REPEAT of the #647 review behind `test_hooks.sh` section 5d) | The builder wired the check into `--drift-only` and tested the linter, not the hook that decides whether the linter runs. | Trigger added; a hook test stages only an RCA; a 5d-style check that the trigger list covers RCA paths. |
| 3 | MED | The hook judged the working tree, not the commit: a fixed but unstaged line passed while a bare line was committed, and another session's untracked draft could block this commit. | Gate scope | The dash check's "new lines" helper (working tree against HEAD) was reused for a gating check. | `--staged` mode (`git diff --cached`, `git show :path`), used by the hook. |
| 4 | MED | The hook's deny message extracted only the config, version and budget sections, so an RCA failure came with no detail. | Message | The awk pattern named fixed sections. | Every `## ` section, `[size-warn]` lines dropped; a hook test. |
| 5 | MED | No way to exempt a line that only mentions the marker; 7 of 26 legacy lines are such mentions, among them the RCA that asked for the gate. | False positives in a blocking check | The dash check's inline-code exemption was not carried over. | Inline code and link targets stripped before matching; the doc says to backtick a mention. |
| 6 | MED | The audit's id list was described as the engine's requested set; it is a stand-in (vanilla Native's ids). | Engine claim | Not verified against the requesters. | Worded as a stand-in; the exact list is #987 item 1. |
| 7 | LOW | Matching edges: `#` in a link anchor, a hex colour, "finding #2", `C#7` counted as an issue; an issue URL and "no issue filed" did not; `FOLLOW-UPS:` missed. | Pattern | Tested on the shapes in the brief only. | Patterns and one test per shape. |
| 8 | LOW | Docs: four modding docs, the record doc's mutation claim, the linter feature doc, INDEX, doc-lookup, hooks catalog, the `/improve` run protocol, help text ("three checks", "The CI gate."). | Docs | Scoped to the tool and its README row. | All corrected. |
| 9 | LOW | Design: the `project.mbproj` parse repeated in two tools; the known-ids table assembled in `main()` and the tests separately; a copied fence walker; duplicated git test fixtures; action declarations read by regex (four live Armory declarations have `type` first). | Duplication | Built in parallel from separate briefs. | One shared reader, one lookup site, the shared walker, one test base, XML parsing of actions. |

Rejected: an INERT-ROW warning for non-`<file>` rows (TAOM_Map's nine inert `<Module>` rows would warn on every run of
shipped data). Deferred with an owner: #987 (exact requested-id list, "nothing audited" exit code, encoding edges, the
deployed TAOM copy, the dash scanner's fence walker, a mistyped `--dash-base`).

## Root-cause pattern: a gate verified on its own code, not on the case it exists for

Both HIGH findings passed every test the builders wrote. The audit was tested on fixtures without the attribute that
decided the verdict, and never on the incident file the repo already held. The lint gate was tested as a linter, never
through the hook that decides whether it runs. Each lesson involved was already written down; the builders were given
the issue text, not the incident and the consumer.

**Preventive action.** A brief for a gate names its founding incident file and its consumer (the hook, the skill step)
and asks for one test through each. The completeness and tooling lens checklists ask about a new or widened accepted set
or exemption, not only removed checks.

## Why each lens caught or missed what it did

- **Data flow (5)** found both HIGH findings by running the old and new tools on the snapshot and the real hook on a
  sandbox repo, plus the staged-versus-working-tree gap.
- **Tooling** reproduced both with the spider-era file from the asset mirror and gave the exact 13-type set from Ghidra.
- **Engine (2)** confirmed the 13 types from every native call site and the game's logs, resolved the four unnamed table
  entries, and found the exact requested-id set.
- **Standards (1)** found the missing exemption for passing mentions and the stale help and docs.
- **Efficiency (3)** found the over-wide git pathspec; no cost risk.
- **Completeness (4)** found the missing hook tests, the record doc's false mutation claim, the stale snapshot and nine
  modding docs.
- **Design (6)** gave the shared reader, the lookup site, the walker and the fixture shapes.

## Convergence pass

One deep-reviewer re-ran every check on its own scratch install and clones: the 13-type set with no vanilla-type union;
the 2026-08-28 replay (2 DEAD-IDs, exit 1); the live install (0 errors); the shared `mbproj_rows`; the hook on an
RCA-only staged commit (deny, `--staged`, every section, size warnings dropped); the index read; the snapshot
byte-identical to the live file; `test_hooks.sh` 1040 passed. It found two MEDIUM and four LOW, fixed except where noted:

| # | Sev | Finding | Fix |
|---|---|---|---|
| C1 | MED | The regression test pinned only half of finding 1: every case ran without a Native manifest carrying types, so re-adding the union with the installed file's types passed all tests (proved with that mutant). | A case whose Native rows carry `type="monster"` and `type="action_set"`, asserting the spider-era row is DEAD-ID; the record doc claims only what it pins. |
| C2 | MED | The stricter "no issue" pattern rejected reasoned phrasings the hook's own deny text invites ("No issue filed (public action).", "no issue filed here because ..."). | Words may stand between the phrase and a reason marker; a bare "no issue." still fails. |
| C3 | LOW | `--staged` judges the index against HEAD, which an `--amend` or a path commit does not record. | #987 item 7. |
| C4 | LOW | The deny message showed an empty report-only section, quoted one report-only kind as a fix, and the timeout advice dropped `--staged`. | Corrected; hook tests. |
| C5 | LOW | Seven doc statements (a coverage claim on TAOM's own sound row, `MBMusicManager` named as a merge requester, "a new RCA", a stale printed count, a call-site count, "CI", a line-ending claim). | Corrected. |
| C6 | LOW | A `python3` shebang in the new test file. | `python`. |

**Why C1 got through.** The fix wrote a test for the shape of the incident rows but ran it in a fixture without the
other half of the inputs the bug read (the vanilla manifest's types). This is finding 1's class one level down: a
fixture that lacks an attribute every real input has. The lesson below now names both inputs.
