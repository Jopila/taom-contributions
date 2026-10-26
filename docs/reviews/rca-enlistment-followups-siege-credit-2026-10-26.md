# RCA: Siege and capture credit follows the player, not the side's leader (#1015, deep-review, 2026-10-26)

## Top-line

#1015 made siege credit follow the enlisted soldier instead of his commander. `/deep-review` (data-flow,
engine and standards lenses, then a convergence pass) found **no runtime defect**: at the moment the siege
completes the map event's sides are still populated, every capture path (assault, auto-resolve, a failed
sally-out) credits the right side, and no capture outside a battle credits wrongly. It found **two design
inconsistencies**, both applied: the side rule now covers every player, not only an enlisted one, which is
what the momentum and co-op docs already promised; and the lord-capture objective had the same flaw (credit
followed whichever winner the engine drew to hold the prisoner), so it now uses the same side rule.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | LOW (Data flow, design) | The fix credited the side only for an enlisted soldier; a player fighting inside another lord's army on the capturing side still got nothing, unlike the battle path and the documented rule. | A fix scoped to the reported case | The issue named the enlisted soldier. | One rule for every player: the player's party fought on the capturing side. |
| F2 | LOW (Data flow) | The lord-capture objective keyed on the prisoner's captor, which the engine draws by weighted chance among the winners (v1.5.5 `MapEvent.cs` ~1827-1835): it missed captures the player's side made and could credit a battle he sat out. | The same flaw in a sibling path | Out of the issue's scope. | Credit when the player's party is on the captor's side of that map event; outside a battle, the old rule. |
| F3 | LOW (Engine compat, Standards) | The capturer was described as the attacking side's leader; a failed sally-out passes the defending side's leader. Stale comments, moved doc line anchors, and a comment that said the battle end still leaves the army. | Accuracy | Written for the common case. | Wording fixed; anchors replaced by method names; one comment cites the engine lines that keep the sides populated when the event fires. |

## Convergence pass

One reviewer checked the second round against the installed v1.5.5 sources: the map event sides are still
populated wherever the capture events fire, no capture outside a battle is credited wrongly, nothing is counted
twice, and dependency injection still resolves. It found four wrong statements (momentum still credits a vassal
through his realm's capture, so the docs and the release note now say so; a dead fallback in the war check; two
comments) and four gaps, all fixed: the issue quest for capturing lords had the same random-draw flaw and now
uses the same rule; the settlement handler now credits sieges only, as its sibling handlers do; both handlers
read the main party null-safely; and war momentum no longer counts a win by defenders who joined from outside
the walls as a captured town (the game takes no town there).

## Feedback memories to codify

None.
