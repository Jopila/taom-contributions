ADVERSARIAL REVIEW: the Eye of Sauron hunt (issue #765, milestone M8) plus three maintainer decisions

REVIEW ONLY. Do not modify, create or delete any file anywhere. Return your whole report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-eye-of-sauron-2026-10-14.md, so never write that path yourself.

Scope, two ranges in this worktree (branch fix/hunt-decisions-765): `git diff 8b26fcbb0..ac0a3e0c0` (trunk commits 34b0e25a9, the feature, and ac0a3e0c0, its deep-review fixes) and `git diff 7e8451555..HEAD` (local commits 26718988b and 33582471a, the decisions). Review the code as it stands at HEAD. Target Bannerlord v1.5.4: engine semantics from C:\Users\mikew\.taom-src\v1.5.4 and E:\Decompiled_Bannerlord\_categories_v1.5.4 (the installed game is v1.5.5; do not report that).

Feature: a player whose deeds (won battles, sieges against the other side) draw attention is hunted. A Free-side player whose kingdom is at war with the hunter's kingdom is hunted by one of the Nine (the Witch-king first; Dol Guldur's wraiths lead Dol Guldur troops) with high-tier troops; an Evil-side player by Glorfindel (lord_R2_1) with high-tier Elven troops. A player-founded kingdom is sided by culture. A hunter who leads a party has it re-tasked and topped up; a free hunter gets a party spawned out of sight. Out of sight the hunter moves closer hour by hour (MobileParty position plus path rebuild); within 1.5 times the player's sight range it gets a speed floor above the player's speed (TaomPartySpeedModel seam, worker threads, a snapshot published on the main thread); a size allowance goes through TaomPartySizeModel. Single-player only (BannerlordCoop and Bannerlord Together stand it down). Decisions at HEAD: E06, the hunt ends after a battle only when the hunter took part and his side lost, or it was the player's battle and someone won (a flee keeps the hunt); E09, the speed floor applies from 1.5 S inward; E17, no hunt starts while the player rides in another lord's army, and during a hunt the hunter releases its pin and chase while the player is in such an army and is re-pinned once when he leaves. Code: Main/Features/EyeOfSauron/**, Main/Adapters/HunterPartyAdapter.cs and the Hunt*.cs DTOs, the two model seams, the MCM group War of the Ring/Eye of Sauron in Main/Features/TaomSettings.cs. Design and decisions: docs/features/eye-of-sauron.md.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST: AGENTS.md, .ai/review-reference.md, docs/features/eye-of-sauron.md, .claude/rules/csharp-architecture.md, .claude/rules/gamemodels.md (rule 9), .claude/rules/harmony-patches.md (threads).

DESIGN DECISIONS (not defects): everything listed above as the feature and the decisions, the 60-day cooldown after a real end, announcements at start and end only, the hunt on by default.

KNOWN SUSPECTS (CONFIRM or DISPUTE each; cite TAOM and v1.5.4 engine file:line):
S1. Save corruption: any path that gives a hero a second party, spawns for a prisoner, fugitive, governor or a hero in a settlement, or leaves a TAOM pin (SetDoNotMakeNewDecisions) set after the hunt, a load, a second campaign, a stand-off, or the hunter's death.
S2. Threads: the speed and size seams read only the published snapshot; any engine or MCM read on a worker thread; torn or stale snapshot reads; the floor band edges and NaN.
S3. Relocation: landing on another island, in water, in an encounter radius, on a settlement, while the hunter or player is in an army or a map event; attached parties.
S4. Party size and top-up: the allowance through AiPartySizeService, the daily shed, troop weight, wages, food.
S5. Ends and the pin: every MapEventEnded (flee, third-party battles, sieges), destruction, capture, death, war ending, kingdom loss, co-op going live, timeout, the E17 stand-off and resume; the pin is released exactly once and only when TAOM set it; pinnedByUs after a load.
S6. Gates: the war gate (FactionManager), player side by kingdom or culture, the player-founded kingdom, a kingdomless player, BannerlordCoop and Bannerlord Together detection.
S7. Tests: mutants that survive (pin release, snapshot publish, end gates, stand-off resume).

REQUIRED SECTIONS: 1. VANILLA CODE (paste: SetPartyAiAction.GetActionForEngagingParty, MobilePartyAi.SetDoNotMakeNewDecisions, MobileParty Position setter and RecalculateLongTermPath, MobilePartyHelper.SpawnLordParty, HeroSpawnCampaignBehavior spawn guards, DefaultPartySpeedCalculatingModel.CalculateFinalSpeed, PlayerEncounter.LeaveBattle, the party size shed). 2. DEEP ANALYSIS: a Free player at war with Mordor wins 6 battles; the Witch-king leads an army when the hunt starts; the player flees the hunter's battle; the player joins another lord's army mid-hunt and leaves; the player rests in a town for 10 days; a save and reload mid-stand-off; the hunter dies in battle. 3. FINDINGS: severity, TAOM file:line, engine file:line, quoted code, reproduction, fix; UNVERIFIED where not proven.

QUALITY GATES: CONFIRM or DISPUTE every suspect; no design decision reported as a defect; say "no finding" plainly.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE.
