# RCA: Hard Questions (deep-review, 2026-10-19)

## Top line

`/deep-review` ran all seven lenses on the uncommitted Hard Questions build (#896, worktree
`feat/lords-notables-prisoners`, base `89374fc4f`). It confirmed 4 HIGH, 3 MEDIUM and 19 LOW findings,
all fixed in the same session before any commit, plus 7 behaviour-preserving improvements. The suite went
from 17,909 to 17,937 passing tests. The HIGH findings share one cause: the plan and the build reasoned
from engine **signatures** and never read the **bodies** of the engine methods the feature leans on
(`ChangeRelationAction.ApplyPlayerRelation`, `EquipmentHelper.AssignHeroEquipmentFromEquipment`), and the
test fakes then encoded the same wrong model of the engine.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| H1 | HIGH | The fresh kit on release copied all 12 slots and deleted the named pieces the strip had left on the lord (decision D1 broken at release). | Engine body not read | The plan named `EquipmentHelper.AssignHeroEquipmentFromEquipment` as the re-kit seam from its signature and from vanilla's come-of-age caller, which always starts from an empty hero. Its body writes every slot, empty kit slots included. | Lesson in `lessons/adapters-taleworlds-api.md`. Fixed: slot-wise copy through `KitSlotRule.CanFill` (only empty or rag slots, never an empty kit slot). |
| H2 | HIGH | Relation penalties added up per clan: the engine stores the player's relation with any clan member on his clan leader, so -20 per relative or -10 per honourable lord summed on one number and clamped whole clans to -100. | Engine body not read | The plan read the `ApplyPlayerRelation` signature, assumed `affectRelatives: true` spreads a change to kin, and built "per lord" penalties with a per-hero dedupe. The body ignores `affectRelatives` and routes through `DiplomacyModel.GetHeroesForEffectiveRelation`. The fake adapter recorded per-hero calls, so 114 tests agreed with the wrong model. | Lesson in `lessons/adapters-taleworlds-api.md` and a trap-index line in `docs/ai-includes/orientation.md`. Fixed: `LordRef.RelationId` from the engine's own mapping, strongest delta per owner, one call per owner. |
| H3 | HIGH | A Neutral victim (Khand, Umbar, the Harad cultures, any unlisted culture) cost no relation at all, not even with himself. | Shared rule faked away | D3 reused `IExecutionRelationService`, which by design zeroes every evaluator of a Neutral victim ("Neutrals don't care"). Execution still has vanilla's blood feud for the victim's own clan; Hard Questions has no feud, so nothing was left. The fixture passed every delta straight through and resolved every side Neutral, so the rule never ran in a test. | Lesson in `lessons/testing-qa.md`. Fixed: a floor for the victim's own relation owner only (Execution's semantics for every other clan kept), with a test over the real `ExecutionRelationService` and `AlignmentService`. |
| H4 | HIGH | A deed in a conversation opened from the party screen was undone by Escape: `PartyScreenData.ResetUsing` refills the live prison and item rosters from the pre-conversation snapshot, so a force death put the dead lord back in the prison roster and the strip loot vanished. | Known mechanism, scoped too narrowly | The same `ResetUsing` behaviour is recorded in `lessons/harmony-il.md` (2026-10-17), but for a roster diagnostic, and nothing loads it for a feature that writes rosters from a conversation. The plan treated `ConversationEnded` as "after the conversation" without asking what screen the conversation returns to. | Second occurrence of the mechanism, so a stronger trigger: a trap-index line in `orientation.md` plus a lesson in `lessons/adapters-taleworlds-api.md`. Fixed: after each deed, commit an open party screen with `PartyScreenLogic.DoneLogic(true)`, as vanilla's `GauntletPartyScreen.OnCompanionRemoved` does. |
| M1 | MED | Eligibility gated on `IsAuthority` and release on `ShouldDeferToHost`; under a co-op mod whose role TAOM cannot read, every peer could strip a lord who was then never re-kitted and never asked again. | Two predicates for one question | The builder took `IsAuthority` from the dialog template (`FieldCommissionDismissDialogBehavior`) and `ShouldDeferToHost` from the state template. The trap index already says co-op loaded, peer authority and dedicated server are three questions. | No new rule. Fixed: both gates on `ShouldDeferToHost`, which `ICoopSessionProvider` names for shared world state. |
| M2 | MED | The strip's "up to 10" Honor companion penalty could never exceed 2: the formula capped the loss at the trait level, and Honor runs -2 to 2. | Fake outside engine range | Tests fed companions Honor 3, 4, 5, 9 and 20, values the engine clamps away (`Hero.SetTraitLevel`). | Lesson in `lessons/testing-qa.md`. Fixed: loss = round(cap x level / 2); tests use levels 1 and 2 only. |
| M3 | MED | Every dialog condition built a full world snapshot, including a scan of every living hero (about 4,150), on every hub display of every hero conversation. | Snapshot shape | The plan specified one snapshot for every use; only the two deeds read its lists. | No new rule. Fixed: `CaptiveSnapshot` keeps scalars, `CaptiveCircle` is built once per deed. |
| L1 | LOW | A lord who dies in captivity kept his save entries: `EndCaptivityAction` returns on Death before raising `HeroPrisonerReleased`. | Event coverage | The plan assumed the release event covers every end of captivity. | Fixed: `HeroKilledEvent` clears the record. |
| L2 | LOW | The kingdom topic (3.5 times the price, the heavier costs, the death roll) was offered for a lord with no kingdom, then revealed only his clan. | Eligibility gap | Not in the plan's eligibility list. | Fixed: the topic needs a kingdom. |
| L3 | LOW | Two identical stacks in one set gave one; the second was cleared and lost. | Dedupe key | D4's "once across sets" was implemented as "once overall". | Fixed: per-set maximum count. |
| L4 | LOW | The reveal called `SetHasMet` (skips the lord's later introduction) and overwrote vanilla's last-seen place with his home. | Side effects not read | The `IsKnownToPlayer` setter already raises the event whose listener places the hero. | Fixed: the reveal sets `HasVisited` and `IsKnownToPlayer` only. |
| L5 | LOW | `Hero.MakeWounded()` clears an existing `DeathMark`; `KillByWounds` reported success when the engine refused the death and charged the extra Honor anyway; a dungeon death left a talkable location character. | Engine bodies not read | Same root as H1 and H2. | Fixed: `HitPoints = 1`, `IsDead` checked, `RemoveCharacterIfExists`. |
| L6 to L19 | LOW | Wrong `affectRelatives` comments (3 sites); 3 type-per-file violations; hard-coded 12 and 6; three stale settings counts in docs (two lenses found different subsets); a stray blank line from the string harvester; a missing Dependencies section, Key Files rows and INDEX line; the in-game checklist missing the paths no unit test reaches; issue #896 claiming `heroes.xml` already used the rag item (no TAOM data ever did). | Hygiene | The count edits were made where the fingerprint test pointed and nowhere else; the issue sentence came from a stale spec line. | Fixed. The harvester's blank-line bug is outside this change (follow-up). |

## Root-cause pattern: signature-level research

H1, H2, L4 and L5 have one cause. AGENTS.md's research rule sends a builder to the installed DLLs for
**signatures**, and the plan stopped there: it read `ApplyPlayerRelation(Hero, int, bool affectRelatives =
true, bool)` and reasoned about what the parameter does, and it read `AssignHeroEquipmentFromEquipment(Hero,
Equipment)` and assumed it fills what the kit holds. Both bodies are a few lines long and contradict the
assumption. The plan then wrote "the existing `ChangeRelationWith` uses `true`, which doubles family hits" as
a fact, which is the "evidence, never invention" failure in its plain form: a claim about engine behaviour
with no body read behind it.

H3 and M2 add the second half: the fakes encoded the same model. A fake that passes a shared rule straight
through (the relation service) or accepts values the engine cannot hold (trait level 20) cannot disagree
with the author, so a green suite only proved the author's belief.

## Why each lens caught or missed what it did

- **Standards (1):** found M1 and the type-per-file items; it does not trace engine behaviour, so H1 to H4
  were out of its scope.
- **Engine compatibility (2):** found H4 (the only lens that walked the party-screen path), H2, H1 and the L5
  items by reading bodies, which is exactly the step the plan skipped.
- **Efficiency (3):** found M3 and set the efficient shapes for the H2 and L1 fixes.
- **Completeness (4):** found M2 by checking the engine's trait range against the test inputs, and the doc and
  checklist gaps.
- **Data flow (5):** found H3 (it ran the shared Execution rule with a Neutral victim) and independently found
  H1, H2 and M1.
- **Design (6):** turned H2's fix into the engine's own mapping and showed that two penalty pairs could never win
  (removed, with three config values).
- **XML (7):** found the stray blank line and the wrong issue premise about the rag item.

The plan (main session) missed all four HIGH findings. Every one was findable from a body read of a method the
plan itself named.

## Rules to codify

1. `lessons/adapters-taleworlds-api.md`: read the body of an engine action, not only its signature, before a
   plan relies on what a parameter or a helper does (H1, H2, L4, L5).
2. `lessons/adapters-taleworlds-api.md` plus a trap-index line: the player's relation with a clan member lives on
   his clan leader, and `affectRelatives` is ignored (H2).
3. `lessons/adapters-taleworlds-api.md` plus a trap-index line: a roster or inventory write during a conversation
   opened from the party screen is reverted by Escape unless the screen is committed (H4, second occurrence).
4. `lessons/testing-qa.md`: a fake must not pass a shared rule straight through or hold values the engine clamps;
   test a reused rule once over the real service (H3, M2).

## Convergence pass

One reviewer checked the applied fixes for parity and found three LOW defects, all fixed:

| # | Sev | Bug | Why missed | Fix |
|---|---|---|---|---|
| C1 | LOW | The doc and a comment said "other Neutral lords" are unmoved by a Neutral victim; in fact every other lord, Free and Evil included, is. | The A3 correction was summarised from its scope note, not from the rule's truth table. | Wording in `hard-questions.md` and `HardQuestionsPenaltyApplier.cs`. |
| C2 | LOW | No test pinned the new `HeroKilledEvent` subscription; deleting it left the suite green. | The L1 tests called the handler directly. | A source-text assertion in `HardQuestionsWiringTests`. |
| C3 | LOW | A comment and the checklist promised the dungeon removal clears a standing dead agent; it only stops his next spawn. | The removal's effect was inferred from its name. | Comment and checklist step 10 corrected. |

Two UNVERIFIED engine questions went to the in-game checklist (steps 5 and 12): whether the dead lord's row stays on an open party screen after the commit, and whether committing a ransom broker's sell screen pays its sale a second time.

## Round 2: the relation summary line (2026-10-19)

After the merge, Mike approved one summary line of relation damage after a force or a strip, as vanilla's
execution shows. Seven lenses and a convergence pass reviewed it. No HIGH; all findings fixed before the commit.

| # | Sev | Bug | Why missed | Fix |
|---|---|---|---|---|
| S1 | MED | The dedicated-server check sat in the adapter, where no test can reach it, while the sibling reproach check sat in the service with a test. | The builder put the guard beside the engine call; `adapters.md` already says adapters are thin and tested through service fakes. | Count, gate and call moved into `HardQuestionsPenaltyApplier.ApplyRelations` (one place, as vanilla's execution); `ApplyRelations_OnADedicatedServer_ShowsNoSummary`. |
| S2 | LOW | The count ignored `ChangeRelation`'s false result, unlike `ApplyCompanionLosses`. | Copied the dictionary size. | Counts successes; `ApplyRelations_AnOwnerTheEngineCannotFind_IsNotCounted`. |
| S3 | LOW | Every fixture had as many hits as owners, so a count of hits passed; and `Received(1).ShowRelationsHurt(2)` cannot tell one call from a call per owner. | The fakes mirrored the happy path; NSubstitute's `Received(n)` with an argument counts only matching calls. | Fixtures with 3 hits on 2 owners, plus `ReceivedWithAnyArgs(1)`. Lesson in `lessons/testing-qa.md`. |
| S4 | LOW | `IS_PLURAL` went into the game-wide text table. | Vanilla's execution line does the same; most vanilla lines set it on the TextObject. | Set on the TextObject (the condition reads the TextObject's variables first). |
| S5 | LOW | A stale "quick notifications are off" comment, missing doc lines (key files, changelog, the -100 case), weak test names and placement, a bribe test that never asserted the bribe was paid, and a commit subject that said "names the clans" for a line that shows a number. | Hygiene. | Fixed. |
| S6 | LOW (process) | The builder wrote code before tests; a deliberate mutant to prove them was refused by the auto-mode classifier. | Brief did not insist on RED-first. | The fix brief required RED-first; failing lines were quoted. |

Merge note: trunk merge `85cbd20c8` rewrote all 12 language files from CR CR LF to LF with a blank line after each
row, so every branch edit to them conflicts with the whole file. This change restored its string files, rebased,
and regenerated the one key with the trunk's fixed harvester.

## Follow-ups outside this change

- `Main/Adapters/IArmouryPlayerAdapter.cs:36` and `CommanderLordAdapter.cs:199` repeat the belief that the
  engine changes kin through `affectRelatives` (#923).
- `tools/harvest_literal_loc_keys.py` added one blank line before `</strings>` per run (fixed on trunk in
  `337228885`; #919 closed).
