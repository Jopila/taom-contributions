# RCA: War of the Ring war rule and campaign balance pass (#818, 2026-10-18)

## Top-line

A 200-day balance simulation (ArmorySim 0.1, a standalone tool by yotthani) showed the free peoples falling from 47 to
41 men per 100 evil men while winning 81 % of their battles, and Arthedain losing 66 % of its men. Mike decided:

- no war at campaign start;
- days 0 to 30 a build-up with no war at all, the Isengard war (Isengard and Dunland against Rohan) on day 30, the full
  War of the Ring on day 44;
- Arthedain's lord template raised from a max sum of 83 to 200;
- new army target orders for Dunland, Gundabad and Dol Guldur.

The change removes every start war from the kingdom data and gates every kingdom-against-kingdom war declaration by the
War of the Ring phase (`WarOfTheRingService.GetWarVeto`), with a daily sweep for wars the phase forbids.

`/deep-review` ran:

- an architect design pass before any code;
- eight `deep-reviewer` lenses in two waves (1, 2, 5, 7; then 3, 4, 6, Tooling);
- one convergence pass.

**No engine incompatibility**: every patch target and parameter name resolves on the installed v1.5.5 DLLs, and the
binding gate pins them now. No finding was HIGH. Every finding is fixed in this change, decided by Mike, or recorded
below as a follow-up.

## Mike's decisions

| # | Question | Answer |
|---|---|---|
| D1 | Wars at campaign start | None; "Day 0 to Day 44 there should be no wars, period", with the day 30 Isengard war kept as the one exception |
| D2 | Arthedain's template (the review found 83 was a deliberate design, not an oversight) | Raised to 200, the generator owns it |
| D3 | Advanced Start scenarios that open with a war | Hidden while the War of the Ring is on; the Mercenary start stays |
| D4 | A player's own hostile act before day 44 | May start a war (phase vetoes lifted; Permanent and same-side still block); AI wars stay blocked |
| D5 | Independent clans | Outside the rule (kingdom against kingdom only) |
| D6 | Same-side pairs before day 44 | Checked before the phase, so they read "same side" and rank below real enemies |
| D7 | Enlistment pulling the liege into the Rohan war early | Allowed as a player act, documented |
| D8 | Founding a kingdom before day 44 | A player act: the clan's wars carry over |
| D9 | One-clan player kingdom call-to-war notice | Patched now |
| D10 | Issues | Main #818, follow-ups #819 and #820 |

## Findings

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | MED | The first data edit set `isAtWar="false"` on five start-war rows and kept `value="-1"`; `Kingdom.Deserialize` declares war for any negative value, so a new game still started with five wars | Architect design pass | data, engine | The attribute's name was taken as its meaning; `docs/modding/kingdoms.md` already said "`isAtWar="false"` does nothing", and AGENTS.md "Research first" was not applied to data | `value="0"`; `ShippedKingdomDataTests` fails on a negative value or `isAtWar="true"` in any ModuleData file and on the transform output; lesson below |
| 2 | MED | The Arthedain template was retargeted by hand, inside the `TAOM-ARTHEDAIN-PT` region `generate_arthedain_troops.py` owns; its next `--apply` would have restored 83. The same edit was reported to Mike as "a missed step" when the generator's comment called 83 deliberate | Docs pass | data, tooling, evidence | **Third repeat in three days** of "check who owns the region" (lessons 2026-10-16 and 2026-10-18); the check is manual and was skipped. The "missed step" claim was written before reading the generator | The generator carries the 200 values; `test_generator_regions_in_sync.py` runs every region owner's `--check` in `/verify` (proven red on a hand edit); Mike told the 83 was deliberate (D2) |
| 3 | MED | `generate_new_faction_kingdoms.py` still wrote Arthedain's day-one war; its `--check` failed against the data fix | XML lens | data, tooling | Same as 2: the data was fixed, its writer was not | Generator fixed; covered by the new gate |
| 4 | MED | AI kingdoms paid 200 influence for war proposals the rule then cancelled (charge in `Kingdom.AddDecision` precedes `IsAllowed`) | Engine, Data flow | design | The veto sat on the final action; nobody traced the stages before it that charge a cost | `TaomDiplomacyModel.GetScoreOfDeclaringWar` subtracts a penalty for vetoed pairs; lesson below |
| 5 | MED | Call-to-war offers that could never lead to a war reached the player; declining cost 50 relation, accepting did nothing | Data flow, Design | design | Same as 4 | `IsAllowed` prefixes on both call-to-war decisions; a prefix on `OnCallToWarAgreementProposedByPlayerKingdom` (D9); the `StartCallToWarAgreement` prefix kept as the backstop |
| 6 | MED | A player who rules a kingdom got a battle with no war before day 44, and the crime rating re-applied -10 relation daily for weeks | Engine, Data flow | design | Same as 4, on the player side | D4: player-caused details lift the phase vetoes, so the war latches once |
| 7 | MED | Advanced Start war scenarios and the Mercenary start declare through `FactionManager.DeclareWar`; the sweep ended their wars on day 1 | Engine, Data flow | design | The design listed the raw callers but its consequences were not put to Mike | D3; the sweep keeps the player kingdom's phase-only wars |
| 8 | MED | The sweep ran in `OnSessionLaunched`, which on a load runs before `MapEventManager` and `SiegeEventManager.OnAfterLoad`; making peace there can disturb live battles and sieges | Data flow | lifecycle | The design pass recommended session launch to catch new games early; the load order was not traced | Daily tick only; lesson below |
| 9 | MED | Before day 44 a same-side pair read as phase-vetoed, so the Mercenary start could pick a same-side neighbour whose war the sweep then ended | Design | design | The veto order was written phase first, and the score tiers were reasoned about only for Full War | D6: same side checked before the phase; RED tests |
| 10 | MED | The Harmony prefixes bind engine parameters by name (`declareWarDetail`, `calledKingdom`, `proposedKingdom`) and read decision fields; nothing pinned them | Completeness | test | The binding gate checks that targets exist, not their parameter names (lesson harmony-il.md 392-402 exists; not applied) | `DiplomacyWarPrefixBindingTests` |
| 11 | MED | The model bodies branched (permission model, then the new score override) against gamemodels rule 4 | Standards, Convergence | standards | Each was written as "one more `if` beside the existing one"; the second was added after the standards lens had run | Both decisions moved into `AllianceActionHook`; models keep the boundary conversion |
| 12 | LOW | The score penalty was 100000 on an unmeasured "base scores are in the low thousands" | Orchestrator | evidence | A constant was sized from a guess | Vanilla's own "never" magnitude (1e7, 1e8 for Permanent and same side) |
| 13 | LOW | `ServiceDiplomacyAdapter.DeclareWarOn` returned true without checking the war landed | Data flow | data flow | Pre-existing; widened by the new veto | Returns the real state |
| 14 | LOW | The sweep counted `MakePeace` calls, not ended wars; tests duplicated cases and misnamed others; the data test read stylesheet markup, not the transform output | Standards, XML, Data flow | test | Written quickly beside the main work | Fixed; LiveInstall transform test |
| 15 | LOW | Coverage gaps: player-path WarEnded, War Ended with the feature off, exact phase-1 list | Completeness, Convergence | test | Branches added late | Tests added |
| 16 | LOW | NavalDLC's `nordinvasion` war scenario was not hidden | Convergence | completeness | Only SandBox's provider was read | Added to the hidden list |
| 17 | LOW | `harvest_literal_loc_keys.py` left a double blank line; `snapshot_api_surface.ps1` dropped the backlink footers | XML, Tooling | tooling | Pre-existing tool defects (#781, #702; rca-perf-integration-fixups-2026-10-04) | Fixed by hand here; follow-ups |
| 18 | LOW | Docs: stale start-war and Gundabad day-30 lines; one new doc sentence named `town_EW4` as Dunland's lead target | XML, Completeness, Orchestrator | docs | A docs pass described the diff from a misread | Corrected after checking the JSON |

## Root-cause patterns

**A veto on an action must also gate where the engine prices or offers that action (findings 4, 5, 6, 9).** Bannerlord
charges for a war before it declares one: AI influence when a proposal is created, a relation penalty for declining a
call to war, a crime-rating relation hit before each attempt, a map notice and a dialogue price. A prefix on
`DeclareWarAction.ApplyInternal` stops the war and leaves every earlier cost in place. Gate the earliest stage that
charges or shows something (the score model for the AI, `IsAllowed` for decisions), and keep the final prefix as a
backstop.

**Generated data has an owner, and a manual "check the owner" step does not hold (findings 2, 3).** Three cases in three
days. The new gate makes the owner's own `--check` part of `/verify`.

**Read the deserializer before trusting an attribute name (finding 1).** The engine reads `value` first; `isAtWar`
only adds a war.

## Why each agent missed what others found

- **Architect design pass:** found finding 1 and listed the raw `FactionManager.DeclareWar` callers. It did not price
  the earlier stages (4, 5) or the load order (8); its brief asked where wars start, not what they cost before they
  start.
- **Implementer batches:** built to the brief; the brief carried the design's blind spots.
- **Lens 1 (Standards):** ran before the score override existed (11, second half), which the convergence pass caught.
- **Lens 7 (XML):** found 3, but data-only; finding 2's generator was caught by the docs pass, which read the generator's
  comment while describing the change.
- **Lenses 2 and 5:** found 4 to 8; the overlap between them confirmed each other.
- **Lens 6 (Design):** found 9, which needed the score tiers and the Mercenary start's pick read together.
- **Lens 4 (Completeness):** found 10 and the coverage gaps; the binding-name lesson exists but no gate enforces it.

## Lessons appended

- `docs/reviews/lessons/campaign-mechanics.md`: a war veto gates where the engine prices or offers the war, not only the
  declaration.
- `docs/reviews/lessons/xslt-moduledata.md`: a negative relationship `value` declares war whatever `isAtWar` says; and
  the region-owner check is now a gate.
- `docs/reviews/lessons/state-lifecycle-save.md`: do not make peace or war in `OnSessionLaunched` on a load.

## Follow-ups (not defects of this change)

- #819: the War of the Ring switched off during Full War does not unlock peace.
- #820: phase transitions gated on `IsAuthority` alone run on every BannerlordTogether peer.
- #781 and #702: `harvest_literal_loc_keys.py` blank lines and raw newlines.
- Tooling:
  - `snapshot_api_surface.ps1 -Check` cannot pass while the docs carry a backlink footer;
  - `promote_borrowed_cultures.py` re-appends its block at the end, and Arthedain's start point in `taom_spcultures.xml`
    differs from `town_AN2` on the live map;
  - `rebalance_party_template_maxes.py` has no `--check` (Erebor and Mordor sit off target by design);
  - `raise_party_template_maxes.py` is an obsolete third writer.
- Efficiency:
  - `AllianceAdapter.FindKingdom` allocates a closure per call (the daily sweep makes about 1,500 a day in days 0 to 43);
  - `TaomSettingsProvider` is now read per AI war decision without the lazy accessor (`CampaignHotPathSettingsProvidersTests`).
- Old saves only: a call-to-war agreement made before this change can outlive the peace the sweep makes.
- The two sibling overrides in `TaomKingdomDecisionPermissionModel` still branch (pre-existing rule-4 debt).
- The MCM hints for the phase days do not say that no kingdom may declare war before the phase 1 day.

## Second change: evil party-size feats capped at 10% (item 2 of #818)

Mike approved "5 and 10 percent (max)": Goblin-town, Blue Craig and Misty Mountain Orcs from +40/+40/+30%
to +10%, Mordor, Isengard, Gundabad and Dol Guldur from +20% to +5%. Built test-first; the seven feat
descriptions changed only their number, edited in place in English, 12 language rows and 12 caches.
`/deep-review` ran lenses 1, 4, 5 and 7 in one wave; 2, 3 and 6 were not run for a value-only change
(lens 4 agreed).

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| S1 | HIGH | The character-creation culture stage and the faction screen read their party-size lines from `factionmap/factions.json`, and 13 of them (English, 12 languages, 12 caches) still said +40/+30/+20%. Mordor's card had said +10% since its feat moved to 20% on 2026-08-14. The feat descriptions that were edited are never displayed by TAOM | XML, Data flow, Completeness | data, completeness | **Repeat (third)** of the lesson "Touching cultural identity means also updating the CC faction-map page"; the change followed the feat's own strings and never asked where a player reads the number | 13 keys fixed by hand (the harvester would revert 5 Lindon strings); `make_new_factions_playable.py` literals fixed; a shipped-data test now compares every card's party-size percentage with the production `EffectBonus`; lesson marked gated |
| S2 | MED | The new production pin used an unanchored `Contains`, so `-0.05f` or `10.05f` passed; the Wave 1 pin had the same gap | Standards, Completeness | test | The expected tail was written without its left boundary | Anchored with `", "` in a shared helper, proven red on mutants |
| S3 | LOW | The two "band" tests in `CulturalFeatsServiceTests` read the hand mirror table and could never fail on a production change | Standards | test | Pre-existing design: the fixture sets `EffectBonus` by reflection from a copy | Deleted; replaced by a production test that every party-size feat is above 0 and at most 10% |
| S4 | MED | The feats also set evil garrison and patrol limits (a Goblin-town town garrison about 560 to 440, a Mordor town about 480 to 420); nothing said so | Data flow | docs | The change was framed as "party size" only | Documented in `cultural-feats.md`; whether party-size feats should reach garrisons is a follow-up for Mike |
| S5 | LOW | Docs kept the removed 20% floor in present tense and numbers derived from the old values (AI party-size table, the 40-203 band, the translator guide's examples) | Completeness, Data flow | docs | Derived numbers were not traced to their inputs | Updated or reworded |

Existing saves: the new values apply on load. Evil AI lords above the new limit are trimmed by the
TroopWeight shed on the first daily tick; a goblin or orc player's own party and garrisons lose a
quarter of their excess per day to vanilla desertion.
