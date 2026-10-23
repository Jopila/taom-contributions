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
- **Registration:** `UIExtender.Register` in `ClientUiExtensions.Register` (called from `ClientSubModule.OnSubModuleLoad`) finds the class, in
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

## Configuration

None. The feature is always on and has no MCM setting. TAOM's four prefabs carry their zeros in the
XML, so a setting would first need them moved onto the patch (UIExtenderEx patches TAOM's own
prefabs by file name too).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ShadowfaxScreens/InstantTransitionRewriter.cs` | Pure XML transform: zero every VisualDefinition's duration |
| `Client/Features/ShadowfaxScreens/UI/ShadowfaxScreensPrefab.cs` | UIExtenderEx patch on the nine vanilla movies |
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

---

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
