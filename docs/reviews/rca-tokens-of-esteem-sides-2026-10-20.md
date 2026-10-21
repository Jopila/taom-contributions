# RCA: Tokens of Esteem sides (deep review, 2026-10-20)

## Top-line

The `/deep-review` of the Tokens of Esteem alignment fix (#957) ran six lenses on the uncommitted worktree
`E:\repos\taom-relations-politics`. The gift gate sided the lord and the player by `Clan.Kingdom.StringId` paired with the
hero's own culture. The two ids came from different entities, which is the pairing `IAlignmentService.ResolveSide` forbids:
a Free-culture lord serving Khand or Umbar read Free and refused an Evil player, and an Evil-culture character in a Neutral
realm read Evil. The fix reads the map faction and that faction's own culture for both sides. The review found three MEDIUM
findings and three LOW ones, all fixed or documented in the working tree. The full suite then passed (21562 passed, 0
failed, 5 skipped).

The original bug is a repeat of the 2026-10-06 lesson "Hand `ResolveSide` an id and the culture of that same entity"
(`lessons/data-content-cultures.md`), which the #863 review did not check.

## Findings and root causes

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | The gate paired the hero's kingdom id with the hero's own culture, so the side of a Free-culture lord in a Neutral realm (or an Evil-culture player in one) was wrong, and the Neutral four (Khand, Umbar, Shaghana, Abanissa) were sided by a culture they do not hold | Data flow; `ResolveSide` pairing contract | The #863 review lenses did not check the pairing contract, and the builder did not read `data-content-cultures.md`, a repeat of the 2026-10-06 lesson | Both sides read `MapFaction.StringId` with `MapFaction.Culture.StringId`; `Gate_AlignmentRefusal_ReadsTheRealmNotTheHerosOwnCulture` and the taste test pin it |
| 2 | MED | The in-game checklist could not catch a regression: it said a Khand or Umbar lord is never refused, but no step put an Evil-culture character in Khand or a Free-culture lord in a Neutral clan (Lens 4) | Completeness; manual check too coarse | The checklist was written from the rule's wording, not from the failing pairings | Step 6 now lists who refuses and who never does, with sub-steps (a) `campaign.join_kingdom` into Khand and (b) a Free lord serving Khand or Umbar |
| 3 | MED/LOW | Independent minor clans with vanilla cultures that `alignment.json` does not list now read Neutral: the Axemen of Erebor (`skolderbrotva`, culture `nord`) and the Dr\u00faedain (`forest_people`, `vakken`). While unhired they accept an Evil player's gift, and Good Deeds reads them the same way (Lenses 2, 5) | Data gap exposed by the fix; documented, follow-up | `alignment.json` lists kingdoms and TAOM cultures, not every vanilla clan culture | Documented under Known Limits; a follow-up decides whether to list `nord` and `vakken` or side those clans another way |
| 4 | LOW | The `GiftFacts` summaries still described `KingdomId` and `CultureId` after the rename (Lenses 1, 2, 4, 5) | Stale documentation | The rename was done by search and replace and the summaries were not re-read | Summaries rewritten to name the pair, its single source and the hero's own culture as taste only, modeled on `DeedFacts` |
| 5 | LOW | Three test rows duplicated rows of `Gate_AlignmentRefusal_IsFreeLordAgainstEvilPlayerOnly`, several rows used `mordor` as a realm id (the realm is `empire_s`) or `""` for a kingdomless side, and the `Lord(...)` fixture default reused the hero's own culture for the side (Lenses 1, 2, 4, 5) | Test quality; unrealistic inputs | The new test was written next to the old one without comparing rows value by value, and the fixture default hid the bug it was meant to expose | Duplicates deleted; real ids (`empire_s`, `player_faction`, an unlisted clan id); the fixture side culture defaults to the faction id |
| 6 | LOW | `ReadPlayer` read `Clan.PlayerClan?.MapFaction` twice and `ReadRecipient` passed three adjacent strings positionally, so a swap would compile silently (Lens 1) | Readability | Builder self-check | One local for the faction; named arguments for `CultureId`, `SideFactionId` and `SideCultureId` |

## Root-cause pattern

Callers assemble the side pair from two separate reads. `ResolveSide(id, culture)` takes two strings and cannot tell
whether they describe one entity, so any caller that reads the id from one place and the culture from another compiles,
passes a test that feeds both from the same fixture value, and sides the wrong way for exactly the realms that need
the culture fallback. Tournament Alignment (2026-10-06) made this mistake first, Tokens of Esteem repeated it, and Lord
Courtesies and Hard Questions still pair a kingdom id with another entity's culture.

Prevention is one shared constructor: a single value (Lens 6 proposed `SideIds`, built from a map faction) that every
caller uses, so the pair cannot be assembled from two reads. That is a follow-up, not part of this fix.

## Why each agent missed these

- **The #863 review lenses** did not check the pairing contract of `ResolveSide`, and the builder worked from the
  alignment table without reading the 2026-10-06 lesson, because the code under change was a gift feature, not culture data.
- **Lens 1 (Standards)** found the stale summaries, the duplicate and unrealistic test rows and the positional strings (4, 5, 6).
- **Lens 2 (Engine)** found the minor clans with unlisted vanilla cultures, the stale summaries and the test rows (3, 4, 5).
- **Lens 3 (Efficiency)** found nothing.
- **Lens 4 (Completeness)** found the checklist that could not catch a regression, the stale summaries and the test rows (2, 4, 5).
- **Lens 5 (Data flow)** found the minor-clan side, the stale summaries and the test rows (3, 4, 5).
- **Lens 6 (Design)** proposed the shared `SideIds` value as a follow-up.

## Feedback memories to codify

None. The rule is in `docs/reviews/lessons/data-content-cultures.md`; this review adds a repeat line under it.

## Part 2 (2026-10-20): every caller on one pair (#957)

Part 1 left the same pairing in other features and proposed the shared pair as a follow-up. Part 2 is that follow-up,
with the maintainer's widened scope: one side pair for every feature, the minor clans the table did not list, and a gate
over every non-bandit clan.

### What was wrong

| Caller | The bad pair | Effect |
|---|---|---|
| Lord Courtesies (player and lord) | the realm id with the hero's own culture | a lord or player of a Neutral realm sided by a culture the realm does not have, so a gift crossed or was refused wrongly |
| Hard Questions (executor, victim, evaluators, trait multiplier) | the same | the relation and trait scaling used the wrong side for a Neutral realm's lord |
| Execution seams (blood-feud relation postfix, honor prefix) | executor culture from `Hero.MainHero.Culture`, victim and evaluator from the hero's or clan's kingdom with the hero's or clan's culture | the same, on the live execution path |
| Tournament hero entrants | clan kingdom with `character.Culture` | a Free-culture lord of a Neutral realm barred from an Evil town |
| Wanderer Allegiance | the player clan's kingdom with the player clan's or hero's culture | a Neutral realm's player refused by a wanderer |
| Prisoner Recruitment | the leader's kingdom with the leader's culture | the side rule waived the morale cost for a Neutral realm's lord |
| Axemen of Erebor, Druedain (and the Grey Company, Beornings, Serpent Guard, Corsair Blades) | their vanilla clan culture was unlisted (`nord`, `vakken`) or the wrong side (`empire` is Dunland, Evil; `vlandia` is Rohan, Free) | an unhired company read Neutral or the opposite side |

### The fix

- `SideIds(FactionId, CultureId)` and the `ResolveSide(this IAlignmentService, SideIds)` extension (an extension, so the
  many test fakes of the two-string method keep working). `TAOM.Adapters.FactionSides` is the only builder:
  `Of(faction)`, `OfHero` (a clanless hero is its own entity, not the home town's realm), `OfCharacter`, `OfPlayer`.
- Carried through `IArmouryPlayerAdapter.Side`, `LordCourtesySnapshot.Side`, `LordRef.Side`, the execution seams (the patches call
  `FactionSides.OfPlayer()`; the relation service and the hook take `SideIds`), `TournamentEntrant.Side`, `TournamentHost.OwnerSide`, `WandererAllegianceService.Evaluate(..., SideIds)`,
  `ShouldWaiveMoralePenalty(SideIds, ...)`, and, as pure type migrations with the suite green before and after, the Tokens of Esteem
  facts, Good Deeds (`DeedSide` deleted) and the Bonds of Battle snapshot (four loose strings and two null-forgiving `!` gone).
- Four `alignment.json` rows: `ghilman` evil, `skolderbrotva` free, `forest_people` free, `eleftheroi` free. The Grey Company
  and the Corsair Blades needed no row: `spclans.xslt` gives them the clan cultures `arthedain` (Free) and `umbar`
  (Neutral), so the Corsair Blades are now Neutral, matching TAOM's Umbar. A clan row must be `free` or `evil`: a `neutral` entry is a no-op, because
  `AlignmentService.ResolveSide` falls through to the culture for a listed-Neutral id exactly as for an unlisted one.
- Gates: `ShippedClanSideCoverageTests` (LiveInstall; the real `spclans.xslt` over the installed clans plus the TAOM clans) and
  `SidePairScanTests` (an IL scan: every two-argument `ResolveSide` caller outside a reasoned allow-list, every `SideIds`
  constructor or copy outside the adapter and the record).
- The MCM hint for Marriage Alignment called Dunland Neutral; `alignment.json` lists `empire` as Evil. The hint and the
  Marriage Alignment overview now name the real Neutral cultures (Khand, Umbar, Shaghana, Abanissa). The hint carries no
  `{=key}`, so no localization row changed.

### Why part 1 did not catch it

The part 1 review fixed the Tokens of Esteem callers and named the others as a follow-up, so they stayed live until the
widened scope. The contract that `ResolveSide`'s two strings describe one entity lived in a doc comment and a lesson file,
not in a type, so each feature that needed a side rebuilt the pair from two reads and passed its own fixture.

### What proves it

The suite was green after each step, the new tests were written red first wherever a behavioural red was possible (the signature changes in Wanderer
Allegiance and the execution seams have only a compile-level red), and the scan gate fails on a regression of each shape. Final run after review
wave 2: `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` reported `Passed!  - Failed: 0, Passed: 22363, Skipped: 5, Total: 22368`
(the run before wave 2 reported Passed 22322, Skipped 5). Not proven in game: the live behaviour of a
Neutral-realm player or lord in each feature (the in-game steps are in `lord-courtesies.md` and `hard-questions.md`), and
the minor clan sides at runtime (the data gate proves the table, not the engine).

### Review wave 1 (2026-10-20)

A four-lens review of the uncommitted part 2 found these, all fixed in the same change:

| Finding | Fix | Why missed | Preventive action |
|---|---|---|---|
| The scan gate missed a target-typed `new(...)`, the shape the trunk bug had (`=> new(lord.KingdomId ?? "", lord.CultureId ?? "")`) | The two-string `ExecutionParticipant` constructor is private and its property is `FactionId`; the SideIds check also flags a target-typed `new(` in a declaration or member typed `SideIds` and `with {` on a `SideIds`; the docs say plainly that a target-typed `new(` passed straight as an argument stays invisible | The gate was a text scan written from the shapes its author could think of; a text pattern cannot know the type of `new(` | Wave 2 rebuilt the gate on IL (finding P1) and deleted `ExecutionParticipant` (P2); lesson `testing-qa.md`, "A text scan cannot see a target-typed `new(`" |
| `FactionSides` was public (its siblings are internal); `AlignmentServiceExtensions` lived in `SideIds.cs` | `FactionSides` is internal; the extension has its own file | Visibility was not compared with the sibling adapters | Reviewed at the gate; no automated check (the sibling convention is internal) |
| Two `GoodDeedsHeroAdapter` reads and the FromTheAshes kingdomless branch built the player's pair by hand | `FactionSides.OfPlayer()` and `OfHero(hero)`; the FromTheAshes snapshot carries one `SideIds`, and its allow-list entry is gone. A kingdomless player now reads the clan's id with the clan's own culture instead of the hero's culture | The plan classed these callers as correct today (each pairs a faction with its own culture), but they built the player's pair by hand with other reads | The IL scan keys on where a `SideIds` is built and `FactionSides` is the only builder |
| `PrisonerRecruitmentMoraleService` kept a private copy of `ResolveSide` | It calls `_alignment.ResolveSide(recruiterSide)` | The plan kept the helper's body so the existing fakes (`GetKingdomSide`, `GetCultureSide`) held | Wave 2 removed the last two copies (finding P7); the allow-list names why each remaining two-argument caller is right |
| `IWandererAllegianceService` had one implementation and no fakes | Deleted (ADR-002); the behavior takes the class | A touched file was not checked against ADR-002 (one implementation, no fakes) | ADR-002 is checked when a file is touched; no new gate |
| Comments said a mercenary is kingdomless, that the blood-feud tooltip and outcome cannot disagree, that "Dunland is Neutral", and named `Hero.MainHero` as a source | `StartMercenaryServiceAction.ApplyStart` sets `Clan.Kingdom`, so only independent and enlisted players are kingdomless; the tooltip and the outcome can differ when the kill destroys the victim's clan; Khand is the Neutral culture, Dunland (`empire`) is Evil | The comments stated engine behaviour that was not read from the decompile | Lesson `campaign-mechanics.md`, "A mercenary contract sets `Clan.Kingdom`"; read the action with `taom-src` before writing what a status does |
| The data gate found its ModuleData by walking up from the test binary, mislabelled its kingdom-clan test, and did not guard the table's keys | `RepoPath`, a renamed test, a check that a kingdom clan's own pair reads its kingdom's side, and a typo guard that every `alignment.json` key is a loaded culture, kingdom or clan id | The first draft located the module data itself instead of using `RepoPath` | `RepoPaths` is the helper for repo files |

#### Minor-clan cultures

Done: the Grey Company and the Corsair Blades carry `arthedain` and `umbar` as clan cultures, with their two rows removed.
The data gate pins both and the intended side of the Corsair Blades is Neutral.

Then, with the maintainer's "fix all": the clans' troops and leaders now read the clan's side too.
- Troops: `lords.xslt` retags `legion_of_the_betrayed_tier_*` to `arthedain`, `company_of_the_boar_tier_*` to `umbar`,
  `eleftheroi_tier_*` to `sturgia` and `brotherhood_of_woods_tier_*` to `empire`. The route needs no `SubModule.xml` row:
  `lords.xslt` runs over the whole accumulated `NPCCharacters` document.
- Leaders: cloned, not retagged. The vanilla `spc_*_leader_*` templates are the `lord_templates` and
  `rebellion_hero_templates` of the `empire` (Dunland) and `vlandia` (Rohan) cultures, which `spcultures.xslt` passes
  through, and a hero keeps its template's culture, so a retag would have spawned Arthedain, Umbar, Dunland or Beorning
  heroes for Dunland and Rohan companion clans and rebellions (the class `CultureLordTemplateTests` records).
  `lords.xslt` emits `taom_<id>` beside each of the 16 originals (new id, clan culture, everything else identical),
  and `spclans.xslt` points the four clans at the clones. The originals and the culture lists are untouched.
- Gate (red first, 28 troop and leader rows opposed to their clan): troops and named leader templates never read the
  opposite of the clan's side, every named template resolves to an `NPCCharacter`, clones equal their originals except
  id and culture, originals stay vanilla. Neutral troops (`darshi`, `nord`, `vakken`) pass: they never desert.
- Saved games keep the culture a hero was created with; leaders created later use the clones; troops change at once. The clan's own culture
  and template list are NOT refreshed by a load: see wave 2, finding D1 (`MinorClanMigration`).


### Review wave 2 (2026-10-21)

A second four-lens review of the uncommitted part 2 (lenses 3, 4, 6 and 7) found these. The maintainer's instruction was to fix all of them, with no follow-ups.

| Finding | Fix | Why missed | Preventive action |
|---|---|---|---|
| D1 (HIGH): an old save kept the vanilla `Clan.Culture` and the vanilla `spc_*` template list, because a load skips the Factions file, while the troops always reload retagged. The Grey Company (clan culture `empire`) preferred Dunland contracts while its `arthedain` troops deserted under Dunland | `MinorClanMigration`: a pure service decides from raw facts, an adapter writes `Clan.Culture` and `Clan.MinorFactionCharacterTemplates` (both public members, no reflection), a behavior runs it on `OnGameLoadedEvent`. Idempotent, no RNG, one INFO line per clan changed; a data test pins its targets to the shipped data | The first doc claimed the clan culture "changes at once" in every campaign, which is true of a new one only | Lesson `campaign-mechanics.md`, "A save keeps `Clan.Culture` and the template list"; a co-op row in `coop-interop.md` |
| D2 (MED): the troop retag leaked the 12 vanilla troops into the Custom Battle pickers of Arthedain, Umbar, Dale and Dunland | The existing CustomGame-only filter stylesheet marks the four clans' `*_tier_*` troops obsolete (no new `SubModule.xml` line); `CustomBattleTroopFilterTests` runs it | `lords.xslt` is registered for CustomGame as well, and the retag was not checked against the Custom Battle pickers | `custom-battles.md` names the filter's second job |
| D3 (MED): `tools/complete_lords_xslt.py --apply` regenerates `lords.xslt` and dropped the #957 block | `MINOR_CLAN_CULTURES` constant emitted by `generate_xslt`; a test that the generator emits it and a test that the shipped file carries it verbatim | Only the #758 retag was a generator constant; the new block was added to the file | Lesson `build-tooling-workflow.md` (a repeat); `tools/README.md` states that `--apply` rewrites the whole file |
| D4 (LOW): comments did not say that a clone is saved by id, nor what else the clan culture decides | `lords.xslt` and `spclans.xslt` comments | The comments were written for the data change, not for the saved state | The comments sit beside the templates |
| P1 (design): the scan gate was a text scan with a named blind spot | `SidePairScanTests` reads the IL through `IlCallScanner`, keys callers by outermost type, requires the allow-list and the observed callers to match both ways, and has a control test that compiles each banned shape. Proof: with the new gate green, a temporary `Use(new(a, b))` in Main turned the IL test red while the old text test stayed green; the probe was removed and the text version deleted | Wave 1 patched the text scan instead of replacing its method | Lesson `testing-qa.md` |
| P2 (design): `ExecutionParticipant` duplicated `SideIds` | Deleted; `IExecutionRelationService` and `IOnExecutionAction` take `SideIds` | The struct predated `SideIds` and wave 1 only made its constructor private | One pair type |
| P3 (design): `IPlayerContextAdapter.GetPlayerSide()` re-wrapped `FactionSides.OfPlayer()` | Dropped; the patches call `FactionSides.OfPlayer()`; the Honor patch takes the hook only | The adapter method was added in part 2 as a pass-through to `FactionSides` | `FactionSides` is the one builder |
| P4 (tests): duplicate and unreachable tests and stubs | Deleted after checking each against its twin | Tests were added per caller, without comparing them with the matrix tests | Compare a new row with the existing matrix before adding it |
| P5 (tests): the data gate transformed the same input in two overloads, kept redundant asserts and re-ran every loader per test | One `Transform`, cached loaders (`Lazy<T>`), the redundant asserts and the retag test removed, one test renamed to say what it enforces | Incremental additions across two waves | None needed |
| P6 (docs): the same gate was described in four places | Each gate is described once, in the Tests section of `alignment-aware-execution.md`; elsewhere one line names it | Each fix added its explanation where it applied | One owner doc per gate |
| P7 (design): `CaravanTradeService` and `MomentumEnrollmentService` kept private copies of the `ResolveSide` fallback | They call `IAlignmentService.ResolveSide` with the id and the culture of one realm; their tests stub `ResolveSide`; both are in the scan's allow-list with a reason | The plan classed them as correct today (the fallback equals the service's), so they stayed copies; wave 1 removed only the one in PrisonerRecruitment | The IL allow-list |
| T1 (tests): `FactionSides` and the empty-pair path of Wanderer Allegiance had no test | `FactionSidesTests` (null branches, a faction without a culture, no campaign, source pins that `OfHero` reads the clan and never `Hero.MapFaction`), and a Wanderer Allegiance test with `SideIds.None` | `FactionSides` had no direct test; its callers' tests fake it | Tests beside the builder |
| T2: stale names and comments after the null-to-empty move | One test renamed, two comments rewritten | Search and replace changed the arguments, not the words around them | None needed |
| Docs: stale counts and lines (key counts, template counts, test counts, "kingdom-then-culture", a missing Arthedain row, stale Key Files) | Re-measured and rewritten | Counts were copied between docs | Re-measure with a command; each count in a doc carries its command |

What proves it: red first for each code change. The migration tests did not compile before the service existed (CS0246 on the missing types); 6 of 11 Custom Battle filter tests failed before the stylesheet change; 3 of 5 generator tests failed (one failure, two errors) before the constant; 11 of 57 Caravan and Momentum tests failed before the production change; the IL scan went red on a temporary `Use(new(a, b))` probe in Main and green again once it was removed. The final suite run is quoted under "What proves it" above.
