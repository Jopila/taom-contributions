# RCA: co-op player attribution, Batch B (#966), 2026-10-21

**Summary.** Batch B taught six TAOM features to tell a co-op player's party or clan from an AI one
(recruit cost T3, War of the Ring participation T5, troop weight T6, alignment desertion C5b, the
field camp X1 and the enlistment offer C7). A six-lens `/deep-review` and a Codex pass (gpt-6-astra,
max) ran on the uncommitted tree. Two lenses and Codex found, independently, that T3 never charged
anyone: the party id Patch113 captures is BannerlordCoop's object-manager key, not the party's
`StringId`. Completeness found that C5b protected nobody at default settings. Codex found that
co-op's prisoner and mercenary recruit paths never raise the event T3 listens to. All three came
from the same habit: a design premise about another system was taken from a name or a summary, not
from the code that produces or consumes the value. The same family of mistake as Batch A's
([rca-coop-safety-nets-2026-10-21.md](rca-coop-safety-nets-2026-10-21.md)), the same day.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | T3 never charged a guest. The client sends `objectManager.TryGetIdWithLogging(MobileParty)` (BannerlordCoop `ClientTroopRosterHandler.cs:37,54`), and every party key is `{typeof(T).Name}_{id}` (`AutoRegistryBase.cs:141`), so the scope held `MobileParty_<StringId>`. `SpecialResourcesBehavior` compared it with `MobileParty.StringId`, never matched, and returned without a log | Another mod's data contract assumed | The design read the server handler that receives the id and named it "the mobile party id"; nobody read the client code that writes it. The real-Harmony test fed `"guest_party"`, a value already in the shape the code expected | Fixed: `ToPartyStringId` strips Coop's prefix (its own `Compact` rule), the test uses a real key shape, and a scoped miss now logs once. Lesson in `lessons/adapters-taleworlds-api.md` |
| 2 | HIGH | C5b protected no remote player at default settings. `isPlayerOwned` only chooses which toggle applies (`AlignmentDesertionService.cs:64-65`), and both toggles default on (`TaomSettings.cs:1271,1276`), so a remote player's troops still deserted, silently. With "Apply To Player" on and "Apply To AI" off, an unreadable registry made every AI clan desert | A flag's meaning taken from its name | The design treated "player-owned" as "protected" without reading what the service does with the flag | Fixed by Mike's decision: during a session a remote player's clan and an Unknown owner lose no troops (`DesertionOwnerPolicy`). Lesson in `lessons/campaign-mechanics.md` |
| 3 | HIGH (pre-existing) | Under co-op nobody is charged `recruit_cost` for prisoner or mercenary recruits, host included. Co-op disables `OnMainPartyPrisonerRecruited` and applies the effects in `PartyDoneLogicHandler.ApplyPrisonerRecruitmentEffect`, which never raises `OnUnitRecruited`; its mercenary handler does the same | Replaced engine path not enumerated | The design listed the vanilla raisers of the event but did not ask what co-op does to each | Moved to #967 on Mike's decision (needs server-owned balances to matter). Recorded on the issue |
| 4 | MEDIUM | The binding gate change passed every patch whose `Prepare()` returns false, not only Patch113. Three Patch7 `CultureStageView` patches use exactly that form, so an engine rename would have gone green while Harmony skipped them | Gate exemption broader than its reason | The builder fixed the one failing patch with a rule written for all patches; the comment named only Patch113 | Fixed: an explicit `ThirdPartyTargetPatches` set. Lesson in `lessons/testing-qa.md` |
| 5 | MEDIUM | `AlignmentDesertionBehavior` went past ADR-002's 150 lines (157) by taking on a decision | Entry point growth | No line-count pin covers that folder | Fixed: the decision moved to `DesertionOwnerPolicy`, the roster loop to `TroopRosterRemoval.RemoveUpTo` (shared with `SpecialResourcesBehavior`); now 149 lines |
| 6 | MEDIUM | The "who pays" decision was split: the vanished-party case was inline, and the scoped leader was passed as the main hero's id | Decision split across layers | The policy was written before the inline guard and never caught up | Fixed: `RecruitChargePolicy.ChoosePayer` holds every case, with a test for the vanished party |
| 7 | LOW | A builder wrote a Windows-1252 dash byte (0x97) into `WarEventSnapshotAdapter.cs`; the compiler accepted it and only an unrelated tool test failed | Encoding | Nothing checked the encoding of C# sources | New gate `SourceEncodingTests.EveryCSharpSourceFile_IsValidUtf8`, proven red with a probe file |
| 8 | MEDIUM | Six new doc citations pointed at lines that do not exist (for example `DesertionOwnerPolicy.cs:41-45` in a 20-line file); the numbers ran on across files, the shape of one numbered listing over several files | Citation taken from a combined listing | The docs builder read several files in one numbered dump and copied its numbers; nothing checks that a cited line exists | Fixed and re-checked with a citation script. Lesson in `lessons/build-tooling-workflow.md` |
| 9 | LOW | Smaller items, all fixed: a redundant `PlayerControlPolicy` layer; an unused `OfHero`; Patch113's `Prepare` silent when Coop's target moved; hand-written double-checked locking (now `Lazy`); comments that contradicted the code; the T6 check placed before a cheaper exit; missing bind, Unknown-branch and wiring tests; stale docs | Various | Ordinary first-draft gaps | Fixed in the same change |

## Root-cause pattern

Findings 1 to 3 are one habit: **a premise about a value was taken from what it is called, not from
the code that makes it or uses it.** "The mobile party id" was a parameter name. "Player-owned" was
a flag name. "The raisers of `OnUnitRecruited`" was a vanilla list that co-op had partly replaced.
Batch A's lesson that morning was the outside-source version of the same mistake (a register's
premises were not checked against TAOM's RCAs and the engine). This one is the inside version: our
own design brief carried the premises, and the builder implemented them faithfully.

## How the reviewers found these

- **Engine compatibility** and **Data Flow** each traced the id from the client's sender and found 1.
  Data Flow also showed the C5b toggle cost (2) and the empty server ledger behind T3.
- **Completeness** found 2 by reading the service behind the flag, and listed the owed docs and tests.
- **Codex** found 1 and 3, and 4. It escalated the prisoner path to HIGH with its real cost (the
  Mumakil rider's 2,000). It classed the C5b behaviour as a decision cost, not a defect, so it did
  not raise 2.
- **Standards** found 4, 5 and 6; **Efficiency** measured every hot path and found no cost that
  matters; **Design** proposed the shapes that were applied.

The build phase missed them because the design brief stated the premises as facts, and the builder
had no instruction to trace a value to its producer or a flag to its consumer.

## Feedback to codify

- **Lesson** (`lessons/adapters-taleworlds-api.md`): an id read from another mod's message is that
  mod's key until its sender proves otherwise; trace it from the sender and test with a real value.
- **Lesson** (`lessons/campaign-mechanics.md`): before designing around a flag, read what its
  consumer does with it.
- **Lesson** (`lessons/testing-qa.md`): a gate exemption names the exempt items; never a rule.
- **Lesson** (`lessons/build-tooling-workflow.md`): take a cited line number from a read of that one file,
  and check every new citation after a docs pass (finding 8).
- **Gate:** `SourceEncodingTests` (finding 7).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/adapters-taleworlds-api.md](lessons/adapters-taleworlds-api.md)
- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)
- [docs/reviews/lessons/campaign-mechanics.md](lessons/campaign-mechanics.md)
- [docs/reviews/lessons/testing-qa.md](lessons/testing-qa.md)

<!-- backlinks-end -->
