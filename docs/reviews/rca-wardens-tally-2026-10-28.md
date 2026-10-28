# RCA: Warden's Tally (#903, deep-review, 2026-10-27 to 2026-10-28)

## Top line

`/deep-review` ran seven lenses in two waves on the dungeon menu's escape odds. The game updated from v1.5.5 to v1.5.6
between the waves, so the work was paused, rebased onto the engine bump and checked again: vanilla's escape tick and the
dungeon menu init are unchanged in v1.5.6, apart from one perk helper signature the bump had already moved. The review
found no HIGH finding, two MEDIUM code findings, two MEDIUM record findings and a set of LOW findings; all were fixed before
the commit. Three design proposals were applied: the tally hangs on the public `GameMenuOpened` event instead of a Harmony
patch, the text is rendered once at open, and code no input can reach was deleted.

The pattern behind the MEDIUM code findings: the build checked its numbers against the solo game only. A co-op client
reads the player's clan as its own, and the dungeon's captor is always the settlement, so two paths written for cases the
engine never produces in that seat shipped untested.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | MED | On a co-op client, the menu halved the chance for a lord in the player's own fief. The client's `Clan.PlayerClan` is its own clan, but the escape roll runs only on the server, where no human clan holds the fief. | The co-op check covered the boost line (which the client's stale effect registry cannot show) and not the halving, which reads the same local state. | A peer that does not roll clears `PlayerHeld`, so the halving and its line drop together with the boost. A test pins it. |
| 2 | MED | A "by lord" branch listed each lord's chance when lords differed. In v1.5.6 every lord in a dungeon shares one chance: a garrison's captures land in the home settlement's prison (`MapEventParty.RosterToReceiveLootPrisoners`). | Written for a case nobody checked the engine could produce. | Deleted with its two text rows; a binding test pins the engine rule it depended on. |
| 3 | MED (records) | Issue #903 still described the plan (another folder, an interface, a different doc name, v1.5.5), and the feature doc had no Translation section, so the new keys owed to #763 were recorded nowhere. | The completion workflow's issue rewrite and the sibling docs' Translation section were not walked before review. | The issue body describes what ships; the doc has the section; #763 lists the keys. |
| 4 | LOW | The engine re-renders a menu text with no attributes every frame (`GameMenuVM.IsMenuTextChanged`), so the nested `TextObject` cost about 10 µs and 26 KB of garbage per frame against vanilla's 6 µs. | Nobody asked how often the engine renders the variable after the init wrote it. | The intro is rendered once to a string at open. |
| L | LOW | Other LOW findings, all fixed: <br>- vanilla's "no prisoners" sentence counts heroes only, so a regulars-only dungeon read "no prisoners" above the tally (now: no tally without lords); <br>- perk terms shown as plain percents read as percentage points (now "-25% of the base chance", which adds up as the engine adds them); <br>- the `%` sign inside the value, so a translator could not place it; <br>- a redundant eligibility check, a dead parameter, a duplicate DTO and two unreachable zero-lord branches; <br>- a wrapper row with no words to translate; <br>- tests that could not fail (a guard never reached, a key never compared at the call site, a wiring test that returned early); <br>- an unreachable doc example and a wrong capacity in another; <br>- comments over the ADR-009 limit, one false claim about .NET Framework number formatting, a stale v1.5.4 claim. | Hygiene. | Fixed. |
| R | LOW | Rebase fallout: the trunk had taken `Patch124` for Time Acceleration, and added a setting, so the counts moved. | Parallel work on the trunk. | Moot for the patch (finding D1); counts set to 499 and 469. |
| D | KEEP | Design proposals applied: (D1) `WardensTallyMenuBehavior` listens to `CampaignEvents.GameMenuOpened`, which `GameMenu.RunOnInit` raises right after the menu's init, on the same args, so no Harmony patch targets the private init; a binding test pins that order; (D2) render once (finding 4); (D3) the deletions above. | Not defects. | Applied. |

## Accepted limits

- On an effect's last day the boost line can show a boost that will not roll, because the daily pass expires effects first.
- The veto line shows only while vanilla's `ExecutionCampaignBehavior` holds a lord for a pending execution; TAOM adds no listener of its own.

## Lesson

`lessons/harmony-il.md`, "Before patching a menu's private init, use GameMenuOpened".
