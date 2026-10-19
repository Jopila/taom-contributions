# RCA: Bard Town Guard (#824, deep review 2026-10-19)

## Top-line

Root cause, confirmed from the installed v1.5.5 code and the live map data without an in-game repro
(Mike accepted the static proof): `TavernEmployeesCampaignBehavior.conversation_talk_bard_on_condition`
picks a random town of the current town's culture other than the current one (`:420`) and reads its
`.Name` (`:421`). `GetRandomElementWithPredicate` returns null when nothing matches, nothing in the
conversation code catches the exception, and six TAOM cultures own exactly one town (Dol Guldur,
Isengard, Lothlórien, Rivendell, Goblin Town, Lindon), each with a musician who spawns in a vanilla
tavern scene. Culture conversion can create more such towns at runtime. The fix swaps that one call for
a TAOM method that runs the engine's pick first and, only on null, the same engine pick over every
other town.

The deep review (six lenses, then one convergence pass) found no runtime defect in the fix. It found
3 MEDIUM and 9 LOW findings plus one process finding, all handled in the same session (F11 and F12 as
follow-ups). The convergence pass then found that the first apply test patched the class directly, not
through its category as the runtime does, and that the PickTown test did not pin vanilla's pick as the
first one; both tests were corrected.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | `Patch107` was already taken on trunk (`Patch107_PersonalRelationNotice`); 106, 108 and 109 too | Parallel numbering | The number was chosen from trunk and pushed branches at plan time; other batches merged first. No gate compares a patch number across code, registry and feature map | Renumbered to `Patch110`, re-checked at rebase; the number moved out of the class name so a renumber touches the category string and docs only. Follow-up proposed: a gate that one number names one feature |
| F2 | MEDIUM | No test failed if the hook's fallback (the line that fixes #824) was removed | Untested fix line | The binding tests proved the call reaches the hook, not what the hook does | IL test: `PickTown` holds two engine picks and one current-settlement read |
| F3 | MEDIUM | Issue #824 described the abandoned prefix design | Docs drift | Filed before the plan; third time in this batch (#821, #823) | Body rewritten; #825 and #826 get the same check before their build |
| F4 | LOW | `BardTownFallbackService` re-implemented the engine's random pick over a flags array, with 8 tests of the copy | Over-structure | The brief asked for a pure service by habit | Deleted; the hook calls the engine pick a second time with a wider predicate (same draw, same order) |
| F5 | LOW | The transpiler built a new instruction and copied labels by hand; the swapped call is a branch target (`IL_0022 brtrue.s IL_003b`) and no test pinned its label | IL convention | The brief did not say the site is a branch target | In-place swap (the house shape) and a label-survival assertion |
| F6 | LOW | `IoC.Resolve` inside the transpiler blocked a real apply test, and no test applied the patch through its category | Test gap | Siblings' apply test not copied | Logger handed over from the module's `InitializeStatics`; an apply test by category, which also pins the category attribute |
| F7 | LOW | The patch class was named after its number | Naming | Number-first habit | Renamed after target and feature |
| F8 | LOW | Docs: method range `:415-438` (it is `:415-435`), "runs when the musician is addressed" (it runs at every conversation start that reaches the line), an in-game check that expected every lyric to name a town (2 of 8 do) | Docs | Written from the plan, not the engine | Corrected; a real in-game check section with a Gondor control town and the absent-warning sign |
| F9 | LOW | The registry section lacked the PatchShield and co-op paragraphs its neighbours carry | Docs | Template not followed | Added |
| F10 | LOW | Feature doc lacked Performance, Known gaps and an INDEX entry | Docs | Template not followed | Added |
| F11 | LOW | The tavern maid's local speciality line sets `DRINK` and `FOOD` only for vanilla culture ids | Same family, other line | Outside this change | Known gap in the doc; follow-up issue on Mike's word |
| F12 | LOW | Trunk's feature-map row for Hidden Hosts says Patch108; its code says Patch105 | Renumber slip elsewhere | Another batch's rebase | Follow-up on Mike's word |
| F13 | Process | The builder's first RED run was a compile failure | TDD | Compile failure taken for RED | Mutation check (transpiler made a no-op fails the binding test); the PickTown IL test was written RED first |

## Root-cause pattern: vanilla assumes every culture owns more than one town

This is the same family as the landless-culture CTD (`rca-landless-culture-spawn-2026-08-04.md`):
vanilla code written for six big cultures picks "another settlement of my culture" and does not handle
the empty answer. TAOM's map gives several cultures one town, and culture conversion moves towns between
cultures at runtime, so the empty answer is reachable. A sweep of the v1.5.5 dump for culture-filtered
`GetRandomElementWithPredicate` calls found seven; only this one was live. The other six: two are
null-safe through a struct default (`EncounterGameMenuBehavior.cs:2842`, `ParleyCampaignBehavior.cs:99`),
one through `?.` (`CompanionsCampaignBehavior.cs:384`), one needs ports TAOM does not have
(`CaravansCampaignBehavior.cs:498`), one sits in a suppressed quest (`ExtortionByDesertersIssueBehavior.cs:978`),
and one is #933 (`DefaultVassalRewardsModel.cs:39`). The sweep matched the call and `Culture` on one line
only, so a pick stored first and filtered later is not covered.

## Why each lens caught or missed these

- **Standards (1):** F2, F6, F7, F9 and the in-place swap.
- **Engine compatibility (2):** probed the installed IL (one call, branch target, exact matcher, patch
  before the delegate exists) and found F1 and F8.
- **Efficiency (3):** no runtime issue; the branch-target fact behind F5.
- **Data flow (5):** closed the last link of the root cause (the musician spawn points) and found F1.
- **Completeness (4), design (6):** F3, F4, F10, F11, F12 and the class sweep.

## Feedback memories to codify

None. The pattern is a lesson in `lessons/data-content-cultures.md`.
