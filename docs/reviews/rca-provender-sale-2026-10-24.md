# RCA: Provender-master remote selling (#909), deep review of 2026-10-24

## Top line

The tier 1 review of remote selling (Codex at ultra, then Codex at high on the fixes; Lenses 5 Data flow, 2 Engine and 7
XML, then Lens 5 again on the fixes) found no CRITICAL or HIGH. It confirmed 1 MEDIUM and 10 LOW findings, after the builder
had fixed one economic defect of its own before the review (S1). Every normal gold and item path was traced and found
sound: town, village and castle; field camp; cancel; a town short of gold; buy-back. Every engine call was verified (39 of
39). All confirmed findings are fixed or documented as known limitations (below). Full suite after the fixes: 25,731 pass.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| S1 | Self | The sale screen sold into a roster of its own, so the town's price never fell with each unit sold, as it does in a vanilla trade screen whose merchant roster is the town's: a player could dump hundreds of items at full price | Engine side effect lost by a substitute object | The plan copied the reference shape (an empty left roster) without asking what the town roster did in vanilla's screen | The fee market data adds the session's sold value to the category's store value; lesson (adapters-taleworlds-api) |
| 1 | MEDIUM | One exception while moving the sold goods into the town skipped the rest of the goods and the party-screen commit; Escape on the party screen under the talk could then bring back goods the player was paid for. The first fix handed an undelivered stack back to the player, which kept the payment too (Codex fix pass, Lens 5 re-review) | Failure path of a close step | The close step was written as one try around gold, goods and commit; the first fix asked who should keep the goods, not who had paid | Each stack moves on its own; a stack that cannot move is logged as lost, since the town paid for it; the commit runs in a finally; a source pin; lesson (state-lifecycle-save) |
| 2 | LOW | The row the player clicked and the town that opened came from two quotes: `Open(rank)` opened the rank-th town of a fresh list | Index into a recomputed list | The re-check on Open was added for safety without carrying the player's choice | `Open(townId)` re-checks that town; lesson (misc) |
| 3 | LOW | The "net buy-back" branch credited the town with any fall in the player's gold, which the screen cannot cause but other gold changes could | Unreachable branch with a wrong effect | The rule was written for symmetry, not from the screen's transaction rules | A gain of 0 or less changes nothing |
| 4 | LOW | An unreachable town (the native path search's 1e30, or the 1e8 and float.MaxValue placeholders) ranked last instead of dropping | Engine placeholder values read as distances | The plan read "NaN or negative" as the only bad values | Distances must be above 0 and below 1e7; the comment cites the placeholders and a test row holds 1e30 |
| 5 | LOW | A ready camp with a live encounter was refused with the false reason "Only in a settlement or a ready field camp" | Wrong reason text | One block value was reused | Its own block and text |
| 6 | LOW | `SaleOffer.Rank` was written and never read; a dead `IsTown` check on `Town.AllTowns` | Dead code | Ordinary slips | Deleted |
| 7 | LOW | The master-switch hint did not name selling; "sold 1 goods" | Wording | Ordinary slips | Fixed before the translation run, so no language goes stale |
| 8 | LOW | A comment said a state pushed from a dialog consequence sits under the conversation; vanilla does exactly that | Wrong engine claim in a comment | Written from intuition | Corrected, citing the vanilla one-shot precedent |
| 9 | LOW | The screen's town gold reads 0 until the first sale (a screen title skips the line that fills it), and vanilla's Transfer All caps a batch by that figure | Engine UI quirk | Vanilla never titles a trade screen | Documented as a known limitation; `FOLLOW-UP` none, no issue: the gold still balances, since Done and the town's purse cap the payment |
| 10 | LOW | In a settlement-scene conversation, gear sold from equipment slots stays on the agent visuals until the scene reloads | Missing vanilla refresh | Vanilla's in-mission trade refreshes agents itself; this screen opens after the talk | Documented as a known limitation; `FOLLOW-UP` none, no issue: visual only, the inventory is right |

## Root-cause pattern

**A substitute object drops the engine's side effects (S1, 1).** The screen replaced the town's roster with one of its own
and replaced vanilla's close with a step of its own. Both substitutes worked on the happy path and lost something the engine
object did for free: the per-sale price fall, and an all-or-nothing settlement. The question to ask of every substitute is
what the original did besides its main job: the events it raised, the state it kept consistent, and the failure it could
not leave half done.

## Why each reviewer missed what it missed

- **Codex (ultra):** found 1 by injecting a failure into the real `Settle`; it judged the normal paths and prices sound,
  which they were after S1. It did not raise 2 to 10.
- **Lens 5 (Data flow):** found 1 to 7 and 9; it traced 28 flows. It did not see 10, which is a mission visual.
- **Lens 2 (Engine):** verified 39 of 39 calls and found 4 (with the uncached 0), 8, 9 and 10.
- **Lens 7 (XML):** every gate passed; it found 7.

## Preventive actions

- Lessons appended: `lessons/adapters-taleworlds-api.md` (a substitute for an engine object keeps its side effects),
  `lessons/state-lifecycle-save.md` (a close step that moves goods after payment never drops a stack and always commits),
  `lessons/misc.md` (carry the id the player saw, not an index into a recomputed list).
- No new rule: the existing "Writers before gates" row (cite the line that builds an engine figure) already covers S1's
  question for prices; pattern A of the #908 RCA is the same family.
