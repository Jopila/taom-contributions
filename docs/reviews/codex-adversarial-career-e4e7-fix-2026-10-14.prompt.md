You are an adversarial reviewer for TAOM (Tales From the Age of Men), a Lord of the Rings total conversion for Mount & Blade II: Bannerlord, C# .NET Framework 4.7.2. Assume the authors missed something. Read AGENTS.md and .ai/review-reference.md first and follow the reviewer role: report defects with evidence; do NOT edit any file.

Scope: ONE commit, 59c1803d2 on branch feature/career-forks in this worktree (`git show 59c1803d2`). It fixes the P2 your previous pass reported (docs/reviews/raw/codex-adversarial-career-e4e7-2026-10-14.md, final section): the career tree-version migration ran at session launch for Hero.MainHero only, so a co-op hero or a hero the player takes over later was never migrated. The fix adds CareerLifecycleService.MigrateAllTreeVersions(playerHeroStringId), which migrates every hero in ICareerDataService.GetAllData() (keys copied first) and returns only the player's refund count; CareerCampaignBehavior.OnSessionLaunched calls it between RepairForeignChoices and RefreshCache and shows the message only when that count is above zero; ICareerLifecycleService now exposes MigrateAllTreeVersions in place of MigrateTreeVersion. Installed game: Bannerlord v1.5.5; engine facts only from the installed DLLs (decompile with ilspycmd).

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

ALREADY KNOWN, do not report: the 9 dotnet tests that also fail on pure trunk a9437a492 (Harad, Umbar, Shaghana, Abanissa, Aserai data); the live Armory's Harad rewrite (environment).

KNOWN SUSPECTS (CONFIRM or DISPUTE each with evidence):
1. Does the fix close your scenario completely: a previously reconciled possessed hero (PlayerPossessionBehavior.CheckForPossession returns early for it), other players' heroes on a co-op host, a hero the player switched away from with Player Switcher? Is any hero that holds career data at session launch still left out (data restored after OnSessionLaunched, a hero whose entry has an empty career)?
2. Can migrating a non-player hero do harm: a dead hero's leftover entry, a hero whose career no longer exists, a hero holding a career id from another module, the GetOrCreateData calls inside MigrateTreeVersion and Stamp adding dictionary entries while the copied key list is walked?
3. Class hunt: the foreign-choice repair (RepairForeignChoices) and the legacy fallback (AssignFallbackCareerIfMissing) still run for the main hero only. Is either a real defect of the same class, given what each exists to repair (read their comments and docs/features/career-system.md)?
4. Your earlier boundary note: OnCareerSelected on a hero that already holds choices certifies them with Stamp. After this fix, is there still a reachable path that reassigns a career over old-tree picks within a session?

REQUIRED SECTIONS:
- DEEP ANALYSIS of the four suspects with concrete scenarios.
- FINDINGS: each with severity (P1/P2/P3), file:line, evidence, and a fix; say UNVERIFIED where you could not prove it. An empty list is a valid answer.

QUALITY GATES: every claim cites a file:line you read; no style-only findings.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-career-e4e7-fix-2026-10-14.md; do NOT write that file yourself.
