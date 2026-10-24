# RCA: Sworn Anew (deep review, 2026-10-24)

## Top-line

The `/deep-review` of Sworn Anew (#869), which gates a captured lord's offer to join a ruler by lore and alignment, ran on
the uncommitted worktree `E:\repos\taom-relations-politics`. The planning architect and six lenses reported five MEDIUM
findings and two groups of LOW findings, and no HIGH. The most expensive one was found before any code existed: vanilla
v1.5.5 already offers a ruler a "join us, and be set free" line to a prisoner that never asks `DefectionModel`, so a
model override alone would have left the feature ungated and a Gondor ruler could have recruited a captured Mordor clan
leader. Every finding was re-checked against the code and fixed in the working tree; the full suite then passed (25065
passed, 0 failed, 7 skipped) and the binding gate passed (744 passed).

Repeat check: `docs/reviews/rca-*.md` and `docs/reviews/lessons/` were searched for `DoneLogic`, party screen, `Escape`,
`PartyScreen`, `ResetUsing`, `DefectionModel`, `ChangeKingdomActionDetail`, `JoinAsMercenary`,
`ConversationEndOneShot` and "every engine path". What repeats:

- **Finding 2 is a related repeat.** `adapters-taleworlds-api.md` ("A roster write during a party-screen conversation is
  reverted by Escape", from `rca-hard-questions-2026-10-19.md` H4) says to commit an open party screen with
  `DoneLogic(true)` after a roster write made from a conversation. It does not say to check which screen is open, and
  that gap is exactly this defect. Hard Questions and Tokens of Esteem followed the lesson and carried the same unguarded
  commit. `harmony-il.md` (roster-loss diagnostic, near line 1109) holds the matching `ResetUsing` fact and
  `rca-good-deeds-2026-10-20.md` met the party screen from the other side.
- **Finding 1 is a near repeat of a different lesson.** `harmony-il.md` (near line 773) asks for "a written list of every
  engine path that tears the object down" before a lifecycle hook. The same habit applies to a GameModel gate and to an
  outcome, but no entry says so, and the builder met none of them. New lesson filed.
- **Findings 3, 5 and 6 have no earlier lesson.** Finding 4 and the LOW group are doc and test drift covered by
  existing general rules; no entry added for them.

## Findings and root causes

| # | Sev | Finding | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | Vanilla v1.5.5 offers a ruler a "join us, and be set free" line to a prisoner without asking `DefectionModel`. A model override alone would have left that line ungated, so a Gondor ruler could recruit a captured Mordor clan leader (planning architect) | Engine paths; a gate on one of several roads to the outcome | The plan listed the callers of the model it meant to override and stopped there. The conversation line reaches the same outcome by a second road | A postfix on the line applies the same verdict as the model. Lesson in `adapters-taleworlds-api.md`: before gating a decision through a GameModel, list every engine path to the outcome |
| 2 | MED | The party-screen commit ran on any open party screen. On the ransom-broker screen (cloned rosters, a Done handler that sells again) it would sell prisoners and ransom heroes a second time (Lens 2) | Engine state; a forced Done on a screen not bound to the live roster | The H4 lesson says "commit an open party screen" and the builder copied it as written. Nothing asked which roster the screen holds. The first guard then checked the wrong roster: a prison-roster identity test also dropped the commits that protect the item roster (found by the convergence pass; fixed) | One shared `PartyScreenCommit.CommitIfMainParty` commits a screen whose right owner is the main party and skips only the ransom screen with prisoners on the broker's side. Sworn Anew also refuses while a ransom screen is open. Hard Questions and Tokens of Esteem used the same unguarded commit and now call it |
| 3 | MED | On co-op peers the veto was switched off with the rest of the feature, and the co-op server checks no alignment, so a client could recruit across sides (Lens 5) | Co-op; suppression applied to a client-only hiding step | The feature's co-op switch was applied to the whole feature, and the three co-op questions (loaded, peer authority, dedicated server) were not asked of each part | The veto, which only hides a line locally, runs on every peer. The model and the commit keep the co-op suppression |
| 4 | MED | The MCM hint and the doc said that with the switch off captured lords can only be ransomed or released "as in the base game". Vanilla's ruler line stays (Lenses 2, 4, 5) | Docs claim not read from the source | The sentence described the intent. The vanilla conversation lines were not listed, which is the same gap as finding 1 | Hint and doc corrected to say the vanilla ruler line stays when the switch is off |
| 5 | MED | `GroupOrder` 69 was taken on trunk during the review (Hearth Roll) (Lens 4) | Merge; a shared slot taken while the work was open | The slot was free when chosen and nobody re-read trunk before the gate | Moved to 71. Settings counts recomputed at the merge |
| 6 | LOW | The orchestrator's own planned change, vetoing vanilla's line on lore and alignment only, would have reopened mercenary-clan prisoners, whose join raises `JoinAsMercenary` and never arms the commit (Lens 5) | Engine paths; keying on one `ChangeKingdomActionDetail` | The flow was read for the detail it usually raises and not for the others it can raise | Dropped before it was built. Lesson in `campaign-mechanics.md` |
| 7 | LOW | Test and record quality: a wiring test that could not fail, an assertion on an engine detail, a latch replaced by the engine's `ConversationEndOneShot`, a duplicated parameter, positional facts (named arguments now), five tests repeating a gate, the API snapshot not regenerated, checklist steps 6 (Harad is Evil in TAOM) and 7 (Sauron is refused as a ruler first), a stale overlap comment, the "first eight facts" count (Lenses 1, 4, 6) | Test shape and doc drift | Builder self-check; helpers and counts were written once and not re-derived | One `FactionSideRules.AreOpposed` replaces five spellings of the Free-against-Evil rule. One generic slot-filler test replaces three per-feature copies. The snapshot is regenerated, the checklist steps and counts corrected |

## Root-cause pattern

Findings 1, 2, 3 and 6 share one habit: the design named the road it was thinking about and guarded that road. Vanilla
had a second road to the same outcome (1). A commit written for one screen ran on every screen (2). A switch written
for the whole feature also switched off a part that is safe on a client (3). A key written for the usual action detail
missed the others the flow can raise (6). In each case the guard was correct for the case in mind and the unguarded case
was one grep away: the callers of the outcome, the roster the screen holds, the three co-op questions, the members of the
detail enum.

Finding 2 also shows a lesson that was followed and still misled. The H4 entry was precise about what to do and silent
about when not to, so two earlier features copied its unguarded form. The new entry adds the condition and points at the
shared helper.

## Convergence note

The convergence pass found that the first fix for finding 2 guarded the wrong roster. It committed only when the screen's
right prison roster was the main party's own, which also skipped the screens where a gift, a strip or a defection had
written the item roster (the ransom screen binds the live item roster but cloned troop rosters). The guard now checks that
the right owner is the main party and skips only the ransom screen with prisoners on the broker's side. Sworn Anew
additionally refuses the offer while the open party screen is a ransom screen, because that screen's cloned prison roster
could still ransom a lord who just joined.

## Why each agent missed these

- **The builder** worked from the plan, which guarded the model and not the vanilla line (1), copied the H4 commit as
  written (2), applied the co-op switch to the whole feature (3) and wrote the hint from intent (4).
- **The planning architect** found the ungated vanilla line (1) before code was written, which is why it cost a postfix
  and not a rework.
- **The orchestrator** proposed the lore-and-alignment veto on vanilla's line (6) without listing the
  `ChangeKingdomActionDetail` values a join can raise. Lens 5 caught it before it was built.
- **Lens 1 (Standards)** found the duplicated parameter, the positional facts and the repeated gate tests (7).
- **Lens 2 (Engine)** found the commit on the ransom-broker screen (2) and the hint's base-game claim (4).
- **Lens 4 (Completeness)** found the taken `GroupOrder` (5), the stale API snapshot and the doc and count drift (4, 7).
- **Lens 5 (Data flow)** found the co-op suppression (3), the mercenary-clan path (6) and the doc claim (4).
- **Lens 6 (Design)** found the test that could not fail and the five-spellings rule that became `AreOpposed` (7).
- **No lens missed a confirmed finding.** Findings 2 and 4 were each reached by more than one lens.

## What proves it

- The full suite: 25065 passed, 0 failed, 7 skipped.
- The binding gate: 744 passed, with the API snapshot regenerated.
- One shared `PartyScreenCommit.CommitIfMainParty` is the only party-screen commit in the three features that force Done.

## Feedback memories to codify

None. The rules are in `lessons/adapters-taleworlds-api.md` and `lessons/campaign-mechanics.md` under #869.
