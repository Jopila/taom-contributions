# RCA: Enlistment wars follow the commander during service (#832, deep-review, 2026-10-20)

## Top-line

`/deep-review` (seven lenses in two waves, then one convergence pass) on #832 found **no API
incompatibility** but **four MEDIUM defects** in the first build, all fixed before the first commit. The
largest was a design defect: the first build followed the commander's wars from inside the engine's war,
peace and kingdom-change events, and those events fire in the middle of `ChangeKingdomAction`. The fix
replaced the event behavior with a sync from the hourly tick, which also removed the change to the shared
`SubModule.cs`.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Data flow, Engine compat) | A sync run from `WarDeclared` saw a half-finished kingdom change. When a realm is destroyed, `ChangeKingdomAction` clears the clan's kingdom first and then re-declares the old wars one event at a time (`ChangeKingdomAction.cs:76`, `:129-135`), so each event ran the full diff against a part-built enemy list: the soldier made peace with every mirrored faction (freeing prisoners, ending sieges) and declared war again. Variants: a rebellion declared the soldier on his commander's old kingdom, and a declaration against the kingdom whose live war list the engine was iterating could throw `InvalidOperationException`. | Code run inside an engine action | The plan verified when each event fires relative to its own state change, not what else the enclosing action does before and after it. | `FollowCommanderWars` runs only from `EnlistmentReconciler.OnHourlyTick`, never from an engine dispatch or the load path; the join-time top-up covers a battle inside the hour. Lesson appended to `lessons/campaign-mechanics.md`. |
| F2 | MEDIUM (Data flow, Standards) | Between a crown war and the hourly release, the commander's next battle could be against the soldier's own kingdom, and the join path had no check, so a vassal fought his own crown: the outcome #832 exists to stop. | Rule scope | "Never refuse a join on a war check" was written for the soldier's missing wars and was applied to every war. | `TryJoin` skips the battle while the soldier serves against his own crown; the release follows. |
| F3 | MEDIUM (Standards) | The crown-war release waited on any open encounter, but a soldier held in the commander's town always has a settlement encounter, so the release could wait for days. | Proxy read | `HasCurrent` stood in for "a battle is live". | The release reads the encounter ownership and waits only on a battle encounter, a conversation or a map event. |
| F4 | MEDIUM (convergence) | The enlist gate checks only the player's kingdom, while the release also checks an independent clan, so an independent player already at war with the lord's realm could swear and be released at the next hour for a war older than his oath. | Two readers of one fact | The release was widened to the player's faction without re-reading the gate. | The release ignores a war that predates the oath (`EnemiesAtOath`). |
| F5 | LOW (several) | A process-lifetime "log once" set with no session reset; `MirrorWarAtJoin` returned false for a war that already existed; `MakePeaceWith` reported success for a vetoed peace; followed wars used the player-hostility reason (quests failed instead of cancelled); a failed enemy read would have wiped the mirror; `EnlistmentBehavior` grew past the 150-line entry-point limit; the release was a toast; doc claims about the vassal risk were unsupported. | Various | Various. | All fixed: every refusal logged, already-at-war first, peace reports whether it landed, the default reason for followed wars, an empty read skips the pass, the order lives in the reconciler, a modal, the doc states the vassal's real remaining risk. |

## Convergence pass

One reviewer checked the hourly design against the v1.5.5 sources. Vanilla makes kingdom war and peace
from the same campaign ticks, the hourly tick never fires inside a battle mission, and only the hourly
path reaches the release and the sync. It found F4, two LOW items and documentation nits, all fixed.

## Root-cause pattern

F1 and F2 share a cause: a rule was placed where an event arrived, not where the world was settled. An
engine event tells you a fact changed; it does not tell you the enclosing action has finished. Acting on
world state from a tick that runs between actions is slower by up to an hour and correct by
construction.

## Why each lens caught or missed these

- **Standards:** F2 (as a plan drift), F3, the log-once set, and the vassal contract.
- **Engine compatibility:** F1 and its rebellion and crash variants, the quest-reason difference.
- **Efficiency:** no hot cost; the hero walk ordering.
- **Completeness:** the tests and docs each fix needed, the unsupported vassal claim, the modal.
- **Data flow:** F1, F2, and the dead-entry and independent-clan cases.
- **Design:** the hourly sync, the modal and the engine's own opposite-side lookup.
- **XML:** no defect; the spelling note.

## Feedback memories to codify

None; the lesson covers the pattern. The 150-line entry-point limit was caught by hand, not by a gate;
that gap is recorded in the lesson.
