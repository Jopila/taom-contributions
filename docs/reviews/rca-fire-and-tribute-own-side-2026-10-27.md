# RCA: Fire and Tribute own side, Auto-Resolve removal, race log (2026-10-27)

## Summary

A 50-minute campaign on 2026-10-26 showed all seven Fire and Tribute hosts broken before a single raid. The host clans
are outlaw clans, so vanilla's `DefaultDiplomacyModel.GetShallowDiplomaticStance` puts them at war with every kingdom, and
the hosts muster in their own side's realms: their own side's lords destroyed them. The fix spares a host from the AI of
its own side through `TaomMobilePartyAIModel` and an own-side snapshot in `FireAndTributeService`. The same session
removed Auto-Resolve Diagnostics (126 MB of the 130 MB log) and turned the new-campaign "pre-TAOM save" warning into an
information line.

The `/deep-review` ran at tier 1 with three lenses: Engine compatibility, and two Data flow passes. It found no CRITICAL
or HIGH, one MED and nine LOW (findings 1 to 10, finding 10 being the commit below); the convergence pass on the fixes found more LOW gaps (finding 11). Codex was not dispatched: the session had no word from Mike for paid dispatch. While the
review ran, another session committed the reviewed work in `6ae924638` under an unrelated subject and merged it with
origin in `a99a8cb1c`. The fixes below land in a follow-up commit, whose body carries the release-note text the first
commit lacks.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | No test proved that the model reads the same `FireAndTributeService` instance the behavior writes. Deleting `RegisterMapping<IRaidHostOwnSideQuery, IFireAndTributeService>()`, or replacing it with a second singleton registration, passed every test; the second form brings the bug back silently. | testing, composition | The new tests drove the service directly, and the Unlooked-For Aid wiring test registered its own substitute for the query. Nothing resolved the query from Fire and Tribute's real registration. | `FireAndTributeWiringTests` asserts `AreSame` on the two resolutions. Lesson in `testing-qa.md`. |
| 2 | LOW | The own-side check on `ShouldConsiderAvoiding` was not pinned: removing it passed every test. | testing | The new IL test covered the attack override only; the avoid override's test predates the change and checks the siege rule. | `TheOverride_CallsBase_AndReadsNoSettingsAndNoIoC` asserts the `OwnSideMeetsHost` call. |
| 3 | LOW | The doc claimed the tests covered every end path, but `Retire`, `Lost`, a Free host and a second live host had none. | testing, docs | The doc sentence was written from the test plan, not from the test list. | Three tests added; the doc names the paths it covers. |
| 4 | LOW | The check was one-way. A pinned host's initiative still runs when its best target fights at the host's target settlement (`MobilePartyAi.cs:485-486`), so a host could attack an own-side lord raiding its target village. | engine assumption | The design assumed `SetDoNotMakeNewDecisions(true)` stops all of a party's own initiative; the branch at lines 485 and 486 lets it through. | The model checks both directions. Lesson in `adapters-taleworlds-api.md`. |
| 5 | LOW | The first snapshot stored kingdom id sets built from `IKingdomWarSnapshotAdapter.GetKingdoms()`, which also computes strength, wars and prisoners, and rebuilt daily to follow a kingdom changing side, which `AlignmentService` (a map fixed at construction) cannot do. A kingdom founded later was missed for up to a day, and two windows (a throw in the spawn notice, a throw during session launch) left a live host unprotected for up to a day. | design | The snapshot was designed around the engine read instead of asking which input can change. | The snapshot now holds host id to side, published on every host-list change with no engine read; the side is resolved when asked. `TheAlignmentReadPath_ReadsNoSettingsAndNoIoC_AndLogsNothing` pins the worker-safe read path. |
| 6 | LOW | Docs described the model as overriding only `ShouldConsiderAvoiding` (`feature-map.md`, `unlooked-for-aid.md`, `coop-interop.md`), Fire and Tribute as having no GameModel, and `gamemodels.md` said "base first". The service remarks still said "Main thread only". | docs | The first doc pass updated the rows named in the plan, not every doc that describes the model (`grep` for the type name was not run). | All updated. Before closing a change to a shared model, grep every doc for the model's type name. |
| 7 | LOW | The removed feature's doc kept "Turning it off", "Reading the data", "Files" and "Cost" as live text; `balance-levers.md` cited it by line number, now shifted; `server-settings.md` quoted a total no gate checks (drift older than this change). | docs | The removal updated the status banner but not the body, and no gate checks `.md:N` citations. | The doc follows the `native-skin-fixes.md` removal shape; citations use the `#why-this-exists` anchor; the server-settings doc no longer quotes the total. |
| 8 | LOW | Race log tests modelled a `SyncData` call the engine skips for a save with no behavior record, so no test pinned the pre-TAOM warning path, and the `OnNewGameCreated` subscription that sets the new flag was unpinned. | testing | The tests reused the existing round-trip store and did not check `CampaignBehaviorDataStore.LoadBehaviorData`. | `RestoreHeroRaces_WithoutResetOrSync_Warns`; the behavior source test pins the subscription. |
| 9 | LOW | The doc did not say the rule is AI initiative only. Three player-involved paths still bring own-side forces against a host: lords joining a player battle (`DefaultEncounterModel.FindNonAttachedNpcPartiesWhoWillJoinPlayerEncounter`), a village defended after a host-versus-player fight, and TAOM's Refuge raid. | docs, scope | The fix was traced from the AI's engage decision outward, not from every path that starts a battle. | The "Own side" row names the three paths. |
| 10 | LOW | The reviewed work reached origin in a commit whose subject and body describe none of it, so `/release` would leave the three changes out of the changelog. | workflow | Another session committed the whole tree mid-review. | The follow-up commit body carries the three release notes. |
| 11 | LOW | The convergence pass found the review fixes' own tests too weak in five places: the culture argument and the second direction of the model's check were unpinned, no test had two live hosts, the alignment read-path pin allowed a cache write, and a source pin read comments. `gamemodel-bases.md` (the engine-bump diff list) still named only `ShouldConsiderAvoiding`. | testing | The fixes were written to the findings' wording, then checked with the two-edit test only on the MED finding. | Each gap has a test that fails when the code breaks (the direction test was proven by a deliberate break); the API snapshot was regenerated. |

## Root-cause pattern

Findings 1 to 3 and 8 share one cause: the tests drove the class under change and stopped at its boundary. The composition
that connects it to its consumer (finding 1), the second override (2), the paths the doc promised (3) and the engine's
real call pattern (8) sat one layer out. The two-edit test (break the behaviour, a test must fail) finds each of them
in under a minute, and it was not run on the new tests before review.

## Why each agent missed or found these

- **Engine compatibility** found finding 9 and confirmed finding 4's mechanism through the vanilla callers. It does not
  read tests, so findings 1 to 3 and 8 were outside its lens.
- **Data flow (Fire and Tribute)** found findings 1 to 6 by tracing the DI mapping and running two-edit checks on the
  tests.
- **Data flow (removal and race log)** found findings 7 and 8.
- No lens was run for Standards, Efficiency, Completeness or Design: tier 1 runs Data flow and Engine compatibility, and
  no CRITICAL or HIGH triggered a second wave.

## Follow-ups

- FOLLOW-UP: a host's anchors are not re-checked at spawn, so a host can muster beside an anchor the other side has captured. no issue: offered to Mike, who decides on new public issues.
- FOLLOW-UP: TAOM's Refuge raid (`RefugeService.FindNearestHostile`) can pick a host and force a battle. no issue: a guard for a rare case is Mike's call under `simplicity-criterion.md`; offered to him.
- FOLLOW-UP: the broken-host log line does not name the destroyer's faction, so a run cannot show who broke a host. no issue: offered to Mike with the in-game check.
- FOLLOW-UP: a pre-TAOM save loaded after another campaign in the same process restores the previous campaign's race map (older than this change). no issue: offered to Mike.

## Feedback to codify

- `testing-qa.md`: a module that maps one singleton to a second interface pins it with `AreSame`.
- `adapters-taleworlds-api.md`: `SetDoNotMakeNewDecisions(true)` does not stop a party's initiative when its best target
  fights at the party's target settlement.
