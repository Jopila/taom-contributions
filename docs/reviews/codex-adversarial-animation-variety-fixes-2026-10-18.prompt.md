You are an adversarial reviewer for TAOM, a Lord of the Rings total conversion for Mount and Blade II: Bannerlord v1.5.5 (.NET Framework 4.7.2). This is a FOCUSED second pass: review the FIXES applied in the uncommitted working tree after the first review (your own first-pass report was docs/reviews/raw/codex-adversarial-animation-variety-2026-10-17.md; the RCA listing every finding and fix is docs/reviews/rca-animation-variety-2026-10-17.md). Do NOT edit any file. Read AGENTS.md "Start here" and .ai/review-reference.md first.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
docs/reviews/rca-animation-variety-2026-10-17.md (every finding, the fix applied, and what was not applied)
docs/features/animation-variety.md
docs/features/orc-goblin-locomotion.md (Limp Variants, Motion spec revision, Kit Import Checklist, Known Follow-ups)

THE FIXES TO VERIFY (CONFIRM each is correct and complete, or show the defect with code):
X1. tools/blender/restyle_human_clip.py: the min_track_half_m clamp now measures from the clip's mean pelvis line (track_centre_x_m, a pure helper clamp_track_half) instead of the moving per-frame pelvis; the achieved metric uses the same reference. Does any planted foot still follow a moving reference? Is the centre the right line of travel for a clip with weight_over_foot and sway?
X2. tools/blender/validate_locomotion_clips.py: the limp slide gate now compares the clip's plateau with the SOURCE plateau and gates lateral and vertical ankle speed; the metrics gate is decided by the merged spec and fails on a missing achieved block; knee bow is re-measured from the posed frames; declared turns resolve the lead side through rs.limp_sides and add the hip-hike roll. Can a sliding foot still pass? Is any gate now judged only on what the restyle claims? New tests: tools/tests/test_validate_locomotion_clips_pure.py.
X3. restyle: step points mapped in place (no sort) with step_points_wrapped; only the default --vanilla-step-points path may be missing; run-block value checks, bool refusal, unknown bone keys; idles take only arm_forward_deg from the limp.
X4. tools/bind_orc_goblin_locomotion.py: duplicate-definition guard for owned ids regardless of the generated block, outside-defined variant ids, second or torn blocks; foreign-set guard after building the new text; --require-variants; any *_anm.tpac makes a variant not skipped; orc female twins removed (the orc Monster has no female_action_set), twin mechanism kept for goblins. Does the guard match the engine's first-definition merge (Module.cs:1472)? Can the region rewrite still lose anything?
X5. tools/gen_troll_anim_clips.ps1: -StepPointsFrom renamed -RestyleReport; Read-StepPoints validates every record; -TravelScale comes from the report's single gait stride (typed value must match within 0.001); -Verify STEP and LOOP checks and failure when nothing was checked. Can a stale or mixed report still pass? Is LoopDisplacement checked against the right vanilla value?
X6. C#: GaitVariantCatalog lists set names once per process silently (MBActionSet.GetNumberOfActionSets + GetActionSetWithIndex(i).GetName()) instead of GetActionSet(id) (whose native miss path logs a line); no per-variant warning; as_orc_female_warrior removed from the base sets; the swing probe is behind EnableSwingAlternativeProbe (default false, instrumentation for co-op; settings counts updated in tests and docs); OnAgentHit returns on blow.IsMissile before any allocation and RecordHit ignores missiles; alt-clip-missing wording; blend-out timing check behind the clip-exists guard; ReferenceIdentity; LogStart factor warning only with the step lever on; MCM hint rewritten for the true step range. Check the C# under Main/Features/AnimationVariety/, Main/Adapters/{AnimVariety,GaitVariety}*.cs, Main/Features/TaomSettings.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs and the tests under TAOM.Tests/Features/AnimationVariety plus SettingsFingerprintTests and HotPathSettingsProvidersTests. Is GetActionSetWithIndex(i).GetName() really silent (decompile the native side if you can: IMBActionSet name lookup with index)? Is the probe switch read in a way that cannot arm the sweep by data alone?
X7. Docs and the RCA: do they state the engine facts correctly (walk phase held at the forward clip's snapping start at full idle, 0x7496D0; low-speed rate lift, 0x74BF60; GetIndexWithID miss logs, 0x6DF7D0 -> 0x590060; GetActionCodeWithName silent, 0x6EAF80)?

REQUIRED SECTIONS:
1. For X1 to X7: CONFIRMED-FIXED or DEFECT, with file:line and proving code.
2. NEW DEFECTS introduced by the fixes (any severity), with file:line and the failing scenario.
3. VANILLA CODE for any engine claim you check, as code blocks from the installed DLLs or C:\Users\mikew\.taom-src\v1.5.5\.

QUALITY GATES:
Every finding cites file:line. Mark anything you could not verify UNVERIFIED. Do not re-report first-pass findings that the RCA lists as fixed unless the fix is wrong. Do not report vanilla-matching behaviour as a bug.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. Do NOT write it to any file yourself; the dispatcher redirects your output into docs/reviews/raw/codex-adversarial-animation-variety-fixes-2026-10-18.md.
