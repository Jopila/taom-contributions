# Tokens of Esteem

## Overview

The player can give a gift to the companions in the party and to lords, and the hero thinks better of the player for it. A companion takes goods from the player's inventory or a purse of gold; a lord takes goods only. What a gift is worth depends on the hero: Dwarves prize jewelry and silver, Elves wine, velvet and linen, the Rohirrim horses, orc chiefs meat and weapons, all read from a data file. A saved cooldown stops goods being turned straight into relation.

## Why This Exists

- **Vanilla behavior:** relation comes from quests, battles and barter. There is no way to simply give a hero something.
- **TAOM requirement:** Middle-earth has clear tastes, and giving is a natural courtesy. TAOM had gold-transfer adapters for wages and start gold, and no gift mechanic.
- **Without this feature:** the only way to win a companion or a lord is a quest or a campaign, and the cultures' tastes stay flavor text.

## Architecture

### Design Challenge

- **The list is a repeatable player line** (`AddRepeatablePlayerLine`, `ConversationSentence.SetObjectsToRepeatOver`), TAOM's first, modelled on vanilla's fief grant in `CompanionRolesCampaignBehavior`. Three engine rules shape it (`ConversationManager.SetDialogRepeatCount`, v1.5.5): the page size is 5 rows; paging starts only above 6 rows, so exactly 6 rows are all shown and a 7th row makes pages of 5 with the "continue listing" line; and a row's condition must return true for the row to be listed (it also writes the row text, `{=!}{GIFT_ROW}`, through `SelectedRepeatLine.SetTextVariable`). The continue line leads back to `taom_gift_asked`, so the hero asks again.
- **The catalog is keyed by item category** (`ItemObject.ItemCategory.StringId`), not by item, so renaming an item keeps its place. The categories are the ones the engine creates in code (`DefaultItemCategories`), so the file is checked against the live registry at load.
- **Purses use their own band.** `IGoldGiftAdapter.GiveGoldToHero` mints gold, and a purse has to leave the player, so the gold moves with `GiveGoldAction.ApplyBetweenCharacters(player, hero, ...)` behind `IGiftHeroAdapter.PayGoldToHero`. A purse is worth one point per 500 gold, at most 10, so it is not priced like an item of the same value.
- **A gift moves the party roster.** A conversation opened from the party screen returns to a screen that still holds its pre-conversation snapshot. Once the item has left, a later troop change followed by Cancel or Reset would refill the roster from that snapshot and hand the item back, so the item adapter commits the open party screen after the item leaves (`lessons/adapters-taleworlds-api.md`, 2026-10-19).
- **The cooldown key is the relation owner.** With Personal Relations off a change lands on the clan leader, so two kinsmen share one relation. The hero adapter reads the owner with `DiplomacyModel.GetHeroesForEffectiveRelation` and the ledger is keyed on it ([relations.md](relations.md), "Rules for callers").
- **Alignment must not use `AreEnemyAlignments`:** it counts Neutral as everyone's enemy, so every Khand or Umbar lord would refuse.
- **Day numbers come from a double.** `(int)double.NaN` and a cast past `int.MaxValue` are both `int.MinValue`, so the hero adapter accepts only a finite day from 0 to `int.MaxValue` (`GiftHeroAdapter.WholeDay`) and otherwise hides the line.

### Solution Approach

`GiftService` is stateless and decides every rule from raw facts the adapters produce (`GiftPlayerFacts`, `GiftRecipientFacts`, `HeldGood`). It takes the campaign's `GiftCooldownLedger` on each call and stamps it once the item or gold has left. The ledger belongs to `GiftConversationFlow`, which also holds the state of one conversation (the branch chosen, the hero's answer, the size of the list, the last outcome); the flow runs without the game, so its three catch blocks and its gates are tested with fakes. `GiftBehavior` binds the dialog lines to the flow at the session start and saves the ledger in `SyncData`; the module builds it with `new` for every campaign and owns save data. The notice after a change is vanilla's own "your relation with X increased or decreased" line.

### Component Diagram

```
gifts/gift_catalog.json
        |
GiftCatalogProvider (validates against ItemCategory, race and culture registries)
        |
   GiftService  <-- IGiftSettingsProvider (MCM "Tokens of Esteem")
   /   |   \  \
  /    |    \  IRelationGrantService (landed change), IAlignmentService
 Hero  Item  PartyItemRoster
adapters
        ^
GiftConversationFlow (ledger, gate, last outcome)
        ^
GiftBehavior + GiftDialogLines  -->  IGiftConversationAdapter (repeatable line statics)
```

## Rules

**The line.** "I have a gift for you." on `hero_main_options` for a companion in the player's party, and on `lord_talk_speak_diplomacy_2` for a lord who is not a child, not a prisoner and not in the player's own clan. No line at all when the feature is off (no fact is read then), a co-op client is asking, a prisoner, or the lord is at war with the player. Both lines open `taom_gift_asked`, where the hero answers; the list ends with "Never mind." at priority 1.

**Order of the gate.** Hidden (feature off, wrong kind of hero, prisoner, at war), then the cooldown (the line shows, greyed, with the days left), then a lord below relation -50 (refuses with "I will not take gifts from you." and takes 1 relation, once per ask; vanilla's notice shows only if the change landed), then a Free lord against an Evil player (refuses, no penalty; Neutral never refuses). A hostile companion still accepts.

**Gain.**

| Step | Rule |
|---|---|
| Companion base | By the item's value: under 100 gold 1, from 100 gold 2, from 300 gold 3, from 600 gold 4, from 1000 gold 5 |
| Purse base | One point per 500 gold, at most 10: 500 gold is 1, 2000 is 4, 5000 and over is 10 |
| Lord base | The catalog's `lordBase` for the item's category, 1 to 10 |
| Taste | The highest `bonus` among the groups that list the hero's race or culture (no stacking), for goods only |
| Scaling | Always on. Above relation 0, times (90 minus relation) / 90, rounded half away from zero: 0 from relation 90, and a 1-point gift still works at 45 and stops at 46 |
| Cap | The most one gift asks for (default 15). Your Charm can add to what lands, so the relation you see rise can exceed the cap |

A gift that would add 0 is listed greyed ("This would not improve your relation."). A purse the player cannot cover is greyed too.

**Payment.** One item from the **unmodified** stack (a "Sharp" or "Battered" copy is never listed; listing modified stacks is a follow-up), or a purse of 500, 2000 or 5000 gold (MCM) from the player to the companion. Then the cooldown starts, then `IRelationGrantService.Grant` runs, and vanilla's relation line shows only when something landed.

**Cooldown.** Per relation owner, the campaign day of the last gift, 7 days by default. It is saved. A refusal does not start it; a finished gift does. A stamp from the future (a damaged save) counts as over.

**Backing out costs nothing.** The "backing out costs 1 relation" rule in the issue was dropped by the maintainer: it punished browsing the list.

## Configuration

### Config File: `Main/_Module/ModuleData/gifts/gift_catalog.json`

One element of the file's `entries` array:

```text
{ "category": "jewelry", "lordBase": 8,
  "tastes": [ { "races": ["dwarf"], "cultures": [], "bonus": 4 } ] }
```

| Field | Type | Description |
|-------|------|-------------|
| `category` | string | An item category id (`ItemObject.ItemCategory.StringId`). Horses and weapons are categories too |
| `lordBase` | integer 1 to 10 | What a lord values the category at |
| `tastes[].races` | strings | Race names (`skins.xml`). A hero of any listed race matches |
| `tastes[].cultures` | strings | Culture ids. A hero of any listed culture matches |
| `tastes[].bonus` | integer 1 to 10 | Added to the gift's worth when the group matches |

At load every category is checked against the item categories, every race against `IRaceManager`, every culture against the cultures. An unknown id is dropped with one log line (a race or culture from its group, a category with its entry); a group left naming nothing, a number out of range or not an integer, a blank or repeated category drops its group or entry, each with its own line. Every drop is counted and one summary warning, `N dropped`, follows when any happened. The file is read once the campaign is up and kept for the process, so **an edit needs a full game restart**, not a new campaign.

### Current Values

| Taste | Goods (lord base) | Who prizes it (bonus) | Read from |
|---|---|---|---|
| Dwarves | `jewelry` (8), `silver` (6) | race `dwarf` (+4) | race: live LOTRLOME_Armory `skins.xml`; categories: `DefaultItemCategories` |
| Elves | `wine` (6), `velvet` (5), `linen` (3) | race `elf` (+4, +4, +3) | same |
| Rohirrim | `sumpter_horse` (2), `horse` (4), `noble_horse` (9) | culture `vlandia` (+3, +4, +6) | `spcultures.xslt` renames `vlandia` to Rohirrim |
| Orc chiefs | `meat` (3), `melee_weapons_3` (5), `_4` (7), `_5` (9) | races `orc`, `uruk`, `uruk_hai`, `pale_uruk`, `dg_uruk`, `goblin`, `berserker` (+3 to +5) | live Armory `skins.xml` |
| Neutral goods | `salt`, `beer`, `cheese`, `butter`, `fur`, `leather`, and `war_horse` (2 to 6) | none | |

`war_horse` carries no Rohirric taste on purpose: all 22 Armory creature mounts (wargs, spiders, elk and the rest) are item category `war_horse`, so the taste would make a warg a horse-lord's treasure, and a test pins it. The ids `empire` (Dunland) and `battania` (Khand) are not what they look like; read them from the data.

### MCM group "Tokens of Esteem" (GroupOrder 62)

All read live; none needs a restart. A hand-edited `TAOM.json` is clamped by `GiftSettingsProvider`. Scaling is not a setting.

| Setting | Default | Range |
|---|---|---|
| Enable Tokens of Esteem | on | |
| Days Between Gifts To One Hero | 7 | 1 to 60 |
| Most Relation One Gift Asks For | 15 | 1 to 30 |
| Free Lords Refuse Gifts From Evil Players | on | |
| Small / Medium / Large Gold Purse | 500 / 2000 / 5000 | 500 to 5000 each (one to ten points of 500 gold) |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TokensOfEsteem/GiftService.cs` | Every rule: gate, gain, options, give |
| `Main/Features/TokensOfEsteem/GiftConversationFlow.cs` | One conversation's state, and the cooldown ledger |
| `Main/Features/TokensOfEsteem/GiftCooldownLedger.cs` | Relation owner id to last gift day; the saved shape |
| `Main/Features/TokensOfEsteem/GiftCatalogProvider.cs` | Loads and validates the catalog |
| `Main/Features/TokensOfEsteem/GiftSettingsProvider.cs` | The clamped MCM values |
| `Main/Features/TokensOfEsteem/Hooks/GiftBehavior.cs`, `GiftDialogLines.cs` | The thin session boundary and the line tree |
| `Main/Features/TokensOfEsteem/TokensOfEsteemModule.cs` | Registrations, `OwnsSaveData` |
| `Main/Features/TokensOfEsteem/Models/` | Facts, gate, option, outcome, catalog records |
| `Main/Adapters/GiftHeroAdapter.cs`, `GiftItemAdapter.cs`, `GiftConversationAdapter.cs` | Engine boundary |
| `Main/_Module/ModuleData/gifts/gift_catalog.json` | The catalog |

## Dependencies

- `IRelationGrantService` (Relations): the relation change, returning what landed.
- `IAlignmentService` (Execution): `ResolveSide` for the lord and the player.
- `IPartyItemRosterAdapter` (Enlistment registration): removes one item from the unmodified stack.
- `IRaceManager` (Core): race names, race id to name.
- `ICoopSessionProvider` (CoopInterop): a client sees no gift line.

## Localization

`taom_gift_*`, all in `taom_module_strings.xml`: `offer`, `refuse_hostile`, `refuse_alignment`, `empty`, `prompt`, `deferred`, `more`, `back`, `thanks_suited`, `thanks`, `failed`, `cooldown`, `cannot_afford`, `no_effect`, `row_good`, `row_purse`. Worded without a gender. The relation line is vanilla's (`str_your_relation_increased_with_notable` and `_decreased_`), so TAOM owns no key for it. The twelve languages hold seeded ids with the English text until the paid translation run.

## Tests

- `TAOM.Tests/Features/TokensOfEsteem/GiftGainTests.cs`: the companion value bands at each edge, the purse band, the scaling at relation 0, 45, 46, 90, the cap and the zero-cap exit, extreme inputs, the monotone property over every relation and cap, the taste lookup.
- `GiftServiceGateTests.cs`: each hidden case (feature off reads no fact, prisoner, own-clan lord), war beating every other rule, the cooldown inside and after it, kinsmen sharing a relation owner's cooldown, the -50 edge, the alignment table over the real `AlignmentService` and the shipped `alignment.json` (Neutral included), hostility before alignment, the charge made once by `Ask` and its notice only when something landed.
- `GiftServiceOptionsTests.cs`: the list for a lord and a companion, purses, taste by race and culture, the cap, blocks, modified stacks, an empty catalog, no player facts.
- `GiftServiceGiveTests.cs`: one item removed, gold moving, short gold, the fresh re-read, the landed change and the notice, the stamp on the relation owner, the early exits, the real `RelationGrantService`.
- `GiftConversationFlowTests.cs` (no game needed): the gates the lines call, the list hand-off, the pick, the ledger, the co-op deferral and the three catch blocks.
- `GiftCatalogProviderTests.cs`: each validation rule and its exact warning count, caching, the registry not ready, and the shipped file loaded with nothing dropped (against a frozen race list, and against the installed `skins.xml` when the game is there).
- `GiftSettingsProviderTests.cs`: clamps, the purse range derived from the band, the slider range and the hint numbers tied to the provider constants.
- `GiftCooldownLedgerTests.cs`, `GiftHeroAdapterTests.cs` (the day cast), `GiftBehaviorTests.cs` (the saved ledger, the explanation texts), `TokensOfEsteemWiringTests.cs` (the module, the line ids, priorities, consequences and tokens, the English rows and the twelve language files).

## In-game Checklist

Setup once: start a campaign with cheat mode on. The console command `campaign.add_hero_relation [Hero] | [Value]` sets up a relation. Carry a few catalog goods bought at markets (jewelry, wine, fur, meat, a horse, a spare warg or other creature mount if you have one).

1. **Matching and non-matching, companion.** With a companion in the party (a Dwarf if you can, else any), open the talk and choose "I have a gift for you." Give a jewelry item, then wait out the cooldown (or use a second companion) and give a fur. Both are taken, the inventory loses one of each, the Dwarf's jewelry earns more, the thank-you line for the jewelry reads "You know what I like. Thank you.", and vanilla's relation line shows once each.
2. **Lord on the diplomacy branch.** Be at peace with a kingdom. Meet one of its lords, choose "There is something I'd like to discuss.", then "I have a gift for you." The list shows only catalog goods you carry, no purses. Give one item: the thank-you comes and relation rises.
3. **Cooldown and save.** Gift the same hero again at once: the line is greyed and names the days left. Save, reload: still greyed. Advance 7 days: it works again.
4. **War.** Meet a lord of a kingdom at war with you (declare war on one, or catch a party in a siege): no gift line at all.
5. **Relation below -50.** `campaign.add_hero_relation [Lord] | -60`, then choose the line: "I will not take gifts from you.", relation 1 lower, vanilla's decrease line once, nothing taken. At relation -100 the 1 point cannot land, so no relation line shows.
6. **Alignment.** As a player siding with the Shadow (an Evil kingdom or culture), choose the line with a Free lord (Gondor, Rohan, Rivendell): refused, relation unchanged. A Khand or Umbar lord, or a Neutral player, is never refused. Turn "Free Lords Refuse Gifts From Evil Players" off in MCM: the Free lord takes the gift.
7. **Gold purse.** With a companion, choose a purse of 2000: your gold falls by 2000 and the companion's gold rises by the same amount (UNVERIFIED where the game shows a companion's gold; the TAOM log line names the sum). A purse larger than your gold is greyed with "You do not carry that much gold."
8. **Party screen.** Open the party screen, talk to a companion from it, give an item, move one troop, press Cancel: the item stays gone.
9. **High relation.** `campaign.add_hero_relation [Hero] | 90`: every row is greyed with "This would not improve your relation."
10. **Paging.** Carry 4 catalog goods and talk to a companion (4 goods plus 3 purses is 7 rows): the first page shows 5 rows and "Let me look through my things again." Carry 3 goods (6 rows): all six show and there is no continue line.
11. **Lord tastes.** A `vlandia` (Rohirrim) lord with a horse, a Dwarf lord with jewelry, an `uruk_hai` lord with meat: each earns more than a lord of another race or culture with the same item (the TAOM log line ends `suited=True`).
12. **A creature mount is not prized.** Give a `war_horse` creature mount (a warg or elk) to a Rohirrim lord: `suited=False` in the log.
13. **Two kinsmen, Personal Relations off.** Turn Personal Relations off in MCM, gift one lord, then talk to a kinsman of his clan: his line is greyed. With it on, the kinsman can be gifted.
14. **New campaign.** Start a second campaign in the same session without restarting: no cooldown carries over.
15. **MCM live.** Change "Days Between Gifts To One Hero" mid-campaign: the next gate uses it, no restart.
16. **The log line.** `[Gifts] <hero id> took <item or N gold>: asked A, landed L, suited=...` after every gift, and `[Relations] ... asked ..., landed ...` from the seam.
17. **The notice.** Vanilla's "Your relation with X increased/decreased" line shows once per change, and not at all when nothing landed.
18. **No line.** A captive lord in your dungeon and a lord of your own clan: no gift line on the diplomacy branch.
19. **Co-op, two machines.** The client sees no gift line; the host can give.

## How to Change a Rule

1. A new taste: add a group to the category in `gift_catalog.json` (race names from `skins.xml`, culture ids from the culture files) and restart the game. A bad id shows in the log as one line and a summary.
2. A new gift good: add an entry for its item category. The category must exist in `DefaultItemCategories` or the item data.
3. A different band, scaling or cap: `GiftService.CompanionBase`, `PurseGain` and `ComputeGain`, with the matching rows in `GiftGainTests`. The cap, cooldown and purses are MCM; the purse range follows `PurseGoldPerPoint` and `PurseMaxPoints`.

## Known Limits

- No thank-you sound (the issue mentions one; nothing was added). If one is added it plays on the main thread.
- The ledger holds a row per relation owner ever gifted and never prunes. It is a few bytes each.
- A lord's base is one number per category; companions price by the item's own value, so a rich item is worth more to a companion than to a lord who values the category low.
- Modified stacks (a "Sharp" or "Battered" copy) are never listed: the roster adapter removes only the unmodified stack. Listing them needs a modifier-preserving removal and is a follow-up.
- Gifts are hidden for a co-op client and for any peer running a co-op mod TAOM cannot probe (`ShouldDeferToHost`); a co-op host can give. See [coop-interop.md](coop-interop.md).
- No gift line while besieging or besieged, or on a raft or in naval auto-travel: the lines sit on vanilla's conversation tokens, which those states do not reach. Not checked in game.

## Changelog

- 2026-10-19: first version (#863). Review fixes the same day: the cooldown is keyed on the relation owner and stamped by the service, purses have their own band (one point per 500 gold, at most 10, range 500 to 5000), the scaling switch is gone, prisoners and own-clan lords get no line, modified stacks are skipped, the catalog counts every drop, the notice is vanilla's, and the wording is gender neutral.

## GitHub Issue

- **Issue:** #863: Gifts: give trade goods or gold to companions and lords for relation
- **Status:** Open
