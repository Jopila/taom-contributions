# RCA: Aura of Dread made local (deep review, 2026-10-15)

## Top-line

Mike reported that the Aura of Dread (the Nazgûl, the Witch-King, Sauron) acted on the whole
battlefield. The aura already drained only inside 12 m, but its morale floor of 0 let it rout
troops by itself, and every rout feeds vanilla's flee contagion
(`AgentMoraleInteractionLogic.OnAgentFleeing`), which reaches past any radius once a line thins.
Mike chose "weaken, never rout": radius 12 to 5 m, inner radius 4 to 2 m, morale floor 0 to 10, and
the MCM radius renamed `DreadAuraRadius` to `DreadAuraCloseRadius` so the new default reaches saved
settings files.

A seven-lens `/deep-review` of that change (uncommitted working tree) found **no CRITICAL or HIGH,
1 MEDIUM and a set of LOW findings**. Mike decided three of them: lower the radius ceiling from
30 m to 15 m (the engine's grid limit), keep the floor at 10 although one full Nazgûl Scream takes
a tier-3 troop held on it to the edge of panic, and file no GitHub issue yet. (The question put to
Mike said the Scream "breaks" that troop; the convergence pass showed it does so only within about
0.025 s of an aura pulse, so the real risk is smaller than he was told, and his choice stands.) All findings below are fixed in the same
working tree. DreadAura and `HotPathSettingsProvidersTests` 259/259 green; full suite 17,413 pass,
1 fail (`EveryLanguage_DeclaresARowForEveryEnglishKey`, another session's uncommitted Harad
translation rows), 6 skipped; `lint_docs --fail-on-drift` exit 0.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED | The radius ceiling was 30 m, but native `IMBMission.GetNearbyAgentsAux` searches its proximity grid only up to `ProximityMapMaxSearchRadius` (15.0 on v1.5.5, `.rdata` RVA 0xB2E120) and walks every agent in the mission beyond it. A radius from 15 to 30 cost a full agent scan on every pulse. A code comment, the feature doc and the MCM hint all said the call had no fallback; the hint gave 30 as the limit. Fixed: slider and both providers cap at 15, comments and doc rewritten, tests `GetConfig_RadiusOutOfRangeOrNonFinite_RevertsAndWarns (15.5)` and `Radius_McmAboveTheEngineGridLimit_ClampsTo15`. | Engine behaviour hidden behind a managed wrapper | The 2026-08-13 design read only the managed `Mission.GetNearbyAgentsAux` and concluded "pure native paging"; an ILSpy decompile cannot show a branch inside the native function. The claim then lived in three places, and this change rewrote the hint line and kept the clause, because the planner and the implementer trusted the existing comment. | New lesson, `lessons/adapters-taleworlds-api.md` "Before stating an engine call has no limit or fallback, read its native side". Proposed trap-index line below. |
| F2 | LOW | `DreadAuraService.cs:62` fell back to `?? 0f` for a null profile: a third copy of the floor default, left at the value that lets the aura rout. Unreachable today (the provider always returns a profile). Fixed: `?? new DreadProfileConfig().MoraleFloor`, test `ComputeDrain_NullProfile_StopsAtTheCompiledFloor`. | A default restated as a literal | The plan named the default's homes (the compiled default, the JSON, the MCM initializer) and nobody searched for a literal fallback of the same field; the sibling getters in `DreadAuraSettingsProvider` already read `new DreadProfileConfig().X`. | New lesson, `lessons/gamemodels-services.md` "A fallback reads the compiled default, never restates its literal". |
| F3 | LOW | The new text said every rout spreads "formation-wide". Vanilla fills the morale loss from the formation only for the slots that AI allies within 4 m leave empty (`AgentMoraleInteractionLogic.cs:79-84`, `:179`), so a dense block keeps one rout among its neighbours. Fixed in the lesson, the doc, `DreadAuraConfig.cs` and the JSON comment. | An engine fact relayed without its condition | The planner read the condition, then compressed it in the implementer's brief to "fills from the whole formation at any distance"; the implementer copied the brief into five places. | The lesson now quotes the condition. When relaying an engine fact to a writer, quote the `if`, not a summary (evidence-over-claims trap 1). |
| F4 | LOW | Hero morale resistance uses `HeroObject.Level / 4 + 1` in place of tier (`CharacterObject.cs:619-623`), so "an elven hero never" holds only from hero level 16. The doc's formula had said tier since 2026-08-13 (v1.4.8 is identical). Fixed in the doc, the hint, the JSON comment; test `ComputeDrain_ShippedFloorElfHero_NeverReachesTheFloor` pins levels 16 and 20. | Stale engine-constant table | The new numbers reused the doc's engine-constant table without re-reading the engine. | The table rows now say "re-read on v1.5.5". No new rule. |
| F5 | LOW | The published times assume initial morale 49.5. `CommonAIComponent.InitializeMorale` adds each component's `GetMoraleAddition()` (party morale and strength share in a campaign battle) and the morale model, so a party at morale 80 takes about 30 s, not 21.75, for a tier-3. Fixed: the doc states the assumption, the hint and JSON say "in an even fight". | Stale engine-constant table | Same as F4. | Same as F4. |
| F6 | LOW | Descriptions of the old contract survived: the feature-map row ("until formations break"), a stale line cite in `configs-factions-and-world.md:454`, floor-0 tests claiming doc rows that no longer existed, comments in `DreadMissionGate.cs` and `DreadPulseScheduler.cs`, the doc's mutation count and test counts. All fixed. | A contract change leaves its old descriptions | The plan listed only the feature doc and one config-reference line, with no search for the old contract's words ("rout", "break") across the feature folder and the feature map. | None: the Completeness lens is the gate for this, and it caught them. |
| F7 | LOW | No test kept the MCM defaults equal to the compiled JSON defaults (with MCM loaded, the MCM default is the radius players get), and no test proved `moraleFloor: 0` loads (the documented way back to the old behaviour). Fixed: `McmDefaults_MatchTheCompiledProfileDefaults`, `GetConfig_MoraleFloorZero_IsAcceptedAsTheRoutSwitch`. | Missing invariant tests | The plan tested the changed values, not the invariant between the two default homes; two sibling features already have that test. | New lesson, `lessons/testing-qa.md` "A setting with an MCM default and a JSON default gets a test that the two are equal". |
| F8 | LOW | The renamed MCM property had no comment saying why it was renamed; `lessons/state-lifecycle-save.md:730` asks for one. Fixed. | A documented rule not reached | The trap-index line "Persisted MCM defaults" says to rename and does not say to comment; the lesson that does lives in a category file nothing loads for `TaomSettings.cs`. | Proposed trap-index edit below. |
| F9 | LOW | Test and doc hygiene: a dead exit in `SimulateSecondsToFloor`, a third hand-written morale loop, a `-1` race-id sentinel, three floor-0 race tests duplicated by other tests, a doc bullet restating its section. Fixed; the never-routs proof now covers the 20/sec rate ceiling. | Hygiene | Written fast under one plan. | None. |

**Process:** no GitHub issue exists for the bug. Mike: not now (2026-10-15). It is owed before the
work is closed, with `triage-needs-ingame` until Mike's in-game check.

**Design decision, not a finding:** one full-strength Nazgûl Scream takes 25 / 2.5 = 10 from a
tier-3 troop, exactly the floor. Regeneration lifts a troop on the floor to about 10.1 between
0.25 s pulses, so the Scream leaves it at 0.01 to 0.1, at the edge of panic, and breaks tier-1 and
tier-2 troops (16.7, 12.5) outright. Sauron's Slam and race-ability fear also have no floor. Mike
kept the floor at 10: those effects reach 4 to 8 m, so their routs start next to the source.

**Convergence pass (Step 4.6):** one reviewer on the fix round found five LOW prose defects and no
code or test defect: the Scream overstatement above (D1), a tournament comment claiming combat could
rout entrants at the shipped floor although the tournament mission carries no
`AgentMoraleInteractionLogic` (D2, `TournamentMissionStarter.cs:87-99`), a v1.4.8 line number in a
v1.5.5 paragraph (D3), "design ceilings" contradicting the new "engine ceiling" comment (D4), and
this RCA citing the F1 lesson before it was written (D5). It also flagged the doc's 67.75 s elf
time: the test host simulates 67.5 s (checked by a one-run assertion at a 0.001 tolerance, all five
rows), so the doc now says 67.5. All six fixed.

## Root-cause pattern

F1, F3, F4 and F5 share one cause: **an engine fact copied from an earlier summary instead of
re-read at its source.** F1 came from a code comment, F3 from the planner's own brief, F4 and F5
from the feature doc's engine-constant table. Each source was once written from a real read, and
each lost something on the way (a native branch, an `if`, a hero clause, an addend). A rewritten
line re-asserts every engine fact it keeps, so the rewrite is the moment to re-verify it.

## Why each lens missed or found these

- **Agent 1 Standards:** flagged the hint's 30 m reason as contradicting the code comment, and
  sided with the comment, which was the wrong one; verified the contagion citation lines but not the
  summary's condition. Found F2, F6 (feature map), the dead exit.
- **Agent 2 Engine compatibility:** ran `native_decompile.py` and read the `.rdata` constant, so it
  found F1; also found F3, F4, F5 and the pre-existing doc errors listed under follow-up.
- **Agent 3 Efficiency:** found F1 independently through the native decompile.
- **Agent 4 Completeness:** found F6, the floor-0 test drift and the `moraleFloor: 0` test gap.
- **Agent 5 Data flow:** found F2, F7 (MCM defaults), F8 and the Scream arithmetic; called the
  native search fallback-free from the managed dump, which the orchestrator then disproved.
- **Agent 6 Design:** found F9 and confirmed option (C) is the smallest production change.
- **The original implementation** (planner plus implementer subagent) missed all of them: see the
  "Why missed" column.

## Follow-up (pre-existing, not applied; no issue filed, `/issue` needs Mike's word)

- `docs/features/dread-aura.md`: the `BattleEndLogic` paragraph names the wrong mechanism (v1.5.5
  `BattleEndLogic.OnEndMission` acts only once the enemy side retreats; routed troops leave through
  `BattleAgentLogic.OnAgentRemoved` and `MapEventSide.OnTroopRouted`), and its "Line 683" is now
  `MapEventSide.cs:636`.
- `docs/features/dread-aura.md:44-45` and the engine-constants row "Formation flee": the 30%
  `GetMovementTypeOfUnits` result changes no behaviour; its only reader, the order panel's view model
  (`MissionOrderTroopControllerVM`), stores it in a property nothing reads.
- `Main/Features/RaceAbilities/RaceAbilitiesConfigProvider.cs:31-33` cites "the Dread Aura's 30 m
  cap" and allows race-ability ranges up to 40 m, past the engine's 15 m grid limit.

**Status 2026-10-16 (Mike: fix all):** the first two are fixed in `dread-aura.md` from the v1.5.5
decompile; the roster section now also records that an ordered retreat (morale at or above 0.01) loses
no troop (`MapEventSide.cs:636`). For the third, the four race-ability radii (`rallyRadius`,
`kinBonus.radius`, `fearOnKillRadius`, `fearAuraRadius`, shipped at 12 m or less) are capped at
15 m, and so is the `KinWithin` trigger range (Mike, after the second review), because it runs the
same ally query. The enemy trigger `range` cap stays at 40 m: five shipped profiles sense enemies
past 15 m (Stand Fast 20 m for every ready dwarf, Forth Eorlingas 30 m when mounted, and Swiftness,
Serpent's Venom and Bard's Aim at 30 to 40 m with any ranged weapon), and Mike kept Stand Fast at
20 m until the full-walk cost is measured. Its comment now states that cost instead of the old 30 m
cap.

## Second review: the follow-up round (2026-10-16)

A six-lens `/deep-review` of the follow-up round found no CRITICAL or HIGH, 1 MEDIUM and a set of
LOW findings, all fixed in the same working tree. Mike decided two: cap `KinWithin` at 15 m, and
keep Stand Fast at 20 m until the full-walk cost is measured.

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| S1 | MED | The new `csharp-patterns.md` anti-pattern kept only "floor the morale" from the lesson's two-branch Prevent, so it condemned three shipped unfloored fear writes Mike chose to keep. | Condition dropped in a relay (repeat of F3) | The line was written from proposal 3 of this RCA, which had already dropped the branch; nobody re-read the lesson. | Lesson appended to `lessons/adapters-taleworlds-api.md`: a promoted rule is copied from its lesson's Prevent, every branch kept. |
| S2 | LOW | `dread-aura.md` said the 30% `Flee` value feeds the order panel's icon; the view model stores it in a property nothing reads. | Caller taken for a reader | The caller was found and the binding was not traced. | One-off; the doc row now names the caller and says nothing reads it. |
| S3 | LOW | `dread-aura.md` said a direct `Panic()` bypasses the panic handler's bracket; only `Retreat()` does (`Panic()` skips `CanPanic()`). Pre-existing text on a rewritten line. | Engine fact kept on a rewritten line | The rewrite kept the clause without re-reading `CommonAIComponent`. | Covered by the F1 lesson ("a rewritten line re-asserts every engine fact it keeps"). |
| S4 | LOW | "Five sight-line triggers" past 15 m was six triggers in five profiles, two of them melee (Stand Fast, Forth Eorlingas); javelins count as ranged. | Count not read | The planner counted from memory of the listing. | One-off; the comment and both docs list the five profiles. |
| S5 | LOW | `KinWithin` still allowed 40 m, although it runs the same ally query as `kinBonus.radius`. | Sibling input missed | The caps were changed per field, not per engine query. | Fixed with a test row; no rule. |
| S6 | LOW | The ordered-retreat row missed the casualty tally and that `isOrderRetreat` is the morale at exit; deserters come only from the defeated side; the siege exemption excludes sally-outs; the fearless-undead line still stated the old `BattleEndLogic` mechanism; a test comment still called the `Flee` value a balance constant. | Partial rewrite | The rewrite fixed the cited paragraph, not every sentence resting on the same mechanism. | One-off; the Completeness lens is the gate, and it caught them. |
| S7 | LOW | Wrong date (2026-10-15 for a 2026-10-16 change), no changelog bullet, `.ai/scopes.md` not linking `csharp-patterns.md`, the native trap row naming no tool, redundant `31` rows and no exactly-15 test. | Hygiene | Written fast. | None. |

## Proposed preventive-rule updates (follow-up commit, Mike's call)

The orientation trap index is full (45 of 45 rows, `TRAP_INDEX_MAX_ROWS`), so a new line needs a
merge:

1. Extend the "Persisted MCM defaults" row: rename a setting, with a comment at it saying why.
2. A native-branch trap: a managed decompile hides a branch inside a native engine method
   (`GetNearbyAgentsAux` scans every agent past 15 m); read the native side before claiming no limit.
3. A morale-write trap: a rout costs up to 10 allies morale, from the whole formation when 4 m holds
   fewer, so a local fear effect lowers morale to a floor and never routs.

**Status 2026-10-16 (Mike: apply the best recommendation):** 1 applied. 2 applied, in the row freed
by moving "Culture party templates" out of the index (`.claude/rules/xslt.md` already states both of
its halves and loads on every `.xslt` file). 3 went into `.claude/rules/csharp-patterns.md`
"Anti-Patterns" instead of the index, with the lesson's second branch kept ("or say in the feature
doc that its routs spread"), and `.ai/scopes.md` now links that rule for other clients. The two
unfloored fear writers say so in `race-abilities.md` and `signature-strikes.md`.
