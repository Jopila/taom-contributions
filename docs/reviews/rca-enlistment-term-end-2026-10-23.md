# RCA: The end of the term, renew or leave (#836, deep-review, 2026-10-23)

## Top-line

`/deep-review` on #836 ran all seven lenses (lenses 4 and 6 resumed after a session limit) and a
convergence pass. It found **four MEDIUM defects** and a set of LOW findings, all fixed before the
first commit.

The finding that mattered: the offer put "Leave with honor" on the inquiry's second option, and the engine
runs the second option when the player presses Escape or gamepad B, whatever the button state. One reflex
keypress would have ended a year of service and reset rank, trust and kit. (Enter runs the first option,
the deliberate confirm, as in the desertion prompt.)

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Engine compat, Data flow) | Option B of a two-option inquiry is the negative option, and `GauntletQueryManager` runs it on the Exit hotkey (Escape, gamepad B). "Leave with honor" sat there and discharged the soldier. | Engine input mapping | The plan treated the two buttons as symmetric; the desertion prompt already kept the safe choice on B, but no rule said why. | Options swapped: B is "Sign on again" (Escape keeps him in service), A is "Leave with honor"; pinned by a test. Lesson appended to `lessons/localization-ui.md`. |
| F2 | MEDIUM (Data flow) | `taom_enlist_term_served` was left registered with no code using it; no gate catches an orphaned row, and the translator serves its cache by id, so reusing the id would show the old sentence in 12 languages. | Orphaned localization key | The old toast was replaced, and its row was kept "in case". | The row, its 12 language rows and the cache entries are retired, with an absence test. |
| F3 | MEDIUM (Completeness, Design) | The bonus shortfall went to arrears under the wage cap, so anything above 14 days of wage was dropped while the log said "owed"; the bonus also skipped the commander's 500-denar reserve that wages keep, so it could empty his purse. | Second copy of the pay rules | The bonus reused only the transfer step, not the floor or the arrears rule. | The bonus is a gift: paid from the purse above the wage floor, never deferred; the notice states the amount received. |
| F4 | MEDIUM (Completeness) | The in-game check said to shorten `ContractDays` in the config, which is a compiled default, so the check took 365 in-game days. | Untestable check | The recipe was written without opening the provider. | A host-only console command, `taom.set_term_end`. |
| F5 | LOW (several) | Leave could discharge in a battle that began in the same frame after the offer showed; the bonus was created from nothing while wages come from the commander's purse; a saved once-per-term stamp could lose the offer (Save And Exit hides an open inquiry without an answer), while the engine can still run the daily tick twice in one frame; no commander gate (a missing, dead or captive lord); a one-implementation interface; the policy in another class's file; unused usings in a hook one line under the limit; a now-dead daily flag; adapter reads before the cheap checks. | Accuracy and simplicity | Various. | Leave requires `EnlistedAttached` and re-arms the offer; the bonus is paid like wages with a notice; the guard is kept in memory only, reset with the session; a commander gate; the console command is `taom.set_term_end`; concrete service; own file; dead code deleted; `IsDue` first. |

Accepted and documented, not changed: renewing holds the soldier to nothing; he may ask for release at
once and keep the bonus, which is bounded (at most 30 days of wage, once per year served). This keeps the
Escape choice cheap to undo.

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources: the option layout and the
engine's Exit and Confirm hotkeys, the bonus paid above the wage floor and never deferred, the daily wage
unchanged from HEAD, the gates, the in-memory guard, the console command, and the 13 strings files and 12
caches byte by byte. It found four LOW items, all fixed: the docs named only Escape; the battle-encounter
gate is dormant until #796; the offer named the full bonus while a poor commander pays less ("up to" now);
the console command name broke the naming table. It also removed a test for a key that never shipped.

## Follow-ups

- No gate catches a registered localization row that no code uses; F2 retired one such key by hand. A test or a lint check is owed (issue on the maintainer's word).
- The wage ledger shrinks banked arrears on a day the wage is paid (`ServiceRewardService`, `alreadyBanked`); pre-existing, outside this change.

## Root-cause pattern

F1 and F2 share a cause: a choice the engine makes on its own (which button Escape presses, which cached
row the translator serves) was not part of the design. Both were found only by reading the engine's input
manager and the translator.

## Feedback memories to codify

None; the lesson covers F1.
