# RCA: The status board during a sit-out and the grace state (#1016, deep-review, 2026-10-26)

## Top-line

`/deep-review` on #1016 ran a data-flow and standards lens and an engine and strings lens, then a convergence
pass. It found **no correctness defect**: the battle join's behaviour and every log line are unchanged by the
extraction of the oath rule, the board feeds that rule the same inputs as the join, and the new string is
registered once and seeded byte-safe in the twelve languages. The findings were about claims and naming, all
LOW, and all fixed before the push.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | LOW (Engine compat, Data flow) | The commit and the issue presented the grace-state change (an inactive commander party counts as unavailable) as a visible fix. In v1.5.5 the engine clears a hero's party in the same call that deactivates it (`MobileParty.RemoveParty` to `Hero.PartyBelongedTo = null`), so the state has no producer and the owed check also passed before the change. | A fix described from the code, not from what the engine produces | The review note behind the issue named the state without asking what creates it. | The term stays for parity with the other fitness checks; the docs say nothing visible changes today and the check is a regression check. |
| F2 | LOW (Data flow) | Two texts said the board can never promise a place the join refuses; the board mirrors only the oath rule, not the join's other refusals. | Overclaim | Written for the shared rule, generalised to the whole join. | Narrowed to "a battle the oath keeps him out of". |
| F3 | LOW (Data flow, Design) | The new rule's name nearly matched the existing sit-out policy, which decides something else; seven hand-written copies of the commander-fitness check (six in the service, one in a rescue cheat), one of which drifted (the defect this issue fixed). | Naming and duplication | Each copy was written next to its caller. | `OathSitOutRule`; one `CommanderSnapshot.IsFit` used by all seven sites, with a truth-table test that also pins it against the tick snapshot's `IsFollowable`. |
| F4 | LOW (Standards, Strings) | A test name claimed more than it asserted; comments that repeated code or other summaries; the new key was not listed in the translation backlog. | Accuracy | Routine. | Test asserts the skipped calls; comments trimmed; key listed on #939. |

## Feedback memories to codify

None.
