# Adopting yotthani's newer performance and engine work: VanillaTuning v0.1.1, MithrilForge, the crowd lab (2026-10-15)

Written for: TAOM maintainers deciding what to take from yotthani's two repositories, and for yotthani (the
"For yotthani" section is meant to be forwarded). Procedure: `/adopt-external`
([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)), seventh review of these sources, after
[adopt-yotthani-2026-10-08.md](adopt-yotthani-2026-10-08.md). Mike's request: review everything yotthani changed since
the last review, with the focus on performance and engine optimization; DualWield and yotthani's other feature mods stay
with yotthani. TAOM is on Bannerlord v1.5.5 and v2.0.34.

**Summary.** No upstream code was taken. What came out of it is knowledge (restated in TAOM's docs, each fact tagged with
its source, evidence kind and engine version), four small TAOM code items (#806 to #809), and a list of asks for
yotthani. The two largest findings: a clip with a rope flag and no `{clip}_weapon` companion is an access violation at
wind-up (TAOM ships no such clip today), and VanillaTuning v0.1.1 now stands aside for TAOM by looking up four TAOM
class names, which makes those names a contract.

## Sources, and what was read

| Source | Commit range | Read |
|---|---|---|
| `yotthani/bannerlord` (private, shared with Mike by its author; MIT by the maintainer's statement of 2026-10-09) | `1ad701bd` (2026-10-08) to `784fef93` (2026-10-15 18:43), 453 commits | the performance and engine surface: `HoN/VanillaTuning` (v0.1.0, v0.1.1, `Core/TaomOverlap.cs`, `Integration/VanillaTuningSubModule.cs`, `NativeBytes.cs`, `PatchHealth.cs`, guard and watch changes, `LimbRayShadow.cs`), `HoN/PerfProbe` (crowd proxy `CrowdProxy.cs`, crowd lab `CrowdLab.cs`, behaviour recorder, native crash log, gauge and autopilot changes), the `HoN/DualWield` limb-ray route (`Core/OneAgentLimbRay.cs`). Out of scope: the balance simulator (`HoN/Armory.Sim`, `docs/balance-sim`, `docs/superpowers`), Armory recipes, the rest of DualWield |
| `yotthani/MithrilForge` (private, MIT) | `4fab7e19` (2026-10-08) to `0a2f4761` (2026-10-15 07:27), 9 commits | `docs/engine/perf.md` (2,117 lines) and `docs/engine/modding-kit.md` (1,790 lines), both now read in full, which closes Tier 2 item 8 of the 2026-10-08 review; `docs/engine/battle-behaviour-measured.md` (v1.5.5); `docs/engine/weapon-companion-clip.md`; the clip-tool fix; `tools/perf-audit/run_scenario.ps1 -Cores` |

Five readers went through these files and produced 92 items. One adversarial verifier per reader confirmed 72 of the 78
items it checked and corrected the rest, and one completeness critic read behind them. The load-bearing claims were
then re-checked by the reviewer against the files, the installed engine or TAOM's code (next sections). All upstream
reads went through `gh api` pinned to the commits above. `perf.md` is German: its content is translated into the docs,
never pasted.

## Security

- **Safe to learn from:** yes. Docs and code were read only. Nothing was cloned, built, installed or run.
- **Licences:** MithrilForge is MIT. `yotthani/bannerlord` is MIT by the maintainer's statement of 2026-10-09 and has
  no root `LICENSE` file yet (asked for again below). `HoN/VanillaTuning` carries its own MIT file.
- **Comparison only:** no code, data or asset was taken, so the provenance rows stay `comparison-only`; the register's
  sixth-pass paragraphs say so.

## What TAOM verified (2026-10-15)

| Claim | Evidence |
|---|---|
| TAOM is on `v2.0.34` | `Main/_Module/SubModule.xml:6` |
| VanillaTuning keys on four TAOM full names, five patch ids | `TaomOverlap.cs@784fef93:17-24`: `nameplate-cull`, `map-view-release`, `mission-start-guard`, and `frame-buffer-guard` plus `frame-buffer-watch` (both on `SkeletonBufferModule`). All four classes exist in `Main` |
| VanillaTuning's stand-aside is a type-presence check, and TAOM's MCM toggle is the only switch | `Integration/VanillaTuningSubModule.cs@784fef93:23-38` (`LeaveToTaom`, run in its `OnSubModuleLoad`): it tests `taom.GetType(name, false) != null` and never reads a TAOM setting, so TAOM's toggle off leaves the site unguarded. Code reading |
| Launcher order does not matter for that check | v1.5.5 `Module.cs`: assemblies are loaded and every submodule constructed (`:1091-1145`) before `InitializeSubModuleBases` (`:1162`), which calls `OnSubModuleLoad` (`:226-233`). Managed decompile |
| TAOM logs "already guarded" at INFO, not a wrong-build report | `SkeletonBufferGuardService.cs:39` (`ForeignReason`), logged at `:158-161`. VanillaTuning's `TaomOverlap` comment predicts a "wrong game build" report instead |
| A throw in a module's main-menu hook repeats every tick | v1.5.5 `Module.cs:538-549` (`SetInitialModuleScreenAsRootScreen` runs every tick while `ActiveState == null`) and `:796-799` (calls every submodule's callback). Managed decompile |
| The tick loop counts down and a removal inside it is unsafe | v1.5.5 `Mission.cs:3750-3753` against `RemoveMissionBehavior` at `:4707-4724`. `DeploymentMissionController.FinishDeployment` ends with `RemoveMissionBehavior(this)` (`:41-80`). TAOM's parked `ShaderPrecompilePlayerAgentGuard` calls it from `OnMissionTick` (`:80-94`). Managed decompile and code reading, never run |
| `MeleeHitCallback` cancels more than the brief said | v1.5.5 `Mission.cs:5340-5377`: weapon parry, block or chamber-block without a shield; a non-enemy victim (with the friendly-fire stun); an invulnerable victim; a swing with no victim and no entity. Shield blocks are not cancelled |
| Morale constants | `CustomBattleMoraleModel.cs:20-36` and `SandboxBattleMoraleModel.cs:227-244` give k 0.75, 0.5, 0.25, a factor of 1.25 with the flag (0.3125), gain `battleImportance * 3 * k`, loss `battleImportance * 4 * k * casualtiesFactor`. Managed decompile |
| Limb-ray entries on the v1.5.5 client | Ghidra decompile of the 14,212,960-byte client (project key `Win64_Shipping_Client-4c2054c56dfee496`): entries `0x6F3520` and `0x6F3360`; the given-agents wrapper calls `0x5D0620` and `0x5C94A0`, never `0x681A00`. In `0x701450` the rope test is `(uint)rec[+0x18] & 0xC000` and the lookup result goes unchecked into `FUN_180472be0` |
| The skeleton-buffer guard works on v1.5.5 | TAOM's log of 2026-10-15 (`taom_debug_2026-10-15_12-45-22.log` lines 40 and 41): `guard ON pool 1: site=0x6A1D4 resume=0x6A1E1` (scan 174.3 ms) and `guard ON pool 2: site=0x6B410 resume=0x6B41D`; line 38232: `[AnimMem] armed: ... loadSite=0x21E50F budgetSite=0x21E534` |
| The FBX import dialog keeps its last choices | `HKCU\Software\TaleWorlds\RGL\fbx_import_settings` exists on the maintainer's desktop and holds `import_physics_shapes`, `convert_to_unit` and others (read-only `reg query`, 2026-10-15) |
| TAOM's four camp tpacs are an older writer's output | their git blob hashes (`1e26222f`, `078dc719`, `4d9670eb`, `4453ec79`) equal those at `yotthani/bannerlord@2e14c22dfc`; `478587b482` differs on all four |
| Nothing TAOM or the live Armory ships carries a rope flag | a byte scan of 5,506 Armory tpacs and TAOM's modules, 2026-10-15 |
| The weapons with their own skeleton in vanilla 1.5.5 | `flail_skeleton` x5, `sling_skeleton` x6 and `shackle_skeleton` x2 across Native, SandBoxCore and MP item XML (upstream names only the flail) |
| Upstream commits cited exist | `5dcf9b24`, `44d89f49`, `44afb243` ("measured: no gain"), `fc45bc48`, `8b852b25`, `35570dab` |
| **I1: the native melee AI reads the block, parry and shield-defend properties** | decompile of the installed v1.5.5 client, 2026-10-15. `IMBAgent.UpdateDrivenProperties` (`0x6E1740`, thunk to `FUN_1805da1c0`) copies all 98 driven properties to `agent + 0x2E8 + 4 * index`. Consumed by the melee decision machine (reached from `IMBMission.Tick`, dispatcher `FUN_18069dee0`): block (index 5), parry on decide (6), parry on attack (11), parry on attacking continue (17), defend with shield (28); controls `AIAttackOnDecideChance` (36) and `AiShooterError` (41) pass. **No native read found** for `AiAttackingShieldDefenseChance` (20); the scan covered 93.4 % of `.text`, so that is "no read found", not "proven dead". The shield value is read through `min(0.95, value)` or `min(0.95, 0.5 * value)`: lowering it bites, raising one already at the cap changes nothing. [Likely] the block ability applies to a soldier holding an intact shield, the parry ability to one without. TAOM's race-ability block and parry percentages and Culture Doctrine's shield value are therefore live levers |
| **I2: the three hitches of the 2026-10-15 session are not attributed** | Hitch 1 (13:20:05, 617 ms in `missionTickMs` at battle start, `spawnMs` 232) is inside `Mission.OnTick`; the `cameraMs` field of #809 will show the camera share. Hitches 2 and 3 (0.96 s and 0.89 s) are `otherMs`, outside `Mission.OnTick`, with the engine log silent and about 6 to 7 MB allocated in the frame. DualWield was loaded in that session, VanillaTuning was not. Cause unknown |

Corrections made while checking the brief, so the docs carry the true version: the ash emitter census label is
`falling_leaves_b::leaves` and the source is TAOM_Map's `lotr_falling_ashes`; the proof run measured 21.0 to 11.6 ms and
the 12.2 ms quoted on 2026-10-08 is the later `VanillaTuning_TAOM` run (`perf.md` lines 707 and 772 to 776). The drop in
clip residency from 11.94 s to 3.67 s is measured, but pinning it on the shared cache line is yotthani's reading. The rope
bits (layer word `0x4000` and `0x8000`, full clip-flag bits 50 and 51) are not the `anf_keep` and `anf_restart` values of
the clip-flags doc, which sit in another word. That the record word at `+0x18` is the layer word is the review's
identification plus yotthani's Kit flag table, not a proof. The crowd-lab commit `44afb243` is dated 2026-10-09, the day
TAOM took v1.5.5, and the crowd lab's mod list is not stated, so its figures carry "1.5.4 or 1.5.5, mod list unknown".

## Decisions

The maintainer approved the plan on 2026-10-15: four issues, four code items, three offline investigations, this
knowledge-base batch, an RCA and local commits. Push, merge, paid calls and public posts beyond the four issues
stay on the maintainer's word.

| Item | Decision | Where |
|---|---|---|
| C1 | Taken: the v1.5.5 skeleton-buffer pin row, and every native pin table fails the binding gate on an unlisted build | #806 |
| C2 | Taken: the main-menu customization runs through `MainMenuStep.Run`, so a throw cannot loop every tick | #807 |
| C3 | Taken: a test pins the four TAOM type names VanillaTuning v0.1.1 stands aside for | #808 |
| C4 | Taken: a `cameraMs` column splits the camera and the engine's PreDisplay out of the hitch probe's frame columns | #809 |
| I1, I2 | Done offline: I1's verdict is in the race-ability and doctrine docs, I2's in "Findings for Mike" | this record |
| I3 | Not done yet: what the engine's `GetMainFps` and `GetRendererFps` measure, and `frameLimiter=` and `vsync=` in `[PerfContext]`; owed as its own item | the plan |
| Knowledge base | Taken, as restated and credited facts in the owning docs | the docs this batch touches |

**Review of the code items.** A seven-lens `/deep-review`, its fix round and a convergence pass, then at the
maintainer's request a fresh seven-lens review and a Codex review (`gpt-6-astra`, reasoning `max`), then a
convergence pass and a focused Codex pass (`high`) on those fixes. No CRITICAL or HIGH finding in any pass. Every
finding, its fix or its rejection is in `docs/reviews/rca-yotthani-review-2026-10-15.md`.

**Owed, on the maintainer's word:** push and merge; sending "For yotthani"; an optional comment on #738; the
NativeCrashLog issue and its design; the Patch37 decision issue (`triage-blocked-decision`); updating the bodies of
#807 and #809 to what shipped; the in-game session in "Findings for Mike", item 8.

## Not taken

| Item | Reason |
|---|---|
| The crowd proxy (far soldiers drawn as cheap figures) and the crowd lab | a separate product of yotthani's research in progress, with no published gain and a known freeze; tracked only. The facts about agent-less figures are in the engine doc |
| The AI sleep mode (pause AI for soldiers with no enemy nearby) | measured by yotthani as no gain (`CrowdProxy.cs@784fef93:1163-1206`, commit `44afb243`) |
| PerfProbe additions, including NativeCrashLog | a native fault written to the log as it happens is a real gap in TAOM, but it means a vectored handler running managed code, which is risky; its own issue and an architect design later |
| The background-collection variant for the post-save GC call | yotthani's one run per variant moved a 311 ms freeze to 120 to 160 ms pauses about a minute later and recommends leaving it OFF, since the gain is small and the player gets less predictable pauses (`perf.md` lines 1397 to 1420, 2026-10-04, v1.5.3; the cause is plausible, not proven) |
| The war-score cache | rejected by yotthani's measurement: `GetScoreOfDeclaringWar` takes 12 us per call (`perf.md` lines 1141 to 1142) |
| Focus-ray throttle, BehaviorTree sensor cost, CultureConversion daily tick, the Black Gate physics-contact messages | measure first, in one TAOM-only in-game session after #809 is deployed |
| The Black Gate ash content fix (#738) | parked for Kysaro on 2026-10-06; it is still the largest measured frame cost on TAOM content (21.0 ms down to about 11.6 ms with the fix, v1.5.3, in yotthani's profile and in-game run). `VanillaTuning_TAOM` swaps it at runtime for players who run that add-on |
| DualWield, Armory, Armory.Sim, the balance simulator, FaceLearner | yotthani's features and products; engine facts only. TAOM's installed DualWield v0.9.14 predates three crash fixes (v0.10.1 and v0.10.6); updating it is the maintainer's choice |
| Tier 2 items 9, 10 and 11 of 2026-10-08 | no upstream change; they stay as decided |

## Findings for Mike (no code here)

1. **Camp tpac delivery decision.** TAOM's four camp tpacs are byte-identical (git blobs) to `yotthani/bannerlord@2e14c22dfc`;
   `478587b482` is the rewritten version and differs on all four. It is the version to adopt when the delivery decision
   owed since 2026-09-29 lands. yotthani's FieldCamp and Refuge modules, which ship the props through `AssetPackages` only,
   loaded the four props in game on 1.5.3.
2. **I1's result needs an in-game owner.** The levers exist, but two zeroed mirror battles of yotthani's showed no change
   (n=2, six properties together). Two of the seven places that set the defend state are not gated by these values, and
   the run may not have applied them. The reason stays UNEXPLAINED; do not write that the levers are inert.
3. **Armour flicker above 2,340 skeletons.** With only the pool-1 guard, yotthani saw armour flicker and float in game on
   2026-10-12 with 2,000 soldiers (`CrowdProxy.cs@784fef93:418-421`, commit `44d89f49`). TAOM guards both pools and has not
   looked: the check is owed.
4. **A code comment is wrong.** `Main/Features/SignatureStrikes/Domain/StrikeContext.cs:15-17` omits friendly fire from
   the cases that cancel `OnMeleeHit`. The doc is fixed; the comment goes to a code batch.
5. **Patch37 hides a retry loop.** Crash containment swallows a throw from any module's main-menu hook, which turns it into
   a per-tick retry instead of a crash. C2 contains TAOM's own steps; another module's throw still loops. A decision on
   whether Patch37 should name the module or stop swallowing after N repeats is a candidate issue
   (`triage-blocked-decision`), not filed.
6. **Shader precompilation: the parked guard.** `ShaderPrecompilePlayerAgentGuard` removes a behaviour inside the tick
   loop. It is latent while the feature is parked; fix it before un-parking (the checklist line is in
   `shader-precompilation.md`).
7. **The shader-compile notice can vanish.** See "For yotthani", item 3.
8. **Two unattributed 0.9 s hitches** (I2) and the battle-start hitch: the next in-game session should run with the
   tick profiler on after #809 is deployed, without VanillaTuning, `VanillaTuning_TAOM` and DualWield loaded, so the
   numbers are TAOM-only.

## Issue drafts

Filed from this review: #806 (native pin tables fail the engine-bump gate instead of skipping), #807 (a throw in the
main-menu setup hook cannot loop every tick, C2), #808 (pin the four TAOM type names VanillaTuning stands down for, C3),
#809 (`cameraMs` split out of the hitch probe's frame columns, C4). Not filed:

1. **check: armour flicker above 2,340 skeletons with both guards on.** A 2,000-plus soldier battle, looking for the
   flicker yotthani saw with the pool-1 guard only. Labels: `triage-needs-ingame`.
2. **fix: the parked precompile guard removes a behaviour inside `OnMissionTick`.** Move `FinishDeployment()` out of the
   tick before un-parking (flag, act next frame). Labels: bug (latent).
3. **research: why zeroed block and parry properties showed no change in yotthani's battles.** I1 found native readers
   for five of the six properties and none for `AiAttackingShieldDefenseChance`; the open part is the two ungated
   entries into the defend state and whether the run applied the values. Labels: `triage-blocked-decision`.

## The doc review

Written when the find-and-verify review of this batch completes.

## For yotthani

Thank you for TaomOverlap, and for the careful notes in `perf.md`, `modding-kit.md` and `weapon-companion-clip.md`; TAOM
has restated the facts it could check, credited to you in each doc, and found the rope-flag finding the most useful. What
we would ask, in case it helps:

1. **More TaomOverlap rows.**
   - `shader-compile-notice`, keyed on `TAOM.Features.ShaderCompileNotice.ShaderCompileNotice`.
   - `xml-merge-fast`, keyed on `TAOM.Features.XmlMerge.XmlMergeModule`. TAOM's merge keeps one document the same way,
     adds an XSLT cache and a session fallback, and stands aside on foreign patches, so with both loaded VanillaTuning's
     runs and TAOM's stands aside.
   - `skip-logo-video`, keyed on `TAOM.Features.FactionUI.Hooks.VideoPlaybackStateSetStartingParametersPatch`.
     VanillaTuning's skip sets `_splashScreenPlayed`, so TAOM's #704 splash never plays.
2. **The "wrong game build" comment in `TaomOverlap`.** Its comment predicts that the skeleton buffer's second patcher
   "would find the bytes changed and report a wrong game build". TAOM in fact detects your jump at the site and logs
   `already guarded by another module` at INFO (`SkeletonBufferGuardService.cs:39`), then still guards the second pool.
   Nothing is reported as a wrong build.
3. **Order for the shader notice.** TAOM stands its notice down whenever a module with the id `VanillaTuning` is active.
   If you add a `shader-compile-notice` row, both mods would yield and nobody would show it. When VanillaTuning ships
   that row, TAOM removes its own stand-down in the same release; until then TAOM keeps it, so no player gets two
   windows or none. Please tell us the version that carries the row.
4. **A root `LICENSE` file** in `yotthani/bannerlord`, so the MIT grant is written down where the code lives. Today only
   `HoN/VanillaTuning` and `HoN/ShaderCacheKeeper` carry one. Asked on 2026-10-08 and still owed.
5. **A note on the four names.** TAOM now pins `NameplateCullModule`, `MapViewReleaseModule`, `MissionStartGuardModule`
   and `SkeletonBufferModule` with a test (#808), so a rename on our side cannot go unseen. If you change how VanillaTuning
   decides (the module id, the assembly name), tell us and we will adjust the test.
6. **A reminder, not a request.** Turning one of the four TAOM features off in TAOM's MCM menu leaves that site unguarded,
   because VanillaTuning stays out of it once the class exists. We read this from your code and have not seen it in game.

Thank you again.
