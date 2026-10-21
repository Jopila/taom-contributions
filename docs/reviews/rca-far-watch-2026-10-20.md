# RCA: Far Watch, send a scout from camp (deep-review, 2026-10-20)

## Top-line

`/deep-review` of Far Watch (#847), the camp action that sends a scout to sweep the surroundings or look over one
settlement, ran seven lenses in two waves. It found no CRITICAL or HIGH defect, 6 MEDIUM and about 25 LOW, several
reported by more than one lens. Every finding was fixed before the commit. Four fixes follow choices Mike made: the
sight cap stays below the engine's whole-grid search radius; any encounter with the camp ends a ride; a solo ride has
its own two labels; only the wait menu's own tick leaves the wait menu. The convergence pass is recorded at the end.

Three findings repeat knowledge the repo already held, and this time the design brief named the right lessons files.
The design cited the lesson on modals opened from a camp menu for its rule (pause the game) and missed the fact inside
it that mattered here (a camp attack opens `encounter_meeting`, not a battle). The army sweep repeated "In an army is
two states". The test tags repeated the 2026-10-08 binding-gate lesson.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | A ride called back after a few minutes renewed an older, larger sight buff's clock to a full new hold, so short rides repeated every rest kept the +50 % sight forever. `End` set the buff to `Math.Max(reached, held)` whenever the ride had reached anything. | Timed effect re-armed by a lesser event | The rule "keep the larger lift" was written for the level and applied to the clock as well; no test called a ride back while an older buff was still held. | `End` sets a buff only when the ride reached more than the held buff; tests for both sides. Lesson in `lessons/gamemodels-services.md`. |
| 2 | MED | The sweep skipped every army member, including members that had joined the army but were not yet merged and still moved on their own, so they vanished from the report. | Army membership read as one state (REPEAT, `lessons/campaign-mechanics.md` "In an army is two states") | The lesson was not in the design's list of facts to check for a party scan; the adapter copied the `Army.LeaderParty != party` shape of a sibling that has a different question. | The fact is `IsAttachedToArmy = party.AttachedTo != null`; sweep test with an unattached member; adapter source pin. Recurrence note on the lesson. |
| 3 | MED | An army leader's row took its troop count from the army and its composition (foot, archers, horse) from the leader's own roster only. | Two halves of one row from two sources | The composition reader was written for a single party and reused for the leader. | The adapter adds every attached party's roster through the pure `FarWatchMath.AddMen`; test with a foot leader and attached horse archers. Covered by the lesson on finding 2. |
| 4 | MED | An attack on the camp opens `encounter_meeting` with no battle, so `MapEventStarted` fires only if the meeting turns hostile. A peaceful meeting left the ride running while the docs said any encounter ends it. | Event chosen without its firing condition (REPEAT of the mechanism in `lessons/campaign-mechanics.md` "A modal opened from a game menu must pause the game") | The design cited that lesson for its pause rule and did not carry the mechanism it records; the IL pin proved `PlayerEncounter.Init` and never the attack path. | The behavior also listens to `CampaignEvents.OnPartyEncounterEvent` (raised last in `EncounterManager.StartPartyEncounter`), gated on a running ride and on the main party, and switches no menu. IL pins on the raise and on the meeting answer. Lesson in `lessons/campaign-mechanics.md`. |
| 5 | MED | The buff lifted vanilla's 60-unit sight cap to 90. Past 77.5 units the visibility pass asks the campaign's `LocatorGrid` (32 by 32 cells of 5 units, span capped at 31 cells) for every cell, every frame, with a quadratic `List.Remove` term, and repeated rides could hold it. | Engine cost not measured before a cap was raised | The design verified the cap (`LimitMax(60)`) and the arithmetic, not the cost of a larger radius in the consumer. | One clamp in `SpottingContributorComposer.MaxCapBonus` (0.29, a 77.4 cap) for every contributor, with the reason in a comment, a value test and two IL pins on the grid shape. Lesson in `lessons/gamemodels-services.md`. |
| 6 | MED | Composer and sight tests that run engine types lacked `RequiresGame`, and two engine-fact tests that execute game IL lacked `RequiresGameIL`. | Test tags (REPEAT, `lessons/testing-qa.md` 2026-10-08 binding-gate lesson) | The builder tagged by file, not by what each test executes. | Tagged per test; the composer tests moved to `TAOM.Tests/Core/Domain/`. Recurrence note on the lesson. |
| 7 | LOW | `HiddenHostsWiringTests...CarriesThePatchCategory` failed when run alone (`Assembly.GetTypes()` without the game assemblies). Shipped with Hidden Hosts. | Test order dependence | The suite always loads the assemblies first, so only a filtered run shows it. | `GameAssemblies.EnsureLoaded()`; RED and GREEN logs alone. One-off. |
| 8 | LOW | `RaiseByScout` built on a remembered count without the `Agrees` check that Hidden Hosts introduced for exactly that case, so a contradicting memory could outrank the scout's reading. | New consumer of a cache skips its validity predicate (REPEAT of `rca-hidden-hosts-p23-2026-10-19.md` finding 2) | The predicate lives in the math class, not on the row, so a new reader can skip it. | `RaiseByScout` uses `Agrees`; two data rows. Lesson in `lessons/gamemodels-services.md`. |
| 9 | LOW | Report names did not follow the map plate (army name, then leader, then party name). | Display rule restated | The report was written from the party name. | `FarWatchMath.PlateName`, read from `PartyNameplateVM`. |
| 10 | LOW | Field Camp switched off mid-ride kept the sight line promised and split the scout-away rule. | Toggle off mid-state | The reconcile listed camp states, not the master toggle. | The reconcile ends the ride (`CampStruck`) when Field Camp is off. |
| 11 | LOW | The rest text named the current scout, not the rider who rests. | Stored fact read live | The store kept the rest clock without the rider. | A rest name is saved (`_taomFarWatchRestName`); old saves read it empty. |
| 12 | LOW | The camp option stayed greyed after the rest ended, because menu conditions re-run only on activate or refresh. | Menu state not refreshed | The design checked the conditions, not when they run. | The hourly tick refreshes the camp panel when its offer is stale (`IGameMenuAdapter.RefreshCurrent`). |
| 13 | LOW | A solo ride's labels read "Call <own name> back" and "Await your scout", and the saved solo flag was unread. | Text for one case only | The texts were written for a named scout. | Two new keys, seeded in 12 languages; other third-person lines left as they are (reported). |
| 14 | LOW | Docs and comments: the encounter path, the 90 cap, co-op wording of a wait completion, the militia line, the `SeeingRange` reader list, "one call site", "adds no allocation", `reflection-sites.md`, the Hidden Hosts tests list, the contributor interface comment, a 216-character line, a rule row over its cap. | Engine behaviour stated from the design | The design's wording went into the docs without a second read. | All corrected; allocation claims now have allocated-bytes tests. |
| 15 | LOW | Dead or duplicate members (`ScoutHeroFacts`, two option texts, `bool? hidesName`, store bounds restating constants, a facts file named after no type, `Clear` vs `ResetForNewSession`). | Leftovers | Built before the final shape settled. | Deleted, renamed or split. |

Applied design proposals that keep behaviour: `RaiseByScout` returns its text (`ScoutReading` deleted); site intel reused
for defender count, army lead and distance; scratch lists as locals; `ScoutSighting` a record; `ScoutTurn` carries its
notice; `IsFiniteAtLeast`; "Mission" renamed "Ride" in code (no saved key changed). Applied with Mike's word: the hourly
reconcile no longer switches menus. Not applied: one shared single-player predicate across features (a follow-up).

Departures the builder recorded, accepted: `NameplateIntel.IsHorizon` stays for existing assertions; the meeting-path
pin proves the menu id and the default answers, the branch order is read in the decompile; the sweep's one-off locator
query uses about 78.2 units once per returned ride, not per frame; a wait-menu completion that lands before the next
hourly look still completes while Field Camp is off.

## Root-cause pattern: a lesson read for its rule, not its mechanism

The Hidden Hosts RCA asked that a brief name the lessons files for each subsystem, and this design did, and cited two
campaign-mechanics entries by name. Findings 2, 4 and 8 still repeat recorded knowledge. In each case the entry was
read for its conclusion in its own context (pause a modal; settlement menus need a merged party; a contradicting
reading replaces a memory) while the fact that applied here sat in the entry's body: the meeting menu that an attack
opens, the unmerged army member, the predicate any reader of the memory must apply.

**Preventive action.** When a design cites a lesson, it also lists the engine facts in that lesson's body that touch the
new feature, one line each, and the test that pins each. A fact with no test is an owed item in the design, not a
citation.

## Why each lens caught or missed what it did

- **Standards (1)** found the missing test tags (6).
- **Engine compatibility (2)** found the meeting path (4) and, with data flow, the leader-only composition (3).
- **Efficiency (3)** found the cost of the raised cap (5), and stated the whole-grid point as 77.5 units; the
  convergence pass corrected that figure (below).
- **Completeness (4)** found the owed tests, the translation backlog entry and the issue comment.
- **Data flow (5)** found the buff clock (1), the unattached army members (2) and the leader-only composition (3).
- **Design (6)** gave the simplest shapes for the fixes and the single exit path from the wait menu.
- **XML (7)** found the 13 string files clean (schemas, ModuleData validation, seeds, bytes) and four LOW text faults:
  the militia doc line (14), the solo labels (13), the sight line printed with Field Camp off (C6 below) and two dead
  option texts (15). Its only failing gate was the external coverage ratchet on nine TAOM_Map Carn Dum names, outside
  this change.

## Convergence pass

One deep-reviewer read the fixes against the brief and the v1.5.5 decompile. It confirmed every brief item in the
code and found no regression in save keys, the encounter handler, the reconcile and wait-menu exit order, the camp
panel refresh or the army rules. It found one MEDIUM and six LOW, all fixed:

| # | Sev | Finding | Fix |
|---|---|---|---|
| C1 | MED | The "77.5 whole-grid threshold" behind finding 5 is wrong. `LocatorGrid.GetBoundaries` caps the span at 31 cells, and the span reaches 31 for most positions once the radius passes 75; the grid wraps, so a full walk visits nearly every party. Vanilla's step-sound search (`MapScreen.TickStepSounds`, `seeingRange + 25`) already walks the whole grid every frame. The cost grows smoothly with the radius and has no step. | The 77.4 cap Mike chose is kept: 74.4 would save about 6 % of the walk. The comment, the engine pins and the docs now state the real facts; the in-game A/B stays owed. |
| C2 | LOW | A registry row still said the cap rises to 90. | Corrected. |
| C3 | LOW | The doc said the wait tick runs while the report inquiry pauses the game; a paused inquiry runs only the idle tick. | Corrected; the landing happens when the inquiry closes. |
| C4 | LOW | The one-call-per-key save guard listed 15 keys and missed the new rest-name key. | 16 keys. |
| C5 | LOW | `NameplateIntel.IsHorizon` had no production reader. | Deleted; tests read the horizon text. |
| C6 | LOW | A wait-menu completion with Field Camp off printed a sight promise the contributor no longer kept. | The completion applies the reconcile's Field Camp check. |
| C7 | LOW | `Complete` always set a fresh buff, so a weaker scout's return shortened a longer held full buff (same class as finding 1). | A held full buff that ends later is kept. |

**Why C1 got through.** The orchestrating session put the cap question to Mike with the 77.5 figure from the
efficiency lens and did not read `GetBoundaries` first. That breaks `evidence-over-claims.md` A.4: spot-check a subagent's load-bearing claim
before you relay it. The figure decided a player-facing number. The preventive action is that rule; it already
exists, so the lesson records the recurrence. C6 was in the XML lens report already; the fix brief folded it into finding 10 (the toggle off mid-ride), the
builder fixed the reconcile and left the wait-menu completion as a departure, and the convergence pass found it again.
A brief that merges two findings keeps both failure scenarios as separate tests. C7 is finding 1's class in a second method: a fix for "a lesser event
re-arms a timed effect" checks every writer of that effect, not only the one the finding named.
