# RCA: The column line on the status board (#838, deep-review, 2026-10-24)

## Top-line

`/deep-review` on #838 ran lenses 1, 2, 5 and 7 in a first wave and lenses 3, 4 and 6 in a second, which
also served as the convergence pass for this small change. It found **no engine incompatibility and no
code defect**: the engine's party behaviour text prints only fixed phrases and names, so an unchanged text
compares equal and the board does not rebuild every refresh, and every engine read sits inside the
adapter's try/catch. The builder already kept the text render off the commander snapshot that the
reconciler and the tick read.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | LOW (Data flow, Engine compat, Standards) | Two comments claimed the pump never renders text; the board's 2-second status tier now renders one behaviour text. | Comment describing an older path | Written for the tick snapshot, not re-read for the status tier. | Comments say which tier pays. |
| F2 | LOW (Standards, outside the lens) | A party state that makes the engine's behaviour text throw would log an error on every 2-second refresh. | Repeating error log | The catch logged every failure. | The adapter logs a failure once, until the next successful read. |
| F3 | LOW (Engine compat) | After a battle, a leftover battle army can make the line name the commander's home settlement (its seeded objective). | Cosmetic, rare | Battle armies are seeded with a placeholder objective. | An in-game check; no code guard (the simplicity criterion). |
| F4 | LOW (Design, Completeness, convergence) | The line sat after rank, pay and duty instead of under the activity sentence it adds to; in a settlement it would repeat the board's "rests inside X" (vanilla blanks it there); the no-party gate and half of the snapshot pin had no test; a garbled doc sentence; a comment said the reconciler never renders text. | Layout and test gaps | Placed where the kit line had been composed. | Moved under the activity line, dropped in a settlement, tests added, wording fixed. |

Noted, not changed: during the commander grace state, a party that exists but is inactive shows the
"march with" activity and a column line (a pre-existing mismatch with the grace exit; reachability
unverified).

## Feedback memories to codify

None.
