# RCA: Lord Courtesies (deep-review, 2026-10-20)

## Top line

`/deep-review` ran seven lenses in two waves on the uncommitted Lord Courtesies build (#897, worktree
`feat/lords-notables-prisoners`). It confirmed no HIGH, 11 MEDIUM and about 25 LOW findings, all fixed before the
commit, plus 4 behaviour-preserving simplifications. Two findings changed Mike's approved design (the gift reward,
the reveal), and he decided both on 2026-10-20. The suite went from 20,205 to 20,236 passing tests.

Two causes dominate. First, the plan was researched against the trunk of 2026-10-19 (`328ef9fc1`), and the build ran
on a trunk that had since merged Bonds of Battle's troop hand-over, the shared relation seam and Personal
Relations (`c34284a73`, `e6204df97`, `876e32af9`). The plan's "no TAOM code rewards troop gifts" was true when
written and false when built. Second, the reveal wrote an engine field (`Settlement.HasVisited`) that nothing in
v1.5.5 reads.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The reveal set `Settlement.HasVisited`, which no code in v1.5.5 reads, so "holdings marked on your map" did nothing visible. The same no-op was already merged in Hard Questions. | Dead engine field | Research checked that the setter exists and never searched for a reader. | Lesson in `lessons/adapters-taleworlds-api.md`. Fixed: the engine's visual tracker, towns and castles only. |
| 2 | MED | Troop gifts used their own tier formula although Bonds of Battle (merged meanwhile) already rewards troop hand-overs with a daily cap; two rules for one act, and the split-gift trick beat the per-gift cap. | Parallel-branch drift | The plan read the trunk of the day; the trunk moved before the build, and the build brief was not re-checked against it. | Lesson in `lessons/build-tooling-workflow.md`. Fixed (Mike): the gift pays through `TroopHandoverService.OnHandover`. |
| 3 | MED | The thanks line showed the asked gain while vanilla's notice showed the landed one (Charm scales positive changes). | Engine body | The relation call's scaling was not read. | Fixed by finding 2 (the seam returns what landed). |
| 4 | MED | A gift worked through a messenger conversation, moving troops into a party anywhere on the map. | Missing co-location rule | Vanilla's donate rule lives inline in two view models; the plan did not copy it, and Messengers opens a normal lord conversation at any distance. | Fixed: same settlement, or the joining radius and the same map event, and no battle simulation. |
| 5 | MED | The reward was paid on the screen's closed event, but the engine commits troops on other paths too (map-bar navigation, Talk). | Wrong settle point | The closed event looked like "Done". | Fixed: settle in the done delegate, which every commit reaches, against a moving snapshot. |
| 6 | MED | No exception guard in the party-screen delegates or the reveal consequence. | Robustness | Copied the happy path. | Fixed: try/catch with a log line. |
| 7 | MED | Tracked plates are drawn every map frame outside vanilla's cull and Patch104; a kingdom reveal marked about 97. | Per-frame cost | Decision 2 was taken from the feature's view, not the renderer's. | Fixed: towns and castles only (about 27 for a kingdom). Performance note in both docs. |
| 8 | MED | Docs misstated the four dropped parts and kept stale settings counts (252, 249). | Doc drift | The doc was written from the brief, not the issue comment; the count edits were made where the test pointed. | Fixed. |
| 9 | MED | The dialog behavior had no executed test; dropping its one stateful line left the suite green. | Test gap | Only source pins. | `LordCourtesiesBehaviorTests` (RequiresGame). |
| 10 | MED | GroupOrder 61 collides with trunk's newer Fell Foes group. | Parallel-branch drift | Same as 2. | Renumbered at the rebase. |
| 11 | MED | The relation owner differs under Personal Relations (the lord himself, not his clan leader); the doc said clan leader. | Parallel-branch drift | Same as 2. | Docs fixed; the code reads the model. |
| L | LOW | A gendered hint, a dead `Tier` field, a stateless DI singleton, a double stack fold, a double snapshot read, reply routing in the behavior, a filtered config test, a stray blank line after a removed row, no talk delegate, no room / no troops / disbanding / other map event gates, doc wording. | Hygiene | | Fixed. |

## Root-cause pattern: the trunk moves under a plan

Findings 2, 10 and 11, and the Hard Questions round 3 regression (`rca-hard-questions-2026-10-19.md`), share one
cause: a plan or a deletion proved against one trunk, built or kept on a later one that a parallel batch had
changed. Six batch sessions merged into `bannerlord-1.5.x` on 2026-10-19 and 2026-10-20. A plan's overlap research
is a snapshot; it has to be redone when the base moves.

## Why each lens caught what it did

- **Standards (1):** doc counts, the misstated dropped parts, reply routing, the GroupOrder comment.
- **Engine (2):** the dead `HasVisited` field, the missing exception guards, the double relation line, the talk path.
- **Data flow (5):** the messenger route, the commit paths, the split-gift cap, the relation seam rule.
- **XML (7):** the gendered hint.
- **Efficiency (3):** the per-frame cost of tracker marks.
- **Completeness (4):** the trunk's GroupOrder collision, the untested behavior, the Bonds of Battle doc drift.
- **Design (6):** the four simplifications, and confirmed no gate duplicates Bonds of Battle.

## Convergence pass

One reviewer checked the applied fixes and found one MEDIUM and four LOW defects, all fixed:

| # | Sev | Bug | Why missed | Fix |
|---|---|---|---|---|
| C1 | MED | Row 4 was only half fixed: when the lord's party is in a settlement, a messenger conversation still offered the gift, because Messengers moves the main party into that settlement (`EnterSettlementAction`), so "same settlement" passed. | The fix was proved for the field case only; no engine state distinguishes a messenger visit from a real one. | Messengers now owns `IMessengerConversationState.IsOpen` (set before the encounter starts, cleared on every end path and at session launch); the co-location check is false while it is open. |
| C2 | LOW | Talk was still listed as a commit path after talking was refused. | Stale comment. | Removed from three lists. |
| C3 | LOW | A contract comment still said one entry per character after the fold moved to the diff. | Stale comment. | Fixed. |
| C4 | LOW | The docs said a kingdom reveal marks "20 to 30"; the data says up to 27, most 4 to 16, and the checklists said "settlements" for towns and castles. | The number was estimated, not counted. | Fixed from the live map data. |
| C5 | LOW | No test pinned the done step's exception guard. | Guard added without a pin. | Source pin. |

## Follow-ups

- The paid translation of the `taom_lc_*` keys: backlog #763.
- In game: the commit-time settle, the tracker marks and their save and load, a gift in an army and in a town, the
  messenger refusal (see the feature doc's checklist).
