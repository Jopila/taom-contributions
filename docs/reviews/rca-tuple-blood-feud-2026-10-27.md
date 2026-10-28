# RCA: party tuple blood-feud mark and Hearth Roll lookups (#1031, deep-review, 2026-10-27)

## Top line

The Prisoner Banners review (#902) found two problems in code that had already shipped. TAOM's copy of the right party tuple
lacked vanilla v1.5.5's blood-feud mark on the Execute button. The Hearth Roll mixin looked up its settings provider from the
container on every row refresh. `/deep-review` ran seven lenses in two waves on the fix. It found no HIGH or MEDIUM finding.
All the LOW findings were fixed before the commit, and one design proposal was applied: the parity test now checks both
tuple copies in full, not only the Execute button.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | LOW (shipped) | `Main/_Module/GUI/PreFabs/Party/PartyTroopTuple.xml` had no `SPGeneral\blood_feud_icon` widget beside the Execute skull. Vanilla v1.5.5 draws it on every Execute button. | The copies were last re-synced to vanilla 1.4.5 on 2026-05-31. The v1.5.2 changeset lists vanilla's `PartyTroopTuple.xml` as changed (`docs/migration/v1.5.2-changeset.txt`), but that bump re-based only `PartyScreen.xml` and the nameplates, and no step compares the changeset with TAOM's clones. `PrefabCloneWidgetReferenceTests` checks only Widget-typed reference attributes, so a missing decorative widget passes it. | The vanilla line, verbatim. |
| 2 | LOW (shipped) | The same Execute skull carried `ValueFactor="20"`, which vanilla v1.5.5 does not have. It came from the 1.3-era file and survived the 1.4.5 re-sync. | Same cause as finding 1. | Removed; the Execute button now matches vanilla exactly. |
| 3 | LOW (shipped) | `ClanLordItemBadgeMixin` resolved `HearthRollSettingsProvider` on every refresh (and the logger inside `LogOnce`). | Written before the constructor pattern was the norm; no gate checks where a mixin resolves. | Both resolved once in the constructor, as `ClanLordItemFamilyMixin` does; a pin fails on any `IoC.Resolve` outside the constructor. |
| 4 | LOW | `docs/reference/feature-map.md` said Hearth Roll reads the relation through `INamedCompanionAdapter.GetRelationWithPlayer`. The mixin reads `Hero.MainHero.GetRelation`, and a test forbids the adapter. | A doc row written from the plan, never checked against the code. | Corrected. |
| L | LOW | Review hygiene: the first parity test pinned only blood-feud widgets, then only the Execute button; the test sat in a folder with no feature; comments and a summary over the ADR-009 limit; a doc sentence in the wrong section; asserts without messages. | | Fixed. The parity test now compares both tuple copies with vanilla, element for element, apart from TAOM's two documented edits (the #902 include and the Shadowfax zero durations). |

## Proposed, not applied (Mike's decision)

- **Deliver TAOM's two tuple edits as UIExtenderEx patches and delete both copies.** Vanilla's tuples would then load as
  shipped and could not drift again. It reverses the #902 binding decision (widgets edited straight into TAOM's own
  copies), so it is Mike's call.
- **Delete the TAOM prefab copies that are identical to vanilla v1.5.5** (eight files under `Main/_Module/GUI/PreFabs/`,
  `diff -w` clean) and gate against a copy identical to the file it replaces. Each copy can hide a later vanilla change, as
  `PartyTroopTuple.xml` did.
- **A clone audit for engine bumps:** `tools/audit_gui_prefab_clones.py`, planned in `lessons/localization-ui.md`, was never
  built. A scan found 12 of 32 clone pairs with fewer elements of some type than vanilla; `PartyTroopManagerPopUp.xml` lacks
  vanilla's `UsedHorsesHint` label (intent unverified).

## Lesson

`lessons/localization-ui.md`, "Pin a GUI prefab copy to the vanilla file it replaces".
