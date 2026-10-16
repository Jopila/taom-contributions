# Adopting yotthani's delta of 2026-10-16: campaign measurements, siege and wage rules, map-scene and texture findings

Written for: TAOM maintainers deciding what to take from yotthani's two repositories, and for yotthani (the
"For yotthani" section is meant to be forwarded). Procedure: `/adopt-external`
([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)), eighth review of these sources (the seventh of MithrilForge, the fifth of `yotthani/bannerlord`), after
[adopt-yotthani-2026-10-15.md](adopt-yotthani-2026-10-15.md). The request: review what yotthani changed since the
last review. TAOM is on Bannerlord v1.5.5 and v2.0.34.

**Summary.** No upstream code was taken. What came out of it is knowledge (restated in TAOM's docs, each fact tagged with
its source, evidence kind and engine version) and one TAOM defect found through yotthani's measurements: garrisons
desert under the weight-deflated party size limit. The defect is confirmed by code reading and fixed beside this
record: the weight penalty and the capacity displays now exempt garrisons and patrol parties. One claim TAOM had restated, about choke points, was removed
upstream and is corrected.

## Sources, and what was read

| Source | Commit range | Read |
|---|---|---|
| `yotthani/bannerlord` (private, shared with Mike by its author; MIT by the maintainer's statement of 2026-10-09) | `784fef93` (2026-10-15 18:43 UTC) to `2ff002be` (2026-10-16 08:16 UTC), 295 commits | the campaign probe and balance-sim documents (`docs/balance-sim/campaign.md`, `docs/superpowers/specs/2026-10-14-sim-campaign-design.md`, `HoN/CampaignProbe/README.md`), the RealmUI feasibility and engine notes (`docs/realm-ui/feasibility.md`, `docs/realm-ui/research/engine-2d-gauntlet.md`, `RealmUI/Game/Theme/PictureReader.cs`, `RealmUI/Game/Flow/FlowController.cs`, `RealmUI/Game/Map/MapCam.cs`, `RealmUI/Probe/run_probe.ps1`) and `HoN/PerfProbe/Integration/BehaviourRecorder.cs`. Out of scope: the RealmUI feature, the balance simulator and `HoN/Armory.Sim`, the crowd lab |
| `yotthani/MithrilForge` (private, MIT) | `0a2f4761` (2026-10-15 07:27 UTC) to `ae7834aa`, 2 commits, one document | `docs/engine/battle-behaviour-measured.md` |

Three readers went through these files; one Opus verifier then checked seven load-bearing claims against the v1.5.5
source and TAOM's code at `0bb1f3cae`, and the knowledge items below were re-read against the v1.5.5 source files
named in each doc.

## Security

- **Safe to learn from:** yes. The repositories were read through a blobless clone, read-only. Nothing was built,
  installed or run, and nothing from the clone was executed. Upstream text was treated as data.
- **Licences:** MithrilForge is MIT. `yotthani/bannerlord` is MIT by the maintainer's statement of 2026-10-09 and has
  no root `LICENSE` file yet (asked for again below).
- **Comparison only:** no code, data or asset was taken, so the provenance rows stay `comparison-only`; the register's
  new pass paragraphs say so.

## What TAOM verified (2026-10-16)

| Claim | Evidence |
|---|---|
| **Garrisons desert under the weight-deflated size limit** | `TaomPartySizeModel.GetPartyMemberSizeLimit` (`Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs:95`) applies `TroopWeightService.ApplyPartySizeWeightPenalty` to every party. For a garrison, vanilla's base has already returned the garrison limit (`DefaultPartySizeLimitModel` v1.5.5 `:86-88`), and the penalty has no garrison check (`TroopWeightService.cs:103-133`). Vanilla desertion removes a quarter of the excess over `PartySizeLimit` a day (`DefaultPartyDesertionModel` `:54-65`); garrison auto-recruit room reads the same reduced limit (`GarrisonRecruitmentCampaignBehavior` `:185`); lords' transfers read the unweighted `CalculateGarrisonPartySizeLimit` (`GarrisonTroopsCampaignBehavior` `:492-494`), so they keep leaving men who then desert. TAOM's own doc treats garrisons as outside the size budget (`docs/features/troop-weight-system.md:150-152`, the garrison limit "never enforce[d] as a hard cap", and the deferred tooltip row at `:180`), but vanilla desertion ticks garrisons too (`DesertionCampaignBehavior.cs:21`); the shed skips them. Code reading, never run |
| The size of the effect is yotthani's measurement, not TAOM's | about 20.6 k deserters in the free block in 150 days, 19.6 k of them in four elven realms, the cause yotthani's reading (`docs/superpowers/specs/2026-10-14-sim-campaign-design.md`@2ff002be lines 379 and 387, run 5, v1.5.5 with TAOM 2.0.34; Mirkwood town 323 men against a limit of 123 on day 110). TAOM has not reproduced the number |
| Choke-point claim removed upstream | MithrilForge `battle-behaviour-measured.md`@ae7834aa lines 133-152 against @0a2f4761 lines 133-135: the second test (best-rated choke point) won 1 of 11, both tests 5 of 22, and the sentence that the AI's own weight guides better than the type is gone. `formations-and-team-ai.md` is corrected |
| `TacticHoldChokePoint` eligibility and score | v1.5.5 `TacticHoldChokePoint.cs:139-174`, restated in `formations-and-team-ai.md`. Yotthani's `ChokeScore` (`BehaviourRecorder.cs`@2ff002be about 352-370) leaves out the distance factor on purpose (lines 354-355), and with it the distance gate, and uses its own enemy centroid |
| Siege map events and the order of finalization | v1.5.5 `SiegeAssaultEventComponent.cs:263-269`, `SiegeOutsideEventComponent.cs:73-80`, `SallyOutsCampaignBehavior.cs:44, 91, 96-108`, `KingdomManager.cs:258-260`, `BesiegerCamp` `:242-245`, `SiegeEvent.cs:889`. Code reading, `KingdomManager` path only. Restated in `fief-granting.md`; TAOM's `FiefSiegeCaptureRules.cs:22-28` fits |
| Clan wage payment and garrison recruitment | v1.5.5 `DefaultClanFinanceModel.cs`, `ClanVariablesCampaignBehavior.cs`, `DefaultSettlementGarrisonModel.cs`, `GarrisonRecruitmentCampaignBehavior.cs`, `MobileParty.cs:1660-1663`; restated in `settlement-economy-food-prosperity.md`. TAOM overrides `GetTotalWage` and raises `MaxWagePaymentLimit` to 20000 (`TaomPartyWageModel.cs:37`, vanilla 10000), which feeds the wage room and wage desertion, and raises tariff income for four cultures (`TaomClanFinanceModel.cs:84-90`); the payment formulas are vanilla's |
| `Texture.CreateFromByteArray` pins the pixels only for the call | v1.5.5 managed: `ManagedCallbacks.ScriptingInterfaceOfITexture.cs:260-266`, `Texture.cs:125-128`. The late native upload is yotthani's reading; the native side is unverified. TAOM has no such call |
| `MapViewRelease` clears the map's own view, not a second one | `MapViewReleaseAdapter.cs:26-30` calls `SceneLayer.ClearRuntimeGPUMemory(false)`, which is `ClearAll(false, false)` (`SceneLayer.cs:145-148`): the arguments of the call that hung in yotthani's run, on a different view. Vanilla's `MapScreen.ClearGPUMemory` clears the same view with `true` (`MapScreen.cs:504-508`). Yotthani's rule (`feasibility.md`@2ff002be lines 181-183) is about a second view: yotthani's own VanillaTuning `map-view-release` makes this exact call on the map's own view (`MapViewReleasePatch.cs`@2ff002be line 70), and its notes record an in-game run of 14 screens with the call at every cover on 2026-10-06 (lines 20-23; that it did not hang is TAOM's reading, the notes do not say; on by default since `a0befc68`, at every 20th cover). TAOM's map views add Gauntlet layers only (`FieldCampMapView.cs:47`, `RealmBordersMapView.cs:52`, `MomentumIndicatorMapView.cs:40`) |

## Decisions

| Item | Decision | Where |
|---|---|---|
| I3 | Built and reviewed beside this record as two items: `frameLimiter=` and `vsync=` in `[PerfContext]`, and `mainMs=` and `renderMs=` in `[MissionPerf]` (both engine getters are busy-time rates, decompiled on the v1.5.5 client) | `docs/features/mission-perf-heartbeat.md`; issue draft 2 below |
| The upstream-review process | Built and reviewed beside this record: `tools/upstream_watch.py`, `docs/reviews/upstream-watch.json`, the session-start `UPSTREAM AHEAD` banner and the `/upstream-review` skill | issue draft 3 below |
| Garrison defect | Fixed beside this record: one predicate, `TroopWeightPenaltyScope.PaysSizePenalty`, exempts garrisons and patrol parties (the review found patrols sat over their deflated limit under vanilla's speed penalty); the penalty returns before it caches or subtracts, and the capacity displays keep vanilla's raw frame for them (test first, `DisplayFrameSourceTests`); `troop-weight-system.md` no longer says vanilla leaves the garrison limit unenforced | `docs/features/troop-weight-system.md` |
| Knowledge items | Taken, as restated and credited facts in the owning docs | the docs this batch touches |

**Owed, on the maintainer's word:** sending "For yotthani". The issues below were not filed, by the maintainer's
decision of 2026-10-16; they stay here as the record of each change.

## Not taken

| Item | Reason |
|---|---|
| RealmUI (the feature) | yotthani's product; only its engine findings are restated |
| CampaignProbe as a tool | it lists yotthani's paths and module list; the method (an unattended 150-day campaign written to CSV) is noted, nothing is adopted |
| The balance simulator and `HoN/Armory.Sim` | yotthani's product; TAOM takes its measured facts only |
| The crowd lab | a separate research product with no published gain; unchanged from 2026-10-15 |

## Findings for Mike (no code here)

1. **The garrison defect and its in-game check.** The mechanism is confirmed by code reading; the size is not. After
   the fix, watch one elf town (Mirkwood, on the order of 300 men against a limit near 120) over a month and see
   whether the garrison still sheds men each day. Until then, the number 20.6 k is yotthani's alone.
2. **Start wars end within days 6 to 8.** Yotthani's two measured campaigns had 10 peaces of start wars on
   days 6 to 8, fitted as a daily peace chance of 0.667 from day 6 (`docs/balance-sim/campaign.md`@2ff002be line 188, v1.5.5
   with TAOM 2.0.34). Track it; do not act on two runs.
3. **The `MapViewRelease` in-game check also watches for a hang or a missing terrain on returning to the map.**
   Yotthani's VanillaTuning runs the same call on the same view without a hang, but TAOM releases on the map's return
   instead of on the cover, and that run predates v1.5.5.

## Issue drafts

Not filed.

1. **fix: garrisons desert because the party size limit is weight-deflated.** `TaomPartySizeModel.GetPartyMemberSizeLimit`
   (`Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs:95`) applies `TroopWeightService.ApplyPartySizeWeightPenalty`
   to every party, including garrisons, whose vanilla limit is already the garrison limit (`DefaultPartySizeLimitModel`
   v1.5.5 `:86-88`); the penalty has no garrison check (`TroopWeightService.cs:103-133`). Vanilla desertion removes a
   quarter of the excess over `PartySizeLimit` a day (`DefaultPartyDesertionModel` `:54-65`), garrison auto-recruit
   room reads the same reduced limit (`GarrisonRecruitmentCampaignBehavior` `:185`), and lords' transfers read the
   unweighted `CalculateGarrisonPartySizeLimit` (`GarrisonTroopsCampaignBehavior` `:492-494`), so they keep leaving men
   who then desert. TAOM's own doc treats garrisons as outside the size budget (`docs/features/troop-weight-system.md:150-152`, the garrison limit
   "never enforce[d] as a hard cap", and the deferred tooltip row at `:180`), but vanilla desertion ticks garrisons
   too (`DesertionCampaignBehavior.cs:21`); the shed skips them. Evidence:
   code reading; yotthani counted about 20.6 k deserters in the free block in 150 days, 19.6 k of them in four elven
   realms, the cause yotthani's reading (`docs/superpowers/specs/2026-10-14-sim-campaign-design.md`@2ff002be lines 379
   and 387, run 5, v1.5.5 with TAOM 2.0.34; Mirkwood town 323 men against a limit of 123 on day 110), which TAOM has not
   reproduced. Fix: exempt garrisons and patrols from the penalty and from the capacity displays
   (`TroopWeightPenaltyScope`), test first. Labels: bug.
2. **perf: log the frame limiter, vsync and the engine's main and render busy times.** I3 of the 2026-10-15 plan. The
   engine's `GetMainFps` and `GetRendererFps` are busy-time rates on the v1.5.5 client (main thread excluding the
   limiter spin; render thread including Present), so `[MissionPerf]` gains `mainMs=` and `renderMs=` and `[PerfContext]`
   gains `frameLimiter=` and `vsync=` (read through `NativeOptions.GetConfig`; `GetFrameLimiterWithSleep` is a stub in the
   client). `tools/perf_runs.py` reads all four, still reads older logs, and reports busy-time medians. Labels:
   enhancement, perf, `triage-needs-ingame`: (a) limiter 60 with vsync None shows `frameLimiter=60 vsync=0` and fps at 60;
   (b) a 30 fps limiter gives `mainMs` and `renderMs` well under 33 ms; (c) a GPU-heavy setup gives `renderMs` close to
   `avgMs`.
3. **feat(harness): a repeatable upstream review with a session-start banner.** `docs/reviews/upstream-watch.json` holds
   each watched repo's last reviewed SHA; `tools/upstream_watch.py` reports how far each has moved (cached for 20 hours,
   within a session-start time budget), writes per-area briefs from a blobless clone, and advances the state; the
   session-start hook prints `UPSTREAM AHEAD` with the commit count; `/upstream-review` runs the review with every gate
   kept. Labels: enhancement.

## The doc review

Three finders read the knowledge batch for fidelity to the source, consistency with TAOM and routing; an Opus
verifier then re-read every claim against the pinned upstream files and the v1.5.5 source. Of 22 findings, the
verifier refuted one in full (#11: `TaomPartyWageModel` does change `TotalWage`, through `GetTotalWage`) and two in
part (#1's claim that the doc misquotes the source, and #17's "n=22 unexplained"). It confirmed the rest, folded three
into others (#13 into the `MapViewRelease` rewrite, #15 into the party-size paragraph, #20 into the shader-precompilation
wording) and added one of its own (the clan-leader gold share loop, `settlement-economy-food-prosperity.md`). All
confirmed fixes are applied. The two that changed a conclusion:

- **`MapViewRelease`:** TAOM's call is `ClearAll(false, false)`, the arguments of the call that hung in yotthani's run.
  A later read settled it as far as a read can: yotthani's own VanillaTuning makes the same call on the map's own view
  (on by default at every 20th cover), and its notes record an in-game run of 14 screens with the call at every cover;
  that it did not hang is TAOM's reading, since the notes do not say. The in-game check still watches for a hang on
  returning to the map.
- **The garrison figure:** about 20.6 k is the whole free block's deserters, 19.6 k of them in the four elven realms,
  and the cause is yotthani's reading of the run. TAOM's own `troop-weight-system.md:150-152` said, at `0bb1f3cae`,
  that vanilla never enforces the garrison limit, which desertion contradicts; the garrison fix corrects it.

## For yotthani

Thank you for the campaign probe and the careful notes in `campaign.md`, `feasibility.md` and the battle-behaviour
document, and for removing the choke-point sentence in the open; the second test is the more useful result. TAOM
has restated the facts it could check, credited to you in each doc. What we would add, in case it helps:

1. **`TaomClanFinanceModel` also raises tariff income** for the Umbar, Erebor, Dale and Khand cultural feats
   (`TaomClanFinanceModel.cs:84-90`), so it is not display-only if a note says so.
2. **`ChokeScore` also leaves out the distance eligibility gate** (`num > 20f && num > num2 *
   0.5f`, v1.5.5 `TacticHoldChokePoint.cs:139-144`), beside the distance factor your comment names, and uses its own enemy
   centroid, so the comment could name the gate as well.
3. **`MapViewRelease` calls `ClearAll(false, false)` on the map's own view** (through `SceneLayer.ClearRuntimeGPUMemory(false)`),
   as your VanillaTuning `map-view-release` does (`MapViewReleasePatch.cs` line 70), so TAOM reads the rule in
   `feasibility.md` (lines 181-183) as being about a second view on the map scene. The wording "never on a view that holds the
   map scene" also covers the map's own view; if you meant only the second view, saying so there would help.
4. **The asks of 2026-10-15 stand:** `TaomOverlap` rows for `shader-compile-notice`, `xml-merge-fast` and
   `skip-logo-video`, and a root `LICENSE` file. `VanillaTuning` had no commit in this range.

Thank you again.

---
