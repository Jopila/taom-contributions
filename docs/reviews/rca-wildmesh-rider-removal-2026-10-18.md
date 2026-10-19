# RCA: WildMesh test rider removal before v2.0.35 (deep review, 2026-10-18)

## Top-line

`/deep-review` of the removal of the five CustomGame-only WildMesh test riders (#794), done so `/release` pre-flight 5
(`git grep -l taom_test_ -- Main/_Module` prints nothing) passes for v2.0.35. Scope: the deleted
`Main/_Module/ModuleData/test_riders/wildmesh.xml`, its `SubModule.xml` block, the deleted
`tools/tests/test_wildmesh_test_riders.py`, two comment strings in `tools/apply_wildmesh_armory.py`, one test comment
and eight docs. Lenses on `deep-reviewer` (Opus 5.5, max effort): wave 1 Engine compatibility, Data flow, XML,
Completeness; wave 2 Design, Tooling; then one convergence pass. Standards and Efficiency were NOT IN SCOPE (no C#,
no harness file, no code added).

**No defect in the removal hunks.** Engine verified that an NPCCharacters file loads only when a `SubModule.xml`
XmlNode or `project.mbproj` names it, and that no save or Custom Battle preset can hold a removed id. XML ran five
gates and two structural checks, all green; the validator before and after differs by exactly the five characters.

**One HIGH, outside the repo:** the testing channel already carries a package labelled v2.0.35, built at 18:27 from
`6553b8c70` with the riders still registered, so testers can pick five `[Test]` riders in Custom Battle. All four
wave-1 lenses found it independently. It repeats the #646 lesson about test riders reaching players.

## Mike's decisions

| # | Question | Answer | Applied as |
|---|---|---|---|
| D1 | Version, with the testing channel already on an untagged v2.0.35 of `6553b8c70` | "v2.0.35 (Recommended)" | Release stays v2.0.35 |
| D2 | The same, once the lenses showed that package ships the riders | "Keep v2.0.35 (Recommended)" | Recorded in the ledger and, with the release commit, in `release-process.md`; the testing package is replaced from the tag build |

## Findings

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | HIGH | The testing channel's v2.0.35 package ships and registers the five test riders | Engine, Data flow, XML, Completeness | Release path | Pre-flight 5 greps the repo only. The riders were deleted "before a release", and a channel package made outside `/release` is a release nobody gated | `tools/package_release.py` refuses a package whose ModuleData defines a `taom_test_` id, dry runs included (next commit); D2 |
| 2 | LOW | Seven statements still said the riders exist: `file-catalogue.md`, `feature-map.md`, the ledger (fixed before the lenses), then `moduledata-validation.md`, the writer's docstring and `ITEMS_COMMENT`, and a test comment | the author's own sweep, Data flow, Completeness, Tooling | Moved fact | The sweep grepped the deleted path and the ids, not the phrase "test riders". **Repeat:** the same day's lesson "A state change is a moved fact: grep the old state's phrases" | Grep the old state's own words; no new rule, the lesson stands |
| 3 | LOW | The ledger called the installed file inert while the installed `SubModule.xml` still registered it | Engine, Completeness | Premature claim | The end state was written as the present state | Fixed: "once this tree is deployed", plus the nine worktrees that still carry the block |
| 4 | LOW | The restore steps omitted the deploy, the restart and the cleanup, used a staging restore, and routed through `./build.ps1`, which ships every session's uncommitted code | Completeness, Engine, Design | Procedure | The quadruped workflow's own copy-not-build rule (stage 11) was not re-read | Replaced by an install-only restore (Design KEEP, applied) with a deploy-before-packaging clause |
| 5 | LOW | "Last present at `6553b8c70`" was off by one commit; the author first wrote `d13e91658` from memory | Data flow, Engine, Completeness | Fabrication risk | A commit id was written before it was proven | Fixed to "restorable from `6553b8c70`", which `git cat-file -e` proves |

## Root-cause pattern

Findings 1 and 4 share one cause: the install is the source of every channel package, and nothing gates what the
install holds. Pre-flight 5 checks the repo, the Phase 8 prune cleans the install only for `/release` itself, and a
tester package can be cut from the install at any time. Test data that is safe in the repo becomes player-facing the
moment it is deployed and packaged. The gate therefore belongs in the packager, which every package passes.

## Why each agent missed these

- Nothing was missed by the lenses: the HIGH sits outside the diff, and the four wave-1 lenses each reached it from
  the pre-flight state named in the scope notes.
- The author's first pass missed findings 2 to 5; the lenses caught all of them before the commit.

## Follow-ups (not applied: pre-existing code outside this change; no issue filed, issue creation needs Mike's word)

- A class-wide test that every file defining `<NPCCharacter>` is in `tools/schemas/taom_npccharacter.json`
  `applies_to` (Tooling F2).
- `.claude/skills/release/SKILL.md` step 5 still mentions ladder and recruitment exemptions that `test_riders/`
  riders never had.
- `TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs:15` and 13 translation-cache files still describe or hold
  the Animalia riders removed in `d85fe6dc3`.
- The `SubModule.xml:NNN` line numbers in `docs/modding/file-catalogue.md` have been stale since 2026-09-05.
- `check-moduledata-validation.sh` validates the working tree, not the index, so a commit of the staged deletions
  alone would pass it with a dangling registration; this commit stages them together.

## Feedback memories to codify

None. The lessons entry in `docs/reviews/lessons/build-tooling-workflow.md` carries the one new rule.
