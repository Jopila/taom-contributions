# RCA: Settlement Services (Watch-Silver and Tidings of the Lists), deep review, #859 (2026-10-23)

**Summary.** #859 adds two single-player services. Watch-Silver lets the player pay the governor of a town or castle the
player's clan owns to raise its loyalty by 5, 10 or 20 points, never above 50. Tidings of the Lists sells, for 20
denars in a town's arena, the three nearest towns that hold a tournament and are not at war with the player. The
feature uses one dialogue chain, one arena menu option, one adapter and no Harmony, prefab or save data. A seven-lens
deep review of the uncommitted tree on `feat/settlements-buildings` (base `d0e6725ec`) found no HIGH and no MEDIUM
finding. It confirmed one money defect that only an engine listener's throw can reach, three display and logging
inconsistencies, a duplicated fault-log class, and about twenty LOW test, doc and hygiene items. All 43 engine members
the reviewed tree called exist on v1.5.5 with the signatures used (41 after the fix pass removed two). Every confirmed finding is fixed in the review's fix pass
unless listed under "Not applied".

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | A throw from the gold charge was treated as "nothing taken": the service logged "could not take" and refunded nothing. `GiveGoldAction` debits the giver first and raises `OnHeroOrPartyTradedGold` afterwards (v1.5.5 `GiveGoldAction.cs:13-14`, `:39`), so a throwing listener escapes with the gold already gone. The same 18-line charge block was copied into both services. | Money path | The brief's receipt rule (`lessons/gamemodels-services.md` "A return value that names a request is not a receipt") was applied to the return value only; nobody asked what the purse holds when the call throws. | One all-or-nothing `PlayerPurse.TryCharge` measures the purse before and after on every exit path and refunds what left it; a stateful-fake test throws after the debit. The convergence pass found that the refund raises the same event, so a refund that throws is caught and logged too. The receipt lesson gains both cases. |
| 2 | LOW | Watch-Silver's "bad number" log latch was keyed on the constant `"nonfinite"` on a singleton service, so after the first fief no other fief was ever logged in that process, and the comment said nothing carried over between campaigns. | Logging, lifecycle | A latch on a singleton was written as if it lived per campaign. Repeat offender: `lessons/build-tooling-workflow.md` "A once-only reason line has one latch per reason" and `lessons/state-lifecycle-save.md` "Un-persisted state on a `Reuse.Singleton` service outlives the campaign". | Keyed per fief, comment and summary say "once per fief per game session"; a test proves a second fief still logs. The latch lesson gains this occurrence. |
| 3 | LOW | The feature shipped its own `CallbackFaultLatch`, a near copy of `ScreenFaultLog`, which the same branch had just created to remove duplicate latches. | Duplication | The builder was told not to touch the follow-ups files and wrote what it needed; the brief named no shared fault log because it did not exist when the brief was written. Copied-precedent defect on this branch: the fifth. | The behaviors use `ScreenFaultLog`, which gains a `Condition` member and a log sentence true for campaign behaviors too (Mike, 2026-10-23). |
| 4 | LOW | The governor showed loyalty rounded (`Math.Round`) while vanilla's town overlay truncates (`(int)Town.Loyalty`, `SettlementMenuOverlayVM.cs:1288`), so the two numbers a tester compares can differ by one. | Display | No one compared the TAOM number with the vanilla number shown beside it. | `(int)loyalty`, as vanilla. |
| 5 | LOW | A Tidings click showed nothing when the charge failed or a gate closed between building the menu and the click; Watch-Silver answered every such case. | UI completeness | The menu consequence switched over the expected results only. | A default branch with a neutral refusal line (`taom_tidings_refused`). |
| 6 | LOW | A distance of 0 (what a failed path search stores) passed the Tidings reachability check and would list that town first. Unreachable today: the shipped cache holds every town pair. | Data validation | The check was written as "finite and not negative"; 0 was read as a valid distance though the current town is already excluded. | `distance > 0f`, through `FiniteFloatValidator`; a test for 0 and one for a NaN cutoff. |
| 7 | LOW | Hand-written finite checks instead of `FiniteFloatValidator`. Repeat offender: `rca-postern-gate-2026-10-18.md` finding 13. | Hygiene | `csharp-architecture.md` names the validator; the builder wrote the check inline twice. | Use the validator. |
| 8 | LOW | Smaller items: the hide-or-grey rule lived untested in the behaviors; dead members (`Name`, `IsCastle`, two `Paid`, `TidingsResult.Success`); an unreachable read-back branch; a `NotInTown` gate the menu id already guarantees; a hand-written `IndexOf`; weak no-save pins; a duplicated settings test; no pin on the dialogue chain whose reply order matters; a wrong comment about text variables; two wrong engine claims in the doc (when the rebellious label clears; what moves loyalty daily); a wrong example ("at 47, a single +2 row"); a missing row in the Tournaments settings table; a hint that named the wrong speaker; doc structure; #931 not yet listing the keys. | Hygiene, docs | Local slips. | Fixed in the fix pass; #931 gets its comment at merge. |

## Root-cause pattern

Findings 2, 3 and 7 repeat lessons already on record, as in the Namegiving review. The brief for this feature named the
lesson for each engine event (the Namegiving RCA's new rule), and no finding came from an engine event. The repeats all
sit in the code a builder writes around the events: latches, validators and shared helpers. The brief named none of
those. The next brief (#860) will also list the shared helpers a builder must use (`FiniteFloatValidator`, `ScreenFaultLog`,
`PlayerPurse` for a gold charge, and the co-op providers the single-player predicate reads: `ICoopSessionProvider` and
`IDedicatedServerProvider`) and the latch rule, so the builder starts from them.

## Why each lens caught or missed these

- Lens 1 (standards) found 3, 7, the hide-or-grey placement and the weak pins; lens 5 (data flow) found 1, 2, 4, 5 and the variable comment; lens 3 (efficiency) found 6 while costing the distance path; lens 2 (engine) verified every member and found the two doc claims; lens 4 (completeness) found the #931 gap, the wrong example, the settings table and the missing dialogue pin; lens 6 (design) turned 1 into one shared purse and found the dead members, the unreachable branch and the redundant gate; lens 7 (XML) confirmed the 11 string rows byte for byte and found the translator's blind spot below.

## Not applied

- `tools/translate_with_claude.py` checks placeholders with `\{[A-Z_][A-Z0-9_.]*\}`, which does not see `{newline}`, so a paid run could drop it from `taom_tidings_body` unnoticed. Pre-existing tooling outside this change; follow-up on Mike's word, and the #931 comment asks for a spot check.
- The single-player predicate is now copied in six services. A shared member is a follow-up on Mike's word.

## Lessons appended

- `lessons/gamemodels-services.md`: the receipt lesson gains the throw-after-debit case.
- `lessons/build-tooling-workflow.md`: the one-latch-per-reason lesson gains this occurrence (a constant key on a singleton).
