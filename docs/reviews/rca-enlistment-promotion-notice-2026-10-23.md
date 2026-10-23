# RCA: Promotion notice and the kit line (#835, deep-review, 2026-10-23)

## Top-line

`/deep-review` (seven lenses in two waves, then a convergence pass) on #835 found **no engine
incompatibility** and **two MEDIUM findings**, both fixed before the first commit, plus a set of LOW
findings. The larger one was a design premise taken from the plan without checking it: a gate against a
double notice that the promotion code makes impossible.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Standards, Data flow) | `PromotionNoticeGate` could never suppress a notice. Every promotion is exactly one rank up (the ladder validator forces `ToRank == i + 1`, `EnlistmentContentConfigProvider.cs:364`), so a battle and a daily check on one day report two different ranks; the rank only falls through a reset or a load, which reset the gate too. The gate added a class, an event subscription and a maintenance-service dependency on the presentation layer. | Code for a case that cannot happen | The plan's risk note ("the notice may show twice") was built without reading how the rank advances. | Gate and all its wiring deleted; the doc's in-game check that could not pass was removed. Lesson appended to `lessons/testing-qa.md`. |
| F2 | MEDIUM (Data flow, Engine compat) | A battle could promote the soldier while his commander was unavailable (captured or partyless, the grace state), which the daily check already refused; the new notice then showed the captive lord's portrait and promised a quartermaster he could not reach. A lord captured in the same battle is also still "serving" when the battle ends. | Two paths, one rule | The payout checked only `IsEnlisted`, which includes the grace state. | The payout skips the promotion in the grace state; the notice falls back to the plain message unless the commander exists, is alive and is free. |
| F3 | LOW (several) | `KitWaits` re-derived the drawn tier instead of asking the ledger the quartermaster uses (an undefined stored tier disagreed); the notice showed for about 1.6 s (four other TAOM notices add 3 s); the adapter doc claimed a failed display is detected (only a throw is); a wrong engine comment; the board key named like the service-record popup keys; no wiring test for the announcer. | Accuracy and simplicity | Various. | `KitWaits` over the ledger; 3000 ms; doc and comment corrected; key renamed before translation; wiring test. |

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources and the 13 strings files byte by byte (a rename script had stopped partway and a second one finished it): every other row is identical to HEAD, the renamed key appears once per file and the old one nowhere, and the gate and its wiring are gone with the touched maintenance files identical to HEAD. It found four LOW wording items and one nit (the lesson said the gate shipped; the 3 s claim and its test name; a "not built" item the board already shows; the payout skips only the promotion in the grace state, not the whole payout), all fixed.

## Root-cause pattern

F1 and F2 share a cause: the change trusted a description of how promotions happen (the plan's risk
note, the `IsEnlisted` name) instead of reading the two code paths that promote. One read of
`EvaluateAndApply` and its two callers answered both.

## Why each lens caught or missed these

- **Standards:** F1 and the layering edge it created; the adapter doc.
- **Engine compatibility:** the notice timing, the undetectable display, F2's same-battle capture case.
- **Efficiency:** no findings.
- **Completeness:** the tests and docs the fixes need, the wiring test, the stale issue body.
- **Data flow:** F1 independently, F2, the undefined tier.
- **Design:** `KitWaits` over the ledger, the gate wiring, the key name.
- **XML:** no findings.

## Feedback memories to codify

None; the lesson covers the pattern.
