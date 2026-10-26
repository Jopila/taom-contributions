# RCA: Camp work, rest healing and start in camp, deep review, #860 (2026-10-26)

**Summary.** #860 extends Field Camps with three parts. Camp work sets the men to hunt (meat), fell timber (hardwood, forest
only) or fish (fish, water near the camp) for a 10-hour wait whose progress is saved in the camp's record (`CampState`
fields 108 to 111). Rest healing adds a base amount of healing for the main party while the camp panel is open, through
`TaomPartyHealingModel`. Start in camp, off by default, raises a field camp on the first map tick of a new campaign. The
review was tier 1 (a GameModel, save data, a campaign tick and DI composition). Wave 1 ran lenses 5, 2 and 7. Lens 7
reported one HIGH, so wave 2 ran lenses 1, 3, 4 and 6 on the fixed tree, and one convergence pass checked the fix diff.
Codex did not run: paid dispatch needs Mike's word in this session, and the only word on record belongs to another
session's work. No lens found an incompatible engine member; lens 2 verified 34. Every confirmed finding is fixed unless
listed under "Not applied".

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The morale step added morale (`RecentEventsMorale += amount`, "The work lifts the party's spirits"). The plan and the issue comment say a morale cost that shrinks with clan tier. Tests, doc and text all encoded the gain. | Spec drift | The builder wrote tests from its own reading of "1 per ten men ... floored at 0" and never from the plan's word "cost". No test name carried the word. | `MoraleCost` and `LowerMorale`, a cost in the text, tests named for the cost. Lesson: a direction word in a plan (cost, gain, cap, floor) becomes a test named for it. |
| 2 | MEDIUM | The Field Camps master toggle did not reach a running work: it kept ticking, rolled its injury and paid. | Gate coverage | The gate was written at the start, the one place a player chooses. The tick and the payout were treated as the same decision already made. | `FeatureDisabled` outranks every block; `Current`, `Advance` and `Complete` end the work and pay nothing, as Far Watch does. |
| 3 | MEDIUM | Start in camp could never succeed: every one of the 25 default culture starts stands 0.80 to 7.13 map units from its town (measured against the live `settlements.xml`), inside the 10-unit town distance. The plan kept the rule because "many" starts were near a town. | Unmeasured plan claim | The plan's "many" was an estimate; nobody measured the start positions before deciding D2. The unit tests fake `CanEstablish`, so no test could see it. | The start attempt alone ignores the town distance (`ignoreTownDistance`); every other block still applies. Lesson: measure the shipped data before a decision rests on "many" or "some". |
| 4 | MEDIUM | The healing model held the camp-rest decision inline (five gates, two perk writes) and grew from 144 to 190 lines; the gates were pinned only by source text. | Layering | The model already delegated the career passive to a service; the new code did not copy that shape. | `CampRestHealing.Grant` decides; a boundary helper, `CampRestModelLine`, reads the four engine facts and applies the result, so each override is one call over vanilla's number and the model is back to 149 lines. Behaviour tests replace the source pins. The convergence pass also found an untagged test that builds a behavior and a stale API snapshot; both fixed. |
| 5 | MEDIUM | Three services built `TextObject`s (`CampActivityService`, `CampRestHealing`, `CampStartInCamp`), so 13 untagged tests ran engine code. | Layering, test categories | The precedent the feature copied (Far Watch) returns a reason and lets the hook render it; the builder composed the text where the decision was. | Services return reasons or numbers; hooks render. Lesson appended to testing. |
| 6 | MEDIUM | Documentation gaps: no feature-map row, three docs with the old camp-panel order, `server-settings.md` still at 470, no #931 entry for the new keys, nothing from #860 in the Owed list. | Completeness | The builder updated the docs the tests read and the feature doc; nothing checks the sibling docs that quote the panel order. | All fixed; #931 gets its comment at merge. |
| 7 | LOW | The midpoint wound skipped the single-player gate; only the start and the payout checked it. | Gate coverage | Same as 2. | `Advance` checks the gate first, clears the work and says why once, before any roll. |
| 8 | LOW | "Return to the work" skipped every start block, so a saved work could resume while the camp re-raised after Fortify or while a scout was out. | Gate coverage | Same as 2. | The camp-panel option always uses `GlobalBlock()`; the dead "Resume" option in the pick menu is deleted. |
| 9 | LOW | The unguarded `PartyBase.MainParty` read in the healing model throws when a campaign has no main party; the model runs for every party the engine heals. | Engine statics | The rule in `adapters.md` ("campaign statics") was not applied to a model. | `Campaign.Current?.MainParty?.Party`. |
| 10 | LOW | The `CampRestHealing` model parameter was optional, so removing the `SubModule.cs` argument still compiled and silently turned rest healing off. A wiring test passed only because of how the type name was spelled. | Wiring | An optional parameter was used to keep a single-owner file unedited during the build. | The parameter is required; a test pins the `SubModule.cs` argument. |
| 11 | LOW | Rest perks still applied with both rest rates at 0; the status line was drawn on the base camp menu where nothing heals; the status text ran two sentences together and named no units; "1 tools" could print; wrong engine reasons in comments ("a menu stops campaign time" for a wait menu); a dead adapter guard; text composed into six global variables for a fixed menu; an interface with one implementation; two hand-synced terrain lists; a hand-written wound loop instead of the engine's `WoundNumberOfNonHeroTroopsRandomly`; redundant guards; stale comments and plan citations; redundant source pins. | Hygiene, design | Local slips, and design proposals from lens 6. | All applied in the fix passes. The wound loop change draws evenly from all healthy regulars instead of stack by stack (a behaviour change, applied as the session's recommendation). |

## Root-cause pattern

Findings 2, 7 and 8 share one cause: a timed job checked its gates at the start and trusted them for the rest of the
job. A wait runs for hours, a save can carry it into another session, and the player can change settings or the camp
in the meantime. Every step that writes (the tick, the roll, the payout, a resume) must re-check the gates the start
checks. Findings 1 and 3 share another: the build took a plan's words as settled facts. One word ("cost") was lost
between the plan and the tests, and one estimate ("many") was never measured.

## Why each lens caught or missed these

- Lens 7 (XML) found 1 by reading the text the player sees against the plan comment, plus the status line placement and wording items.
- Lens 5 (data flow) found 2, 7, 8, 10 and the both-rates-zero case. It traced every step of the wait, not only the start.
- Lens 2 (engine) found 3 by measuring the start positions against the live map data, and the wrong engine reasons.
- Lens 1 (standards) found 4, 5 and 9 in wave 2.
- Lens 4 (completeness) found 6.
- Lens 3 (efficiency) found no defect; every hot path is clean.
- Lens 6 (design) proposed the engine wound helper, one terrain table, deleting the single-implementation interface, the keyed pick menu and the per-menu init.
- Wave 1 lenses did not run the standards check, so 4, 5 and 9 waited for wave 2.

## Not applied

- The camp-work adapter and its interface stay in the feature folder, not `Main/Adapters/`. Seven feature-local adapters have the same shape.
- The adapter reads Far Watch's `FarWatchStore` directly: `IScoutIntelQuery.IsScoutAway` covers only settlement rides, and work must also stop for any ride.
- The older inline logic in `TaomPartyHealingModel.GetSurvivalChance` keeps the file above 150 lines. FOLLOW-UP: no issue: pre-existing code outside this change; a move to a service is the single-player predicate follow-up's sibling and waits for Mike's word.
- The single-player predicate is now copied in seven services (ten with the three-term copies). FOLLOW-UP: no issue: Mike's word is owed for the shared-predicate refactor; `CampGate` is the seed.
- Honey and wax activities: no such item exists in any installed module. FOLLOW-UP: no issue: creating an issue for two trade goods needs Mike's word.

## Lessons appended

- `lessons/gamemodels-services.md`: a timed job re-checks every gate its start checks, on each step that writes.
- `lessons/build-tooling-workflow.md`: a plan's direction words become test names, and a plan decision resting on "many" is measured first.
- `lessons/testing-qa.md`: a service returns a reason, the hook renders the text, or the service's tests need the game.
