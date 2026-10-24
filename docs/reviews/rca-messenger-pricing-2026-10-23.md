# RCA: Messenger distance pricing (#898 part 2, deep-review, 2026-10-23)

## Top line

`/deep-review` ran seven lenses in two waves on the change that prices a messenger by distance, with
surcharges for war, a stranger and a siege, and two new refusals. It confirmed one HIGH, two MEDIUM
and about twenty LOW findings, all fixed before the commit, plus eight behaviour-preserving
simplifications. The HIGH was a hint that no player could ever see: the code and the tests were
right, and the widget tree was not.

The pattern behind the main findings: the rules were checked as values, not as what the player sees
or does. A hint bound to the right text in a widget that never gets hovered, a "same army" fact true
for a lord who is still on the march, and refusals that also blocked the face-to-face line all pass a
value test.

## Findings

| # | Sev | Bug | Why missed | Fix |
|---|---|---|---|---|
| 1 | HIGH | The "about N days" hint never showed: the only `HintWidget` sat beside the `ButtonWidget`, and an enabled button takes the hover, so the hint fired only for the greyed button. | The prefab test pinned the binding, not which widget the cursor reaches. #944 removed the inner hint to fix the disabled case, and nobody re-asked the question for the enabled case. | A second `HintWidget` inside the button (vanilla `BarterScreen.xml:276`), the outer one kept for the greyed state; the prefab test pins both. |
| 2 | MED | "Same army" meant membership: a called lord still marching to the army, or a player in an army but not with it, was refused with "marches in your army". | The fact was read from `Army` membership, the simplest property, without asking what "with you" means on the map. | Both parties must be in the leader's group (`Army.DoesLeaderPartyAndAttachedPartiesContain`). |
| 3 | MED | The new same-settlement and same-army refusals also hid the in-person dispatch line in a lord's hall, where they are always true. | The refusals were added to the one shared check without listing that check's callers. | The refusals apply only to the remote send; the in-person path passes `inPerson: true`, and the service skips both checks then. |
| L | LOW | The gold rule computed in two places; the offer computed twice per refresh; "1 days"; config validated twice; a cache in the mixin that saved one string per page; a dead `CanSendMessenger` overload; gold tests whose quote equalled the base price; restated defaults in 20 test rows; a dialog condition named for gold that checks every refusal; untested branches (zero speed, zero travel days); unpinned surcharge reads; a dead doc link and stale doc lines; no backlog entry for five new keys. | Hygiene, and wave 1 fixes that left their old paths behind. | Fixed; translation owed in #763. |

## Why each lens caught what it did

- **Data flow (5)** found the invisible hint: it followed the hint from the view model to the widget the cursor reaches.
- **Engine (2)** found the membership meaning of "same army" and the hidden in-person line, by reading `Army` and the callers of the check.
- **Standards (1)** also found the hidden in-person line, plus the duplicated gold rule, the double offer, the double validation and the plural text.
- **Efficiency (3)** found the cache that cannot pay for itself: every page open builds a new mixin.
- **Completeness (4)** found the backlog gap, the dead link and the untested branches.
- **Design (6)** moved the in-person rule and the offer into the service, where unit tests reach them, and tied the trip days to the speed the messenger travels at.
- **XML (7)** found the plural form.

## Lesson

Finding 1 repeats an existing lesson: `lessons/localization-ui.md`, "A hint on a disabled button needs an enabled
parent", which already says to keep the inner hint for the enabled state. #944 fixed the disabled state and removed
the inner hint, and its test asserted that no inner hint exists. The lesson now has a repeat note: a fix for one
button state needs a structural test for both. Findings 2 and 3 are local to this feature and need no new rule.

## Follow-ups (older code, not in this change)

- `MessengerCampaignBehavior.ResolveHero` scans every character with LINQ; `Hero.Find` walks only the hero lists. Unverified for a removed hero.
- `MessengerConfigProvider`'s two older range checks can use the new `Checked` helper.
- `HeroSnapshot.IsInMapEvent` has no reader.
