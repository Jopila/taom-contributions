ADVERSARIAL REVIEW: follow-up fixes for the War Chronicle and the Eye of Sauron (issue #792; engine quirk #793)

REVIEW ONLY. Do not modify, create or delete any file anywhere. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-review-followups-2026-10-14.md, so never write that path yourself.

Scope: `git diff a9437a492..HEAD` in this worktree (branch fix/review-notes-ledger, 14 local commits: 2 test-infrastructure, 5 hunt, 7 ledger; `git log --oneline a9437a492..HEAD`). Target and installed game: Bannerlord v1.5.5 (engine source C:\Users\mikew\.taom-src\v1.5.5 and `pwsh tools/taom-src.ps1 path <Type>`).

What changed:
-- Test infrastructure: TAOM.Tests/Infrastructure/EngineDataStore.cs, one fake of the engine's behavior save store (save adds each key once and a duplicate throws; load of a missing key returns false and leaves the ref), replacing six private fakes; RepoPaths.MentionsOutsideComments, a raw-text prefilter before comment stripping in six wiring scans.
-- Eye of Sauron hunt: tests that pin the typed wait decision; HunterPartyAdapter.IsMapFree checks Campaign.Current?.MainParty; Refuge text (the engine saves MobilePartyAi.DoNotMakeNewDecisions); docs. An earlier per-culture adapter cache was reverted (ConditionalWeakTable locks on every read on .NET Framework, on the parallel party tick).
-- War Chronicle: Main/Core/Persistence/SyncDataChunker.cs, one guarded chunked SyncData loop (counts below 0 or above 4,096 ignored with a warning) used by WarChronicleBehavior and WarOfTheRingMomentumBehavior with byte-identical keys; WarChronicleDailyGate, so War Chronicle's daily work (the tick service: escape pass, rally, ledger; and the chronicle's daily step) runs at most once per campaign day, the last-run days saved in a new `daily` payload section; a `[WarLedger] v=1 t=load cid=<cid> day=<S>` line written on OnGameLoadedEvent on the authority; tools/analyze_war_ledger.py drops a campaign's records after day S at a load marker, keeps day S from both runs, and no longer rewinds on day numbers; MessengerCampaignBehavior's guard and a source scan for `PartyBase.MainParty == null` across Main; docs, RCAs, a release note on Arthedain's towns, a re-baselined translation coverage file.
Engine fact behind the gate and the marker (verify it): v1.5.5 Campaign.CreateCampaignEvents (Campaign.cs:1022-1030), run on every load, sets the daily event's initial wait to the ELAPSED part of the day, and MBCampaignEvent schedules Now + InitialWait (MBCampaignEvent.cs:32); so a save at fraction f of day S ticks next at S + 2f.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST: AGENTS.md, .ai/review-reference.md, docs/features/war-chronicle.md, docs/reviews/rca-war-chronicle-batch-a-2026-10-09.md and rca-eye-of-sauron-2026-10-14.md (follow-up sections), .claude/rules/csharp-architecture.md (session reset, NaN gates).

KNOWN SUSPECTS (CONFIRM or DISPUTE each; cite TAOM and v1.5.5 engine file:line):
S1. The daily gate: a new campaign, a load of a save without the `daily` section, a load mid-day before and after midday, the chronicle lane and the tick lane drifting apart, a NaN or negative day, a second campaign in one process (constructor reset), co-op clients. Any path where daily work runs twice on one day or is skipped for a whole day.
S2. Save compatibility: WarChronicle and momentum keys byte-identical to the trunk; old saves (no `daily` section, momentum _v2 keys) load; a corrupt count is ignored, not thrown; section isolation of the new section.
S3. The load marker: OnGameLoadedEvent order against the first daily tick; the day value (floor of elapsed days from CampaignStartTime, same as the daily snapshots); authority gating; the analyzer's rule (drop day > S, keep day S, exact repeats collapse) against real line orders, multiple loads in one log, a load of a newer save, logs without markers.
S4. EngineDataStore: does it match CampaignBehaviorDataStore.BehaviorSaveData exactly; did any migrated suite depend on the old fakes' overwrite or null-skip behaviour.
S5. The source scans: false negatives (a pattern written differently) and false positives (comments, strings).
S6. Docs and records: claims that the code contradicts.

REQUIRED SECTIONS: 1. VANILLA CODE (paste: Campaign.CreateCampaignEvents and its load caller; MBCampaignEvent; CampaignBehaviorDataStore.LoadBehaviorData and BehaviorSaveData.SyncData; SaveContext.SaveStringTo). 2. DEEP ANALYSIS: a save at 06:00 on day 40 loaded; a save at 18:00 on day 40 loaded; a pre-gate save loaded; three loads in one log for the analyzer. 3. FINDINGS: severity, TAOM file:line, engine file:line, quoted code, reproduction, fix; UNVERIFIED where not proven.

QUALITY GATES: CONFIRM or DISPUTE every suspect; say "no finding" plainly.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE.
