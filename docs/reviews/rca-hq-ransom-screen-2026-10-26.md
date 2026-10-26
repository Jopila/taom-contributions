# RCA: Hard Questions on the ransom screen (#1001, deep-review, 2026-10-26)

## Top line

A play test found that a captive lord killed by Hard Questions' Force from the backstreet ransom screen stays on that
screen. Selling him there ransoms him back to life (`EndCaptivityAction.ApplyByRansom` calls `ChangeState(Released)`,
which has no dead-hero guard) and pays his ransom. The fix hides every Hard Questions line while a ransom-mode party
screen is open (fix A, approved by Mike). Six lenses reviewed it: no code gap, and every finding LOW.

The pattern: the earlier fixes (Hard Questions round 1, Sworn Anew) taught when to commit an open party screen, but
not that one screen can never be committed into the party. Sworn Anew refused a defection on the ransom screen
(`SwornAnewService.Allows`); Hard Questions, written first, never got the same check.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | HIGH | A lord killed from the ransom screen stays on its cloned roster; a sale revives him and pays his ransom. | The round 1 party-screen review asked what Escape undoes on screens bound to the live rosters; nobody asked which screens a captive conversation can open from. | Every line is unavailable while the top `PartyState` is in ransom mode (`CaptiveLordAdapter.IsRansomScreenOpen`, `InterrogationService.Eligible`). |
| 2 | LOW | The first build wrapped the read in a try that returned false, which fails open (it shows the lines). | The builder copied a "throw-safe in a condition" habit without asking which way the guard must fail. | Expression-bodied read; it cannot throw. |
| 3 | LOW | The deed-level test's kill assert could not fail: the fixture lord was not wounded, so `Force` never reached the kill. | Repeat of `lessons/testing-qa.md`, "A test must be able to fail". | Wounded lord, roll under the death chance; a mutation run proves both ransom tests fail without the gate. |
| 4 | LOW | Checklist item 14 asked to talk to a captive on the alley screen, which lists no prisoners (`PartyScreenHelper.cs:469`). | Written from the old item's text, not from the engine. | Deleted; items 5 and 6 cover the commit. |
| 5 | LOW | Doc drift: the strip's reason, the commit skip "serving Sworn Anew" (Sworn Anew never commits there), `sworn-anew.md` calling this bug open, and the issue saying he reappears in the settlement (his settlement is null, so he is alive only in the hero lists). | Text written before the engine trace. | Corrected; the issue's close comment states the corrected effect. |
| 6 | LOW | No test pinned the whole-stack probe; a switch to `ActiveState` would silently undo the fix. | The service tests fake the adapter. | Source pin in `HardQuestionsWiringTests`. |
| 7 | LOW | Test naming and a duplicate test with redundant stubs. | Hygiene. | Renamed and deleted. |

## Not done, on purpose

- No re-check of a queued deed when it runs. It can only matter if `EndConversation` throws before it raises
  `ConversationEnded`; that is speculative, so per the simplicity criterion it is left out (offered to Mike on 2026-10-26; not asked for).
- My first comment on #869 suspected Sworn Anew had the same hole. It does not; the correction is posted there.

## Lesson

`lessons/adapters-taleworlds-api.md`, "A conversation can open from the ransom screen: refuse a deed that its cloned
rosters would undo".
