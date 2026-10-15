ADVERSARIAL REVIEW: four follow-up commits (#796 enlistment battle flag; #792 main-party reads and chronicle_open dry run)

REVIEW ONLY. Do not modify, create or delete any file anywhere. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-enlistment-battle-flag-2026-10-15.md, so never write that path yourself.

Scope: `git show 4c573a776 8feb8a150 b70ff43fc d02c50fdc` in this worktree (branch docs/audit-765). Target: Bannerlord v1.5.5 (engine source C:\Users\mikew\.taom-src\v1.5.5).
- 4c573a776 (#796): EncounterAdapter.GetOwnership now passes isBattleEncounter to EncounterOwnershipSnapshot; before, it was computed and dropped, so Enlistment rule R1b (EncounterOwnershipPolicy) and EnlistmentLoadNormalizer never saw a battle encounter. A source test pins the argument; docs/features/enlistment.md updated.
- 8feb8a150: tests that the chronicle_open dry run creates no row, notes no phase day, and predicts what the confirmed open does.
- b70ff43fc: PlayerEquipmentAdapter, TroopWeightDisplayHook and EnlistmentBattleBehavior read the main party through Campaign.Current?.MainParty; the scan also catches parenthesised getters, reversed null comparisons and `??`.
- d02c50fdc: dry-run message texts, an unused using, a wrapped line.

READ FIRST: AGENTS.md, .ai/review-reference.md, docs/features/enlistment.md (R1b, the encounter ownership rules, #551, #577).

KNOWN SUSPECTS (CONFIRM or DISPUTE each with TAOM and v1.5.5 engine file:line):
S1. #796 turns R1b on for the first time. Trace every consumer of IsBattleEncounter (EncounterOwnershipPolicy, EnlistmentLoadNormalizer, any other) and every state in which isBattleEncounter is now true (field battle, siege, siege aftermath, the loot screen, a raid, a hideout, a tournament, a conversation inside an encounter): can R1b now keep an encounter alive that must end (a stuck encounter, the #551 latch, a discharge that never completes)? What does PlayerEncounter.Battle hold in each window (v1.5.5 PlayerEncounter)?
S2. The adapter read changes: any behaviour difference when a main party exists; any caller relying on the old throw or null.
S3. The scan regex: false negatives and false positives after the extension.
S4. The new dry-run tests: do they fail when the dry run mutates state or predicts wrongly?

OUTPUT: findings with severity, file:line, quoted code, reproduction and fix; "no finding" plainly per suspect. Return the full report as your FINAL MESSAGE.
