# RCA: B4b, campaign-map types to TAOM.Client (ADR-013, #968), 2026-10-22

**Summary.** B4b moves six campaign-map types (three MapViews, the Fiefs navigation element, the Hall of
Fire conversation provider, the settlement plate widget) and three helpers into `TAOM.Client.dll`, behind
five core seams with null versions. It is risk tier 1 (a Harmony patch, DI timing, campaign ticks), so it
had one review wave under the tiered rule: Codex (gpt-6-astra, ultra) plus the Data flow, Engine
compatibility and Tooling lenses. No CRITICAL or HIGH was found, so no second wave ran. One MEDIUM from
Codex, two MEDIUM from Tooling and several LOW items were confirmed against the code and fixed in the same
commit. The repeat pattern from B3 and B4a held again: a check scoped to the old layout (one assembly) was
not widened when code left that assembly.

Sources: Codex output `docs/reviews/raw/codex-adversarial-taom-client-b4b-2026-10-22.md` (local, ignored),
prompt `docs/reviews/codex-adversarial-taom-client-b4b-2026-10-22.prompt.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Three IL safety gates (`InformationManagerClearBanTests`, `PartyOwnerGetterBanTests`, `SettlementEncounterInvariantTests`) scanned only TAOM.dll; a banned call in a moved MapView was never seen | Gate scope | The plan's gate triage searched for source paths, not for scanners given one assembly | `TaomAssemblies.All` (core and client) used by every TAOM-wide safety scan; `TaomAssembliesTests` proves a client method is enumerated; two more gates widened |
| 2 | MEDIUM | Three test classes loaded client types that need SandBox.View or CustomBattle without `GameAssemblies.EnsureLoaded()`, so they passed only after an earlier class loaded them | Test isolation | The full suite hides load-order dependence | `[ClassInitialize]` loader on each; `RequiresGame` tag |
| 3 | LOW | `snapshot_api_surface.ps1` skipped the client slot fill silently without TAOM.Client.dll, writing a wrong row and passing `-Check` | Silent fallback | The fill was written as best effort | It now throws |
| 4 | LOW | The null field camp overlay registration and the two post-Apply statics calls for FieldCamp and NameplateRelation were pinned only by text that a comment or a deletion would not fail | Weak pin | Tests called the methods themselves | A container test, and the `IoC.Configure` gate pins all five statics calls by name |
| 5 | LOW | A lesson said an empty profiler view scan logged no error; Harmony rejects the category and an error is logged when the profiler is on | Engine claim | Written from the plan, not from Harmony's code | Lesson and plan corrected |
| 6 | LOW | Stale comments and one research row (Hall of Fire phase, B7 note) | Drift | Moves change what comments describe | Fixed |

Not changed, recorded: `snapshot_api_surface.ps1 -Check` cannot exit 0 on the committed state because of
the backlinks footer and a stale `CoopRecruitAttributionPatch` row (pre-existing; a follow-up docs commit).
The profiler's tick listener walker counts TAOM listeners by TAOM.dll only; no client listener exists yet
(note for B4c and B5). The FieldCamp overlay reaches its behavior through a static, set after
`ClientComposition.Apply`, only so B4b needs no `Main/SubModule.cs` hunk while B4c runs in parallel; the
constructor route can replace it after B4c lands.

## Why the reviewers found or missed these

- Codex found #1 by building a counterexample in a moved method and following the scanner's assembly loop.
- Tooling found #2 by loading the client types under .NET Framework with no resolver, and #3 by running the
  script against a bin without the client DLL.
- Data flow found #4 by asking which test fails if each production line is deleted.
- Engine compatibility found #5 by decompiling Harmony's `PatchClassProcessor`.

## Lesson

When types move to another assembly, search the test tree for every scanner given one assembly
(`typeof(SubModule).Assembly`, `IlCallScanner`), not only for source paths. Repeat of the B3 and B4a class
(`docs/reviews/lessons/build-tooling-workflow.md`, the 2026-10-22 entries).
