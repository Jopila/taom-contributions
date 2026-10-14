ADVERSARIAL REVIEW: War Chronicle events, army focus and momentum award (issue #765, Batch B: milestones M3, M4, M6) plus two maintainer decisions

REVIEW ONLY. Do not modify, create or delete any file anywhere. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-war-chronicle-events-2026-10-14.md, so never write that path yourself.

Scope: `git diff ac0a3e0c0..HEAD` in this worktree (branch fix/chronicle-decisions-765): trunk commits 84f5e63a9 (the feature) and 7e8451555 (deep-review fixes), plus the local commit bad32ad08 (decisions C04 and C07). Ignore every other change. Target Bannerlord v1.5.4: engine semantics from C:\Users\mikew\.taom-src\v1.5.4 and E:\Decompiled_Bannerlord\_categories_v1.5.4 (the installed game is v1.5.5; do not report that).

Feature: 13 data-driven War of the Ring events in Main/_Module/ModuleData/war_chronicle/war_chronicle.json (narrative events and 7 siege windows) run by Main/Features/WarChronicle/Events/ (ChronicleService state machine, config provider and validator, state store, outcome service, announcement queue and presenter, router behavior WarChronicleEventsBehavior) and saved in the WarChronicle payload (WarChronicleBehavior is the one SyncData owner). Anchors: campaign start, Phase 1, Phase 2, or an earlier event plus an offset. A siege window opens at Phase 2 or later, records assaults from siege starts, resolves Fell on capture by an attacker, and closes Held (assaulted, still the defender's; deferred while an attacker's siege still runs, capped at one extra window) or Lapsed. Outcomes apply small timed effects through the existing War Chronicle effect registry (volunteers, prisoner escape; the player's own clan never gains or loses), an optional momentum award (MomentumAwardService, MomentumActionType.Chronicle) and a ledger line. Army focus (Main/Features/ArmyTargeting/ArmyTargetFocusRegistry.cs) raises attacker Besieger and defender Defender army scores on an open target. Decisions in bad32ad08: C04, a phase anchor is max(configured phase day, first day the chronicle saw the phase minus 1), saved per campaign; C07, a siege whose target an attacker already owns at open resolves Fell (effects, momentum, fell line, no window) so its child events still open.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST: AGENTS.md, .ai/review-reference.md, docs/features/war-chronicle.md, docs/features/army-targeting.md (focus), docs/features/war-of-the-ring-momentum.md (Chronicle type), .claude/rules/csharp-architecture.md.

DESIGN DECISIONS (not defects): no player gate; small temporary effects; every siege pays both sides (Held: defender volunteers, attackers escape; Fell: the reverse); Lapsed pays nothing; events never declare war or peace or move settlements; a save from before the chronicle skips events whose time has passed (D5); momentum victory stays off; C04 and C07 as above.

KNOWN SUSPECTS (CONFIRM or DISPUTE each; cite TAOM and v1.5.4 engine file:line):
S1. Resolution: OnSettlementOwnerChangedEvent fires twice per conquest; a rebellion, a gift, a destroyed defender, a capture by a third kingdom, the deferred close and its cap, a siege lifted and restarted. Any path that pays twice, pays the wrong side, or never resolves.
S2. Persistence: the chronicle and phases sections in the payload (codec, section isolation), the constructor reset, a load mid-window, a second campaign in one process, adoption of old saves, War Ended.
S3. C04: the first-seen day minus 1 against the daily listener order (chronicle step vs WarOfTheRingBehavior), test mode, a phase slider lowered or raised mid-campaign, the war disabled and re-enabled, a load before the phase is seen.
S4. C07: an attacker-held target at open with a dead or eliminated defender; effects for an eliminated kingdom; momentum before the war starts.
S5. Announcements: the queue, the map-free check, the inquiry, co-op authority in the callback, text resolution; any engine call off the main thread.
S6. Army focus: the score path cost when no window is open, the zero-score veto, Army Strategic Intelligence off, a stale focus after a load or War Ended.
S7. Content: war_chronicle.json ids (settlements town_V2, town_L1, town_EW3, town_S3, town_EW4, town_AN3, town_EW1; kingdoms) against the repo and the live TAOM_Map install, Hostile pairs in diplomacy.json, and every taom_wc_* key used exists once in Main/_Module/ModuleData/taom_wotr_strings.xml.

REQUIRED SECTIONS: 1. VANILLA CODE (paste: OnSettlementOwnerChangedEvent callers and ChangeOwnerOfSettlementAction; SiegeEvent start; the army target score path TAOM multiplies; InformationManager.ShowInquiry). 2. DEEP ANALYSIS: a test-mode campaign (phase days 1 and 3) walked through the 13 events; East Osgiliath already Mordor's at open; a save and reload mid-window; the war switched on at day 100. 3. FINDINGS: severity, TAOM file:line, engine file:line, quoted code, reproduction, fix; UNVERIFIED where not proven.

QUALITY GATES: CONFIRM or DISPUTE every suspect; no design decision reported as a defect; say "no finding" plainly.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE.
