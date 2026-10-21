# RCA: Namegiving, deep review, #858 (2026-10-20)

**Summary.** Namegiving (#858) lets a realm ruler rename a town, castle or village from the kingdom Fiefs panel
for influence, and gives four fiefs a lore name while the other side of the War of the Ring holds them. It uses
one UIExtenderEx mixin and one insert-only prefab patch, no Harmony and no reflection. A seven-lens deep review
of the uncommitted tree on `feat/settlements-buildings` (base `e7cdb2e96`) confirmed one HIGH finding (the
Rename block was inserted beside the action buttons, not inside them), two MEDIUM findings (the switch froze lore
names in place; a clan that changes kingdom with its fief never updated the lore name), and about fifteen LOW
items. Mike decided the switch behaviour: off restores the map names. Every confirmed finding is fixed in the
review's fix pass unless listed under "Not applied".

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `FiefActionsRenamePrefab` used `InsertType.Append` on `ListPanel[@Id='ActionButtonsContainer']`. UIExtenderEx's `Append` inserts the next sibling of the target (`PrefabComponent` :382), not its last child, so the block sat beside the Annex column in a horizontal row and outside the gamepad navigation scope of the action buttons. | UI placement | The design brief read "Append into" as "append a child", and the prefab test asserted the XPath and the type, not where the node lands. The same library has the inverse trap (`Child` defaults to the first position), which was recorded the day before. | `InsertType.Child` on `.../ListPanel[@Id='ActionButtonsContainer']/Children` with `Index => int.MaxValue`; the prefab test pins the type, the index and the XPath. A repo-wide test over every `PrefabExtensionInsertPatch` is proposed below. Lesson appended to `lessons/localization-ui.md`. |
| 2 | MED | With the switch off, `ReconcileCore` returned before the restore branch, so a lore name already applied stayed on the map for good. | State transition | The design treated "off" as "do nothing" and did not ask what "off" means for a name the feature already wrote. | Mike decided: off gives the lore fiefs their map names back at the next capture, load or change of realm; player names stay. `DecideLore` takes the switch as an input and owns every lore decision; tests pin both origins with the switch off and that a co-op session writes nothing. |
| 3 | MED | A clan that joins, defects from, founds or rebels against a kingdom keeps its fiefs, so their map faction changes with only `OnClanChangedKingdom` raised. The lore name of such a fief never changed side. | Event coverage | The design took `OnSettlementOwnerChangedEvent` as the only signal that a fief changed side. | A listener on `OnClanChangedKingdomEvent` reconciles that clan's settlements; a wiring test pins it. Repeat offender: `lessons/campaign-mechanics.md` "A clan that changes kingdom moves its fiefs with no owner-change event" (2026-10-14); that entry is strengthened. |
| 4 | LOW | The full reconcile pass ran on `OnGameLoadedEvent`, which fires only for a saved campaign, so a new campaign never ran it. | Lifecycle | The brief named the load handler from the "a throw loops the loading screen" rule and did not check which campaigns raise it. | The pass runs on `OnSessionLaunchedEvent`, which fires for new and loaded games, with one outer try. Repeat offender: `lessons/state-lifecycle-save.md` (the `OnGameLoaded` coverage entries at :592 and :752). |
| 5 | LOW | A lore name is saved as the raw `{=taom_namegiving_lore_*}` text in `PartyBase.CustomName`, so the key prefix and every row are a save contract. Nothing warned about it. | Save contract | No `SaveableTypeDefiner` and no `SyncData` are involved, so the names did not look like save data. | A warning on `LoreNames`, `KeyPrefix` and the module's `OwnsSaveData`, and in the doc's How-To. Repeat offender: `lessons/state-lifecycle-save.md` "A code-registered object id that a save references is a save contract" (Postern Gate, 2026-10-18); that entry is widened to engine-saved text. |
| 6 | LOW | Smaller items: dead members (`RenameTarget.Kind`, `RenameResult.Cost`, an unused `LoreRow.Text`); `ReconcileAllLore` enumerated `LoreNames.All` outside its try; the map-action gate was read once per target; a mixin constructor `Refresh()` duplicated the refresh UIExtenderEx replays; comments said the block copies the Annex shape (it is the governor-button shape); a co-op relevance entry left the switch out of the settings fingerprint; gate-order text that did not match the code; doc gaps (seeding step, pins, Known Limits, checklist lines); unused usings and a raw no-break space in a test. | Hygiene | Local slips. | Fixed in the fix pass. |

## Root-cause pattern

Findings 3, 4 and 5 each repeat a lesson that is already in the record, and finding 1 is the second insert-position
defect in UIExtenderEx in two days. The brief for this feature followed the batch rule from the Reeve's Ledger RCA
(name the engine fact each copied pattern depends on), but it did not consult the lessons for the events it chose.
The lessons exist; the step that reads them before the design does not. The repeat-offender clause applies:

- The briefs for the two remaining B10 features (#859, #860) list, for each engine event and each prefab insert they
  use, the lesson entry that covers it, and the architect reads `campaign-mechanics.md`, `state-lifecycle-save.md`
  and `localization-ui.md` before the brief.
- A mechanical gate replaces the reading for the insert position: a test over every `PrefabExtensionInsertPatch` in
  `TAOM.dll` that fails on an `Append` whose comment or name says "child", and on a `Child` without an `Index`
  override. It is a follow-up on Mike's word, because it touches every existing patch.

## Why each lens caught or missed these

- Lenses 2 (engine) and 7 (XML) found finding 1 by reading `PrefabComponent`.
- Lenses 1 (standards) and 5 (data flow) found finding 2; lenses 2 and 5 found finding 3; lens 2 found findings 4 and 5.
- Lens 3 (efficiency) found the per-target gate read and the duplicated refresh; lens 4 (completeness) found the co-op relevance entry, the gate tests and the doc gaps; lens 6 (design) found the dead members and proposed `DecideLore` as the one owner of lore decisions.

## Not applied

- A shared fault latch for the screen mixins of Reeve's Ledger and Namegiving, and a shared test helper that walks
  prefab bindings. Both touch committed Reeve's Ledger code; follow-ups on Mike's word.
- The deep-review standards lens should name UIExtenderEx mixins as an entry-point boundary in its check 9. A
  harness edit outside this feature; owed on Mike's word.

## Lessons appended

- `lessons/localization-ui.md`: UIExtenderEx `InsertType.Append` inserts a sibling, never a child.
- `lessons/campaign-mechanics.md`: the clan kingdom-change entry gets this second occurrence and a design-time check.
- `lessons/state-lifecycle-save.md`: the save-contract entry is widened to text the engine saves for TAOM.
