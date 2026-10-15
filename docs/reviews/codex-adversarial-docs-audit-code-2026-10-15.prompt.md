ADVERSARIAL REVIEW: three small code changes from the War Chronicle documentation audit (issues #765, #792)

REVIEW ONLY. Do not modify, create or delete any file anywhere. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-docs-audit-code-2026-10-15.md, so never write that path yourself.

Scope: only the C# in three commits of this worktree (branch docs/audit-765): `git show 8fd7b2a60 629dcd086 e9854bbd0`. The other commits on the branch are documentation; ignore them. Target and installed game: Bannerlord v1.5.5 (engine source C:\Users\mikew\.taom-src\v1.5.5).
- 8fd7b2a60: Eye of Sauron MCM hint text (the speed floor applies within 1.5 times the seeing range), console usage text, comments.
- 629dcd086: `taom.chronicle_open <id>` is now Tier C: without a literal positional `confirm` it prints what would happen and changes nothing; with `confirm` it acts. A dryRun parameter goes through the chronicle service.
- e9854bbd0: EncounterAdapter and PartyItemRosterAdapter read the main party through `Campaign.Current?.MainParty?.Party` instead of `PartyBase.MainParty?.` (the getter returns null without a campaign and otherwise dereferences Campaign.Current.MainParty, so it throws while the stored main party is null); a source scan test also fails on `PartyBase.MainParty?.`.

READ FIRST: AGENTS.md, .ai/review-reference.md, docs/features/dev-console.md (tiers), docs/features/war-chronicle.md (chronicle_open).

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line):
S1. The dry run mutates state on some path (narrative occurs, siege opens, Fell at open, focus, announcements, ledger, momentum, effects) or reports something the real run would not do.
S2. `confirm` parsing: case, position, extra arguments, an id named "confirm".
S3. The adapter reads: any behaviour difference when a main party exists; any caller that relied on the old exception or null.
S4. The scan: false negatives (other spellings) and false positives (comments, strings).

OUTPUT SECTIONS: findings with severity, file:line, quoted code, reproduction and fix; say "no finding" plainly per suspect. Return the full report as your FINAL MESSAGE.
