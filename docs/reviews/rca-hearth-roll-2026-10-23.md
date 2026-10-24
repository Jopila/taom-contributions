# RCA: Hearth Roll (#906, 2026-10-23)

## Top-line

The tier 1 review of Hearth Roll (a red tint over an unhappy companion's clan row and an age after a child's
name, built from one view-model mixin and three prefab extensions, no Harmony) ran the data flow, engine and XML
lenses. Codex was not run: it is a paid dispatch and Mike was away, and his word for Codex covered the previous
feature only. No lens found a CRITICAL, HIGH or MEDIUM; 8 LOW findings were confirmed and fixed in the same session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | A player companion away on an issue's alternative solution is Disabled, so the relation read, which looked the hero up again through `Hero.AllAliveHeroes`, found nothing and returned 0: that row was never tinted (lens 2). | Lookup registry excludes a live state | The plan reused an adapter that looks heroes up by id, although the mixin already held the `Hero`. TAOM's lookup convention (`AllAliveHeroes`) is right for "is this hero alive" and wrong for "this hero I already have". | Read the relation from the `Hero` the mixin holds; source pin. Lesson in `adapters-taleworlds-api.md`. |
| 2 | LOW | The same adapter's `catch { return 0; }` hid relation faults, so the "a fault is logged" claims in the mixin comment and the doc were untrue (lens 5). | Claim not checked against the callee | The claim was written for the mixin's own try, not for the method it calls. | Fixed with 1 (the read now sits inside the mixin's own try). |
| 3 | LOW | Setting the name to `CoverChildren` without a `MaxWidth` made a long name one unbounded line that squeezes the location text to nothing, on the clan list and the Player Switcher picker (lens 2, lens 7). | Gauntlet measure semantics | `TextLayout` wraps only at a fixed width; the plan said "set the name to CoverChildren" without asking how a covering text measures. | `MaxWidth="226"` on the same patch, so long names wrap as in vanilla; the wiring test pins both attributes. |
| 4 | LOW | Comments said the setting is read on a selection change and "once per row, after the companion check"; neither is true (lens 2, lens 5). | Comment written from intent | | Reworded to the real refresh points. |
| 5 | LOW | The doc said an unhappy companion "leaves the clan"; vanilla removes the hero from the game below -10, and only on a relation loss (lens 2). | Engine claim imprecise | | Reworded from `CompanionRolesCampaignBehavior` and `KillCharacterAction`. |
| 6 | LOW | The plural comment and doc implied two keys can carry every language's plural rules; Russian and Polish need a 2 to 4 form (lens 5, lens 7). | Localization assumption | | Reworded; translator note to use a label form as vanilla does. |
| 7 | LOW | The doc pointed at a translation backlog issue that does not exist yet (lens 5). | Doc ahead of the work | | The doc says the issue is owed. |
| 8 | LOW | The named companion count in `docs/INDEX.md` and `README.md` still said 18 after the feature map was corrected to 17 (lens 5). | Stale count in sibling docs | One copy of a number was fixed and the others were not searched. | All three fixed; the rewritten feature-map line lost its old dash. |

## Root-cause pattern

Findings 1, 2 and 3 come from reusing a part without reading what it does at the edge: an adapter whose lookup
list excludes a live state and whose catch hides faults, and a size policy whose measure rule changes wrapping.
The plan named both reuses as wins (no new interface, one attribute) and nobody opened the reused code's edges
until the engine lens did.

## Why each reviewer caught or missed what it did

- **Engine compatibility (2):** found 1, 3, 4 and 5 by decompiling the hero registry, the issue system and the
  text layout.
- **Data flow (5):** found 2, 4, 6, 7 and 8, and independently proposed the same direct relation read as lens 2.
- **XML (7):** found 3 from the layout side and proved the Player Switcher rows stay clean.
- **Codex:** not run (no word for paid dispatch while Mike was away).

## Feedback memories to codify

None beyond the lesson.
