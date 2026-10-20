# RCA: Hidden Hosts phases 2 and 3 (deep-review, 2026-10-19)

## Top-line

`/deep-review` of Hidden Hosts phases 2 (tooltip masking) and 3 (the saved "~" memory, the hourly fade, plain-sight
stamps and Scouting XP), #846, ran seven lenses in two waves. It found no CRITICAL defect, 2 HIGH, 5 MEDIUM and about
20 LOW, several reported by more than one lens. Every finding was fixed before the commit, three of them through
choices Mike made (a contradicting live view beats a memory even with no digit readable; the player's own siege
counts on both sides; the approach XP stays). The convergence pass on the fixes is recorded at the end.

Two findings repeat knowledge the repo already held: the siege test (`lessons/campaign-mechanics.md`, "Is the player
in a siege is `PlayerSiege.PlayerSiegeEvent`") and the stopped campaign tick in menus (the trap index's "Menus stop
time" and `docs/reference/engine/campaign-tick-time-and-party-ai.md`). Neither the design nor the build read them,
because the design brief named only the five lessons files the phase 1 review had touched.

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Exact plain-sight stamps ran only from `CampaignEvents.TickEvent`. `Campaign.Tick` raises it only while `_dt > 0` (or in the first three ticks), and every plain game menu sets the time control to Stop, so town, castle, village, encounter and conversation visits were never stamped. The memory popup told the player the opposite. The stamp throttle also counted campaign time (0.25 x real time at play speed) while the doc said real time. | Event chosen without its firing condition (REPEAT of a known trap) | The design traced the visibility event's thread but not when `TickEvent` fires; the trap index line "Menus stop time" does not name `TickEvent`; the behaviour tests called `OnTick(dt)` by hand, so the engine's gate was never in the test. | A third behavior stamps on `GameMenuOpened` (raised by `GameMenu.RunOnInit` for every menu) and on a real-time throttled tick for waits; IL pins on the `_dt > 0` gate and on `RunOnInit`. The trap index line now names `TickEvent`. Lesson in `lessons/campaign-mechanics.md`. |
| 2 | HIGH | A remembered count outranked a live reading that contradicted it: an 80-man party counted with two digits that grew into a 300-man army showed "~80". `Record` overwrote only for a reading with at least as many known digits, and a reading with none never reached the store. A test pinned the "~123 over a 7-man host" behaviour as intended. | Cache invalidated only by a better observation | The design defined "better" (more digits) and never "contradicting"; the test that pinned the case read as a feature. | `HiddenHostsMath.Agrees` (same digit count and same known leading digits, integer only) decides; a contradicting reading replaces the row, with no digit readable too (Mike). A zero-allocation test guards the per-frame path. Lesson in `lessons/gamemodels-services.md`. |
| 3 | MED | The "Counted ... ago" row and the memory popup's flag fired for a settlement whose tooltip had no defenders row (Alt view, hideout, hidden information): the text getter set "shown" before the row was found. | Shown-flag set by a getter | The service method that formats the "~" text also recorded that it was drawn. | `ShowedMemory` on the frame is set only by the code that wrote a "~"; the reason row and the flag follow it. Lesson in `lessons/localization-ui.md`. |
| 4 | MED | The stamp exemptions (main party, player clan, player army, info-hidden, true sight) and "in plain sight" were decided again in a new adapter, `HiddenHostsCampaignAdapter.Add`, beside the service's `IsPartyExempt`. | Policy in an adapter (REPEAT of phase 1 row 10) | Phase 1 moved its exemptions into the service; phase 3 added a new adapter and repeated the shape. | Stamps read `PartyIntelSnapshot`s and the service applies one shared predicate; the structs and the adapter method are deleted. Lesson extended in `lessons/gamemodels-services.md`. |
| 5 | MED | "In plain sight" for the player's own siege used `MobileParty.SiegeEvent`, which is set only for besiegers, so a defending player got neither exact counts nor stamps of the besiegers, while the stamp list covered the defenders. | Engine member read by name (REPEAT, `lessons/campaign-mechanics.md` "Is the player in a siege") | The lesson exists; the design brief did not send the architect to `campaign-mechanics.md`. | `PlayerSiege.PlayerSiegeEvent` on both sides, IL-pinned. Preventive action below. |
| 6 | MED | A fault that left the tooltip prefix (the `Enabled` read sat outside `Open`'s try) failed the whole tooltip and bypassed the fault budget. | Prefix not exception-safe | The second bracket (`OnPeriodicRefresh`) was written correctly; the first copied an older shape. | `Open` reads everything inside its try; the prefix sets `__state` first. One-off. |
| 7 | MED | The in-game plan did not exercise the adapter-only facts (own fief, a companion inside, the conversation partner, a player-clan battle, an army leader's own row, true sight). | Owed check incomplete | The doc called the in-game check the adapters' coverage without listing what only it can prove. | Checklist extended. One-off. |
| 8 | LOW | Battle and siege totals counted info-hidden parties that vanilla leaves out ("+ ?"), so a mask could have more digits than any printed number. | Mirror of a vanilla filter incomplete | The design summed involved parties without reading vanilla's own sum. | One helper over `InvolvedParties`, skipping settlements and info-hidden parties. |
| 9 | LOW | A party tooltip of an army leader used the army total for its digits and latch, while vanilla's party tooltip prints the leader's own roster. | Count source mismatch | The phase 1 snapshot count includes attached parties. | The Party frame of a leader uses `OwnHealthyCount`, with no latch, memory or XP. |
| 10 | LOW | Pending Scouting XP was granted only from `TickEvent`; a save made while time was stopped kept the paid latch and lost the XP. | Same root as 1 | Same as 1. | Also granted on `OnBeforeSaveEvent` (raised by `SaveHandler.SaveTick`), co-op gated. |
| 11 | LOW | A remembered total appearing mid-hover showed "~" on the plate but not in the open tooltip. | Decision taken at build only | The wrapper was added only when the verdict already had memory. | Every masked party or army total is wrapped. |
| 12 | LOW | A two-thread interleave could leave a stale frame current; the owner-thread check hid it from readers, not from openers. | Shared static scope | The design guarded readers only. | `[ThreadStatic]` slot, owner-thread field deleted, interleave test. |
| 13 | LOW | Docs and comments: stamp interval in real time; "party screen" as a size-text caller; tooltip rows "sharpen every two seconds" (not the plain-string ones); "1 day 3 hours" (vanilla says "1 day and 3 hours"); a second `RemoveParty` caller; the wrapper's fault handling; "local UI only" and "Display only" now that the toggle gates Scouting XP; co-op join as a save load; the thread question; stale counts and comments; `patch-targets.md` lists one target per `TargetMethods` class. | Engine behaviour stated from the design | The design's wording was carried into docs without a second read. | All corrected; the generator limitation is stated in the registry entry. |

Not applied, with reasons: a single pass over each side of a siege tooltip (the same size of code for a tiny win);
one shared snapshot builder for the eight test files (a follow-up once this lands, it would touch every test file the
fixes touched).

## Root-cause pattern: the design read the engine, not the repo's record of it

Findings 1, 4 and 5 are things TAOM had already written down: the stopped tick in a trap line and an engine doc,
the policy-in-adapter shape in the phase 1 RCA, and the siege member in a lessons entry. The architect brief for
phases 2 and 3 listed five lessons files, the ones phase 1 had appended to, and the architect read the engine
thoroughly instead. That produced correct engine citations and repeated the repo's known mistakes.

**Preventive action.** A design or build brief names the lessons category FILES for every subsystem it touches, not
the files the last review changed (AGENTS.md already says to read the category file before touching a subsystem;
the brief is where it was lost). For this feature that is `campaign-mechanics.md` (events, sieges), `gamemodels-services.md`
(service and adapter boundary), `harmony-il.md`, `state-lifecycle-save.md`, `localization-ui.md` and `testing-qa.md`.

## Why each lens caught or missed what it did

- **Standards (1)** found the policy in the adapter (4) and, outside its lens, the stopped tick (1).
- **Engine compatibility (2)** found the stopped tick (1) with the settlement case, the battle total (8), the leader
  count (9) and the doc errors, and verified 49 engine members.
- **Data flow (5)** found both HIGH findings (1, 2), the shown flag (3) and the siege asymmetry (5).
- **XML (7)** found the "1 day and 3 hours" wording; the seeds were correct.
- **Efficiency (3)** found no per-frame cost and gave the cost rules the fixes followed (integer prefix test, light
  stamp reads, a real-time throttle, the main-party check on settlement events).
- **Completeness (4)** found the in-game plan gaps (7) and the test that pinned finding 2 as intended.
- **Design (6)** gave the simplest shapes for 1, 2, 3, 4 and 5 and found 11 and 12.

Finding 1 was reported independently by three lenses.

## Lessons appended

- `lessons/campaign-mechanics.md`: a record taken on `TickEvent` misses every state that stops time.
- `lessons/gamemodels-services.md`: a remembered observation is replaced by a contradicting one, not only by a better one; and a new adapter in a later phase repeats a policy the service owns.
- `lessons/localization-ui.md`: a "shown" flag is set by the code that wrote the text, never by the getter that formatted it.

## Convergence pass

One reviewer checked the applied fixes for standards and parity and reported CONVERGED (nothing at MEDIUM or above).
Its LOW findings were fixed before the commit:

| # | Sev | Finding | Why missed | Action |
|---|---|---|---|---|
| 14 | LOW | The fix for finding 8 also dropped info-hidden parties from the SIEGE total, but vanilla's siege tooltip counts them; only its battle tooltip leaves them out. | The fix list said "same for the siege read if it applies" and the check of whether it applies was skipped. | The siege total counts every non-settlement party again; the helper's comment names the battle only. |
| 15 | LOW | The two new handlers (the plain-sight stamps and the shared XP grant, now also run before a save) read the toggle outside their try, the class finding 6 fixed in the tooltip prefix. | The new code copied the pre-fix shape. | The read moved inside the try; one test per handler. |
| 16 | LOW | About a dozen doc and comment lines the fixes made untrue (the wrapper now applies to every masked total, the reason row follows what was written, the thread field is gone, the event lists and counts, the army leader's party frame, the wrapper's fault path, the save event's raiser). | Fixes changed behaviour faster than the prose around it. | Each line corrected against the code. |

The trap index line "Menus stop time" (`docs/ai-includes/orientation.md`) now says that no `TickEvent` fires in a
menu; the lesson in `lessons/campaign-mechanics.md` names `GameMenuOpened` as the event that does.
