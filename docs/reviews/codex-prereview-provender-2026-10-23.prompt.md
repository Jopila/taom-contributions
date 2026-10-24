# Codex pre-review: Provender-master (#908), 2026-10-23

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Scope is the uncommitted working tree of this
worktree on top of `c2988ac3d` (`git diff` and `git status`; the new files are untracked). Engine
source for v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\` (for example
`TaleWorlds.CampaignSystem.Actions.SellItemsAction.cs`); signatures come from the installed DLLs.
Mark anything you cannot prove from code as UNVERIFIED.

## The change

A new feature module, `Main/Features/Provender/` plus `Main/Adapters/ProvenderWorldAdapter.cs`. The
hero in the main party's Quartermaster role (`GetRoleHolder(PartyRole.Quartermaster)`, never the
player) takes standing orders in conversation (`hero_main_options`, hub `taom_provender_orders`):

1. **Food order.** On `SettlementEntered` and `OnSettlementLeftEvent` for the main party, in a town or
   a village with a trade town (never a castle, raided, under raid or at war with the player), each
   food type the market stocks is topped up to ceil(eaters / stocked types), bought in market order
   with the leader's gold through one `SellItemsAction.Apply` per type, sized to the stock and to
   gold / current price. Officer XP: Trade gold/100, Steward gold/200.
2. **Slaughter order.** On the daily tick, only below Low Food Days, one head at a time from the herd
   with the most to spare until the stores last Low Food Days again, keeping N heads per type. The
   adapter applies `InventoryLogic.SlaughterItem`'s yield (MeatCount meat, HideCount hides) to the
   party roster. 10 Steward XP per head.
3. **Morale.** In a settlement or a ready field camp: pay every soldier but the player (two MCM tiers,
   `GiveGoldAction.ApplyBetweenCharacters(player, null, gold)`, then `RecentEventsMorale +=`), or share
   out the ration from `provender/rations.json` (majority troop race, then leader culture, then
   default), from the stores first, buying any shortfall at the market, then morale and
   `SetDisorganized(true)`. Every morale write commits an open party screen (`DoneLogic(true)`).
4. Gates: MCM switch, `ShouldDeferToHost`, `IsDedicatedServer`, enlisted player, a valid officer.
   Both orders are saved per campaign in `SyncData` (`_taom_provender`, Dictionary<string,string>).
   Seven new MCM settings, counted as simulation-relevant (fingerprint counts bumped).

## Questions to answer with evidence

- Money: can the food order or a drink purchase leave the player with less gold than intended, or
  spend more than it reports? Is the per-unit price rise inside one `SellItemsAction` call handled?
  Is a gold reserve missing in a way that harms the player (wages)? Does any path double-charge?
- Settlement events: does entering AND leaving a settlement, an encounter, a siege, captivity, an
  enlisted or co-op state, or a village with no trade town and no bound town break or double-run?
- Slaughter: is the days-of-food arithmetic right for `GetNumDaysForFoodToLast` and `FoodChange`
  (zero or positive change, overflow), and does the roster write match vanilla's slaughter?
- Conversation: is the dialog wiring correct (tokens, priorities, conditions, clickable reasons,
  per-line text variables), and is the party-screen commit safe when the talk was not opened there?
- Save and lifetime: per-campaign behavior built with `new`, old saves, damaged entries; any engine
  object kept across campaigns in a singleton.
- Co-op: is any world write reachable on a client or a dedicated server?
- Any caller or registration outside the diff that breaks (DI graph, `FeatureModules`, settings
  fingerprint, co-op doc coverage).

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the
proving code or a reproduction. Say PASS for a question you checked and found sound.
