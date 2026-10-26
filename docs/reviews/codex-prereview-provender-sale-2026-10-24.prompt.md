# Codex pre-review: Provender-master remote selling (#909), 2026-10-24

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Scope is the uncommitted working tree of this
worktree on top of `ed3bb65cc` (`git diff` and `git status`; new files are untracked). Engine source
for v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\` (for example
`TaleWorlds.CampaignSystem.Inventory.InventoryLogic.cs`, `Helpers.InventoryScreenHelper.cs`).
Mark anything you cannot prove from code as UNVERIFIED.

## The change

The shipped Provender-master (docs/features/provender.md) gains remote selling. In a settlement or
a ready field camp (and in a camp only with no live encounter or map event), the hub line "I have
goods I need sold." lists the N nearest eligible towns (default 3): not the current town, not at war
with the player, not barring the player (vanilla access model), not besieged, gold at least 5,000,
not of the opposing alignment (both sides Free or Evil and different; Neutral trades with anyone).
The cut: max 30 falling to min 10 with the officer's Trade to 250, plus 5 per rank. Picking a town
ends the talk (`close_window`) and, on `ConversationManager.ConversationEndOneShot`, pushes an
`InventoryState` in Trade mode: `new InventoryLogic(town.Settlement.Party)`, `Initialize(new
ItemRoster(), main.ItemRoster, main.MemberRoster, isTrading: true, ...)`, a fee `IMarketData` that
mirrors `TownMarketData.GetPrice` but adds the value of this session's sold goods to the category's
store value and takes the cut off sell prices, and a `TownSaleListener` over the chosen town's gold.
On close (`DoneLogicExtrasDelegate`): if the engine did not call the listener's `SetGold` (outside
a settlement), the town is debited by the player's gold gain; the sold roster moves into the town's
stock; the party screen is committed. Files: `Main/Adapters/ProvenderSaleAdapter.cs`,
`Main/Features/Provender/ProvenderSaleService.cs`, `ProvenderSaleRules.cs`,
`Hooks/ProvenderSaleDialogLines.cs`, plus edits to the flow, module, behavior and settings.

## Questions to answer with evidence

- Gold: in every path (inside a town, a village or a castle; in a field camp; cancel; the town
  short of gold; a net buy-back) is the town debited exactly what the player gains, once? Can
  `DoneLogic` pay or charge any other party (encounter, current settlement) instead?
- Items: do sold goods always reach the town, with modifiers, and never duplicate or vanish
  (cancel, buy-back, a crash path)? Does any party-screen snapshot later revert them?
- Prices: does the fee market data equal what vanilla's town trade screen would charge, unit by
  unit, minus the cut? Any exploit (sell, buy back, sell again; price not falling)?
- State and timing: is pushing an InventoryState from ConversationEndOneShot safe for a map
  conversation, a party-screen conversation and a settlement-scene (mission) conversation? Does the
  conversation's own state teardown pop the new state, or leave a dangling one?
- Gates: is any path reachable for a co-op client, a dedicated server, an enlisted or captive
  player, or a town the player cannot enter? Does the camp gate cover every way DoneLogic picks a
  counter-party (`CurrentMobileParty` comes from `PlayerEncounter`)?
- Any registration, settings-count or test that breaks; anything that names a client-only assembly
  (ADR-013, ServerJitGateTests).

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the
proving code or a reproduction. Say PASS for a question you checked and found sound.
