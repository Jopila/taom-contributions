# Shadowfax Screens

## Overview

The clan, kingdom, character, career, inventory, party and quest screens open at once. Their panels
no longer slide in, and the rows in the inventory and party lists no longer animate when they expand
or change state. Nothing else about the screens changes. The feature is always on.

## Why This Exists

- **Vanilla behavior:** in Bannerlord v1.5.5 most screen panels slide in over 0.45 s, and the
  character screen's side panels over 0.4 s. Inventory and party rows animate for 0.075 to 0.15 s
  each, and the equipped-item controls slide for 0.2 s. TAOM's own career screen slid its header and
  footer in over 0.35 s.
- **TAOM requirement:** players open these screens many times in a session, and the slide carries
  no information.
- **Without this feature:** each screen open spends up to 0.45 s moving its panels into place.

## Architecture

### Design Challenge

The animations live in the `VisualDefinitions` block of each screen's prefab XML. TAOM ships its own
copies of the party screen, its two troop row prefabs and the career screen, but uses vanilla's
prefabs for the other nine movies. A UIExtenderEx `PrefabExtensionSetAttributePatch` resolves one
node per `[PrefabExtension]` attribute, so it would need an XPath for each of the 27
VisualDefinitions, and each XPath misses when an engine update renames its node.

### Solution Approach

- **Vanilla movies:** `ShadowfaxScreensPrefab` is a UIExtenderEx `CustomPatch<XmlDocument>`. It
  receives the whole prefab document, and its nine `[PrefabExtension]` attributes register it for
  ClanScreen, ClanMembers, KingdomManagement, FiefsPanel, CharacterDeveloper, Inventory,
  InventoryEquippedItemControls, InventoryItemTuple and QuestsScreen. UIExtenderEx applies it from
  `WidgetPrefab.LoadFrom` each time the prefab loads from disk, which also covers the four that load
  as child prefabs (ClanMembers, FiefsPanel, InventoryEquippedItemControls, InventoryItemTuple).
- **The rewrite:** `InstantTransitionRewriter.MakeInstant` sets `TransitionDuration="0"` on every
  definition in `Prefab/VisualDefinitions`, the only block the engine reads. A definition without
  the attribute gets it too, because Gauntlet's default is 0.2 s. The ease settings and
  `VisualState` targets stay, so each panel ends where vanilla puts it. With a zero duration
  `Widget.UpdateVisualDefinitions` snaps to the target state on its first tick; it never divides by
  the duration, and `DelayOnBegin` has no effect. Vanilla uses the same zero duration on the
  tournament screen.
- **Gamepad navigation:** two vanilla widgets wait for the animation before they hand gamepad focus
  on: an expanded inventory or party row's controls (`InventoryTupleExtensionControlsWidget`, 0.15 s)
  and the equipped-item controls (`InventoryEquippedItemControlsBrushWidget`, 0.2 s). Both compare
  against the duration, so with 0 they hand focus on after one frame.
- **TAOM's own prefabs:** every VisualDefinition in `Party/PartyScreen.xml`,
  `Party/PartyTroopTuple.xml`, `Party/PartyTroopTupleLeft.xml` and `CareerSystem/CareerScreen.xml`
  has `TransitionDuration="0"` in the XML. That includes the party section headers' 0.045 s press
  squish. FormationListPanel and FormationSeperator belong to the formation dropdown, which is
  commented out in PartyScreen.xml; their zeros take effect only if it returns.
- **Registration:** `UIExtender.Register` in `SubModule.RegisterUiExtensions` finds the class, in
  solo and, through `CoopUiRegistrationPolicy`, under co-op (the class has no `[CoopSuppressedUi]`;
  the change is visual only). No IoC entry and no Harmony patch.
- **Generated prefabs:** TAOM.Dependencies sets `UIConfig.DoNotUseGeneratedPrefabs = true`, so the
  engine always loads these movies from XML and the patch always applies.

### Component Diagram

```
SandBox prefab XML (9 movies)           TAOM prefabs (party x3, career)
        |                                        |
  WidgetPrefab.LoadFrom (UIExtenderEx hook)   TransitionDuration="0"
        |                                     written in the XML
  ShadowfaxScreensPrefab (CustomPatch<XmlDocument>)
        |
  InstantTransitionRewriter.MakeInstant
```

## Clan fast open

**What it does:** the Clan screen builds its party lists once per open instead of three times. The
screen shows the same rows and headers; only the repeated work goes. The switch is "Clan Fast Open"
(default on). It is off under co-op, whatever the setting says.

**Why three builds:** `ClanPartiesVM.RefreshPartiesList` clears and refills the Parties, Garrisons
and Caravans lists and constructs a row view model for every party, caravan and garrison of the
clan. Vanilla runs it three times in one open, all for the same instance:

1. The `ClanPartiesVM` constructor calls `RefreshPartiesList` directly.
2. The same constructor then calls `RefreshValues`, which calls `RefreshPartiesList` again.
3. The `ClanManagementVM` constructor calls `UpdateKingdomRelatedProperties`, then
   `UpdateBannerVisuals`, then its own `RefreshValues`, which calls `_clanParties.RefreshValues()`,
   and that calls `RefreshPartiesList` a third time.

Builds 2 and 3 rebuild lists that build 1 just filled with nothing in between to change them.

**What is skipped:** only a `RefreshPartiesList` call made inside the same instance's own
`RefreshValues`. A prefix on `RefreshValues` opens a refresh scope (a token held in a thread-static
and handed to the finalizer through Harmony's `__state`); the bool prefix on `RefreshPartiesList`
returns false only when the innermost scope belongs to that exact instance. Direct callers always
build: `ClanManagementVM.RefreshCategoryValues` (after a fief, member or income change) and any
co-op handler are never inside a scope. A thrown exception, a later finalizer that throws, or a
stripped prefix cannot leave a scope open, because the finalizer closes it from the token and a null
token does nothing.

**Headers:** vanilla sets the "Caravans (N)" and "Garrisons (N)" header texts only at the end of
`RefreshPartiesList` (`ClanPartiesVM.cs:500-503`), while `RefreshValues` first resets them to the
bare text. After a skip the `RefreshValues` finalizer re-renders both headers from the current
lists, with the same `GameTexts` calls vanilla makes. The Parties header is not touched by
`RefreshValues`, so it keeps the text build 1 gave it.

**Interplay:**
- **Refuge** (`RefugeClanScreenPatch`, a postfix on `RefreshPartiesList`) still runs after every
  build that runs, so its refuge rows are added once, in the first build, and stay. The header
  re-render counts them, as vanilla's did after Refuge's own re-render.
- **TroopWeight** patches the row's `UpdateProperties`, not the list build. `RefreshValues` still
  refreshes every party row after the skipped build, so it runs as before.
- **Timer:** `GauntletClanScreen`'s explicit `IGameStateListener.OnActivate` (found through the
  interface map, never a name string) is wrapped by the shared timer. With "Log Screen Open Times"
  on, one line covers the VM build and the movie load, with the list-build counts as its detail:
  `[ScreenOpen] Clan open: 212.4 ms (fast open on; list builds: 1 run, 2 skipped, 38.1 ms)`.
  The build milliseconds are the real build only; with the switch off the detail reads
  `fast open off; list builds: 3 run, 0 skipped, ...`.

**Known limit:** with the switch on, a `RefreshValues` call after the screen is open refreshes the
existing rows without rebuilding the lists. The one place vanilla pairs a list change with it
(`ClanManagementVM.cs:1389-1390`) calls `RefreshCategoryValues` first, which still rebuilds.

**How to measure:** turn "Log Screen Open Times" on in the Battle Load Diagnostics page, open the
Clan screen six times with "Clan Fast Open" off, then six times with it on, on a large save. Drop
the first open of each arm and compare the medians of the `[ScreenOpen] Clan open` lines in
`taom_debug.log`. Keep the change only if the median drops by at least 16 ms (one 60 fps frame) and
by more than the off arm's spread; otherwise delete it and keep the timer.

**Files:**

| File | Purpose |
|------|---------|
| `Main/Features/ShadowfaxScreens/Clan/ClanScreenFastPathService.cs` | Refresh scopes, the skip decision, the open window's counts |
| `Main/Features/ShadowfaxScreens/Clan/ClanFastOpenCalls.cs` | Static facade the hooks reach, and the patch category name |
| `Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshValues_ClanFastOpen_Patch.cs` | Opens and closes the scope; re-renders the headers after a skip |
| `Main/Features/ShadowfaxScreens/Clan/Hooks/ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch.cs` | The skip, and the build count and timing |
| `Main/Features/ShadowfaxScreens/Clan/Hooks/GauntletClanScreen_OnActivate_ClanFastOpen_Patch.cs` | The `[ScreenOpen] Clan open` window |

**Tests** (`TAOM.Tests/Features/ShadowfaxScreens/Clan/`): `ClanScreenFastPathServiceTests` (off,
co-op, provider throw, owner-only skip, nesting, null token, counting window, detail format);
`ClanFastOpenHookTests` (real Harmony on dummy classes: inner call skipped and direct call not, a
throwing later finalizer, a stripped prefix, the open line); `ClanFastOpenBindingTests` (both
targets, the three-build chain in the installed IL, the header tail, the interface-map target);
`ClanFastOpenWiringTests` (registration, the facade, the category).

## Configuration

The instant screen transitions are always on and have no setting. TAOM's four prefabs carry their
zeros in the XML, so a setting would first need them moved onto the patch (UIExtenderEx patches
TAOM's own prefabs by file name too).

The screen open speed work adds a "Screen Performance" group to the Battle Load Diagnostics MCM
page. None of its rows needs a restart; each is read on every screen open.

| Row | Default | What it does |
|---|---|---|
| Log Screen Open Times | Off | Writes one `[ScreenOpen]` line per screen open to `taom_debug.log`: the screen, the phase and the milliseconds. Use it to compare a fast-open switch off against on. |
| Clan Fast Open | On | Builds the Clan screen's parties list once instead of three times. Off restores vanilla's build. |
| Inventory Fast Open | On | Skips work the Inventory and trade screen repeats. Off restores vanilla's build. |
| Party Fast Open | On | Caches lookups the Party screen repeats for each troop. Off restores vanilla's build. |
| Quest Log Fast Open | On | Builds finished quests only when they are selected. Off restores vanilla's build. |

## Inventory and trade fast open

One screen covers both inventory and trade: `GauntletInventoryScreen.OnReady` builds `SPInventoryVM` and
loads the `Inventory` movie, and a merchant's stock is the same screen with a larger left list. Two
changes sit behind "Inventory Fast Open" (default on, read per open, no restart), both IL call-site swaps
under the category `PatchXXX_ShadowfaxInventory` (the patch number is assigned at rebase).

**1. Name-sort skip.** `SPInventoryVM.InitializeInventory` sorts each side by item name with
`OrderBy<ItemRosterElement,string>` straight into `ToArray<ItemRosterElement>` before building one
`SPItemVM` per row. With the toggle on, `NameSortRewrite` swaps both `OrderBy` calls for
`ShadowfaxInventoryCalls.SortByNameOrPassThrough`, which hands the source on unsorted; off, or with no
service, it calls `OrderBy` as vanilla does. The rewrite applies only when the method holds exactly two
such sites, each immediately followed by `ToArray<ItemRosterElement>`; any other shape returns the IL
unmodified with one warning (so a re-applied category, whose second pass finds no `OrderBy`, is a no-op).

**Why the order the player sees is unchanged.** `InitializeInventory` has two callers, the 3-argument
`SPInventoryVM` constructor and `AfterReset`, and both build the two sort controllers and call
`SortByDefaultState` (then the saved sort preference) right after it. Every comparer falls back to
`ItemDescription` on a tie (`ItemComparer.ResolveEquality`), so the final order depends on the rows, not on
the order they were inserted in. `InventoryBindingTests` pins all three facts against the installed DLLs:
the caller list, the sort after the call, and the tie-break.

**2. Concept lookup memo.** The 8-argument `SPItemVM` constructor reads `Concept.All` (the cached
`Campaign.Current.Concepts` list) and calls `SingleOrDefault<Concept>` twice per row, into two fields that only
`ExecuteConcept` reads. `ConceptLookupRewrite` swaps those two calls for `CachedConcept0` and `CachedConcept1`,
which keep one result per call site and per source list (the list is held weakly; a new campaign has a new
list and recomputes). A null result is cached; an exception (a duplicate concept id) is thrown exactly as
vanilla throws it and is never cached. The rewrite needs exactly two sites, each preceded by
`Concept::get_All` within 24 instructions and after the previous site. There is no patch on `Concept.All`
itself. The expected gain is small, because `Concept.All` is already a cached list and the cost is the
linear scan with its delegate.

**Timing.** With "Log Screen Open Times" on, the `OnReady` prefix starts the `Inventory open` window only when
`_gauntletLayer` is null (a repeat `OnReady` returns early in vanilla and is not an open), and a void finalizer
ends it, also when `OnReady` throws. `InitializeInventory` is timed by the service as a phase inside that
window (a Reset re-runs it outside an open and logs nothing). The line looks like:

`[ScreenOpen] Inventory open: 212.4 ms (mode=trade, left=312, right=88, fast=on, rows=118.0 ms, sorts skipped=2, concepts cached=598/600)`

`mode` is `trade` or `inventory` (from `InventoryLogic.IsTrading`; the private inventory mode is not read, to
avoid reflection), `left` and `right` are the other party's and the player's element counts, `rows` is the
`InitializeInventory` time, `sorts skipped` counts the pass-throughs and `concepts cached` is hits over lookups.
The install line is one per site in `taom_debug.log`: `[ShadowfaxInventory] name sort: swapped 2 of 2 call sites`.

**Left out, and why.** Trade-data and button-hint deferral (the ensure paths are unproven, and the
`ThisStock` setter can apply a transaction), a banner cache, and widget cost are not built. If the rows stay
slow after this, the next target is `FindItemFromSide`'s linear lookup.

**How to measure.** Turn "Log Screen Open Times" on and load a save. Go to the largest merchant. For
"Inventory Fast Open" off and then on, open the trade screen 6 times each, drop the first open of each arm,
and compare the medians of the `[ScreenOpen] Inventory open` lines. Keep the change only if the median drops by
at least 16 ms and by more than the off arm's spread; otherwise delete it and keep the timer points.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ShadowfaxScreens/Inventory/InventoryOpenFastPathService.cs` | Toggle gate, install lines, concept memo, per-open counters and detail (no TaleWorlds types) |
| `Main/Features/ShadowfaxScreens/Inventory/NameSortRewrite.cs`, `ConceptLookupRewrite.cs` | Pure IL rewrites for the two call-site swaps |
| `Main/Features/ShadowfaxScreens/Inventory/ShadowfaxInventoryCalls.cs`, `SourceScopedMemo.cs` | The static helpers the rewrites call; the weak one-slot memo |
| `Main/Features/ShadowfaxScreens/Inventory/Hooks/` | `OnReady` timing bracket, `InitializeInventory` transpiler and phase, `SPItemVM` constructor transpiler |
| `Main/Adapters/IInventoryOpenAdapter.cs`, `InventoryOpenAdapter.cs` | Mode and side counts of the active inventory state, for the timer detail |
| `Main/Features/ShadowfaxScreens/InstantTransitionRewriter.cs` | Pure XML transform: zero every VisualDefinition's duration |
| `Main/Features/ShadowfaxScreens/UI/ShadowfaxScreensPrefab.cs` | UIExtenderEx patch on the nine vanilla movies |
| `Main/_Module/GUI/PreFabs/Party/PartyScreen.xml`, `PartyTroopTuple.xml`, `PartyTroopTupleLeft.xml` | Party screen and troop rows, zero durations |
| `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml` | Career screen header and footer, zero durations |

## Dependencies

- Bannerlord.UIExtenderEx 2.13.3 (through TAOM.Dependencies): `Prefabs.CustomPatch<XmlDocument>`.

## Tests

- `TAOM.Tests/Features/ShadowfaxScreens/InstantTransitionRewriterTests.cs`: durations zeroed, a
  missing duration added, ease and states kept, idempotent, definitions outside the block left
  alone, no-op without definitions, null document.
- `TAOM.Tests/Features/ShadowfaxScreens/ShadowfaxScreensPrefabTests.cs`: pins the nine movie names;
  checks each installed vanilla movie still has a definition to zero and no VisualState with its own
  duration (needs the game install); and checks TAOM's four prefabs are already instant.
- `TAOM.Tests/Migration/PrefabExtensionBindingTests.cs`: every `[PrefabExtension]` movie name,
  including these nine, must exist in the installed game with the exact case of its file name.

## How to make another screen instant

1. Find the movie name: the prefab file name under `Modules/<Module>/GUI/Prefabs`.
2. Confirm that every `VisualDefinition` in it is an animation you want to remove.
3. If TAOM ships its own copy of the prefab, set `TransitionDuration="0"` in that XML and add the
   file to `TaomPrefab_AlreadyInstant_RewriterChangesNothing`.
4. Otherwise add `[PrefabExtension("<Movie>", null)]` to `ShadowfaxScreensPrefab` and the name to
   `PatchedMovies` in the tests.

## Changelog

- 2026-10-18: first version.

## GitHub Issue

- **Issue:** #850: [Instant UI: management screens open without slide animations](https://github.com/haterade22/TAOM/issues/850)
- **Status:** Open
