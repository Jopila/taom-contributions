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
