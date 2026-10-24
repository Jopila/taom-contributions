# RCA: Remember past service when a soldier re-enlists (#837, deep-review, 2026-10-24)

## Top-line

`/deep-review` (seven lenses in two waves, then a convergence pass) on #837 found **no engine
incompatibility** but **one HIGH lifecycle defect**, found independently by four lenses, plus one MEDIUM
and a set of LOW findings, all fixed before the first commit.

The HIGH: the new service history lived in a process-lifetime singleton store and survived the store's
`Clear()`, while the engine only loads a behavior's saved data for a saved campaign that holds it. A new
campaign, or a save with no enlistment data, therefore inherited the previous campaign's entries. Lords,
clans and kingdoms have fixed ids, so those entries matched: the new campaign restored old ranks, refused
kit the hero never drew, and wrote the old history into its first save.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Standards, Engine compat, Efficiency, Data flow) | The history survived `Clear()` on purpose (so a discharge keeps it), and `Clear()` was also the new-campaign reset; `Deserialize` was the only replacement, and it runs only when a save holds the data. | Singleton per-campaign state without a session reset (the rule in `.claude/rules/csharp-architecture.md`) | The design asked "what survives a discharge" and never "what survives a new campaign"; the two used the same method. | `Clear()` drops the history (both new-session paths call it); a discharge resets only the record. Future-dated entries are ignored. Lesson appended to `lessons/state-lifecycle-save.md`. |
| F2 | MEDIUM (Standards) | Four singletons took the history service as an optional `= null` parameter. DryIoc then passes null when anything deeper in that graph fails to resolve, and `Validate` reports no error, so the feature could switch itself off silently. | Optional dependency on a container singleton | Optional parameters kept ten test constructions unchanged. | Required parameters, test substitutes, and a wiring test. |
| F3 | LOW (several) | The kit seed used the restored rank's tier (one tier too low) and the fact was lost after a restored term; expiry could cut a desertion cooldown short; a new main hero (heir, Player Switcher) inherited the old hero's ranks and cooldowns; two owners for which discharges write history; the kit-stayed rule written four ways; test gaps (verdict sets, the step order, the round trip, the tie-break); the desertion warning did not name the new cost; the issue and doc said "2 years" for 730 days (a game year is 84 days at normal pace, 24 in Fast mode); the new desertion warning first claimed no service would be remembered, while an older honourable entry still counts. | Accuracy and simplicity | Various. | The entry stores the kit tier the ledger recorded; cooling desertions are never pruned; entries carry the hero id; one owner and one rule; the tests; a third appended warning sentence; days, not years. |

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources: the new-session reset (a save with no
enlistment data never reaches `SyncData`, so the session-launch `Clear()` resets it), the required
parameters, the one kit rule, the single owner of quiet reasons, the kit tier and its tie-break, the shared
cooldown predicate, the hero ids, the second append pair and the 13 strings files byte by byte. It found no
code regression; one warning sentence claimed more than the policy (narrowed to "this term", with a policy
test) and three doc details, all fixed.

## Root-cause pattern

F1 is the third time this pattern has reached review in this batch (#835's gate, #836's guard, #837's
history), and it repeats the #861 lesson in `lessons/state-lifecycle-save.md`: state that must outlive one event was put in a singleton without asking which lifetime it has.
Here the answer was "one campaign", and the code had "one process".

## Feedback memories to codify

None; the lesson covers it.
