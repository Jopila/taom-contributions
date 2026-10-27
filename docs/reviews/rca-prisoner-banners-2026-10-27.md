# RCA: Prisoner Banners (#902, deep-review, 2026-10-27)

## Top line

`/deep-review` ran seven lenses in two waves on the party screen banners: the defect lenses (1, 2, 5, 7), then efficiency,
completeness and design (3, 4, 6) on the code after the wave 1 fixes. The review found one HIGH finding, three MEDIUM
findings and a set of LOW findings. All of them were fixed before the commit. Three design proposals were applied: the
values are read once in the mixin's constructor, the rules lost an input the engine makes redundant, and the two identical
prefab blocks became one sub-prefab.

The pattern behind the HIGH and the first MEDIUM finding: the build copied the widget and brush names from vanilla, but not
the arguments vanilla passes to the view model and the size it gives the sprite. Both only show in a running game.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | HIGH | Both banners were built as `new BannerImageIdentifierVM(banner)`, which leaves `nineGrid` false. The brush `Flat.Tuple.Banner.Small.Hero` sizes its texture at 2.5 times the widget's short side, and vanilla pairs it only with nine-grid banners (29 of 29 bound sites). A plain banner texture is the centre third of the banner, so each corner would show a slice of the emblem at three times vanilla's zoom. | The plan checked that the brush and the constructor exist, not what vanilla passes to the constructor next to that brush. The constructor's default argument looked harmless. | `nineGrid: true` on both; `BothBannerViewModels_AreNineGrid` fails on any constructor call without it. |
| 2 | MED | The noble icon was drawn at 20x20. The sprite `General\Mission\noble` is 58x40, and a plain `Widget` stretches its sprite to the widget, so the icon showed at 69% of its real proportions. | The size was a guess; nobody read the sprite's size in `NativeSpriteData.xml`. | 29x20, pinned by the inert-block test. |
| 3 | MED | The mixin looked up its settings provider and logger from the container in `OnRefresh`. 16 of the 20 Client mixins look up in the constructor. | Copied from Hearth Roll, the one sibling that does the lookup late. | Superseded by finding D1: the mixin now does all its work once, in a guarded constructor. |
| 4 | MED | The binding test checked the five property names as substrings. `@HasPrisonerClanBanner` contains `PrisonerClanBanner`, so a slip that bound the clan banner in both corners passed every test (shown by a mutation replay). | The test proved that each name appears, not that each widget binds its own pair. | Exact tokens, and each banner's wrapper `IsVisible` must be `@Has` plus that banner's data source. |
| L | LOW | Other LOW findings, all fixed: <br>- a banner-code cache that saved nothing, because no caller refreshes a bound row; <br>- a CRLF test that only checks the local checkout (git stores these files with LF); <br>- a hand-written binding walk instead of `PrefabBindingWalker`; <br>- a brush grep that `EveryGuiPrefabBrush_IsDeclaredByAModule` already covers; <br>- a placement test that passed with the block outside the portrait; <br>- wrappers not pinned as inert; <br>- a nine-grid pin tied to an exact call count; <br>- no read-through test for the switch; <br>- comments over the ADR-009 limit; <br>- a wrong doc sentence about the tier icon; <br>- stale setting counts in `server-settings.md`; <br>- missing Dependencies and Co-op sections, the `coop-interop.md` paragraph and the INDEX row. | Hygiene, and a doc checklist that was not walked before review. | Fixed. |
| D | KEEP | Design proposals applied: (D1) a bare `[ViewModelMixin]` that reads once in its constructor, since everything it reads is fixed for the row's life, so `PartyCharacterVM.RefreshValues` is no longer transpiled; (D2) `Decide` lost `isHero`, which a clan implies, and takes named arguments with `Hero.IsClanLeader`; (D3) the 13-line block, identical in both tuples, is the sub-prefab `TaomPrisonerBanners.xml`, so a size change or a re-sync touches one file. | Not defects. | Applied. |

## Accepted limits

- After an execution on the screen, the other rows do not refresh until the screen reopens: a new clan leader's icon and a
  destroyed kingdom's banner are stale until then. A fix needs a hook on `PartyVM.ExecuteTroop` for a cosmetic gap that
  clears itself, so the simplicity criterion rejects it.
- A ruling-clan prisoner shows the same banner in both corners, because a ruling clan's own banner takes the kingdom's
  colours. The right corner always means the realm.
- A mixin on a view model type that declares no `OnFinalize` makes UIExtenderEx add one more transpiler to the base
  `ViewModel.OnFinalize`. This feature adds one; six TAOM targets already do.

## Pre-existing, outside this change

- TAOM's right tuple lacks vanilla v1.5.5's `SPGeneral\blood_feud_icon` on the Execute button. It belongs to the next re-sync.
- Hearth Roll's mixin does the same late container lookup as finding 3.

## Lesson

`lessons/localization-ui.md`, "Copy vanilla's view model arguments and sprite size with its widget, not only the names".
