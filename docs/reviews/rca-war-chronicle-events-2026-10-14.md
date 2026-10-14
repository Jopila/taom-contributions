# RCA: War Chronicle Batch B (event chain, army focus, momentum award), deep review and Codex pass, #765 (2026-10-14)

**Summary.** Batch B of the War Chronicle work (#765, committed as `84f5e63a9`) is the scripted event chain
(13 lore events, 7 of them siege windows), the army focus that biases AI armies toward a window's town, and
the momentum award a resolved siege pays. A deep review of 56 findings (C01 to C56; workflow journal
`wf_26ecc518-3f7`) had verifiers confirm 21 (C01 to C21: 1 MEDIUM, 20 LOW) and refute none; the other 35 were
INFO, NIT and KEEP-style proposals that nobody verified, and each was applied, answered or left as a note
(below). The review fixes landed in `7e8451555`. The maintainer then decided two of the findings on
2026-10-14 (C04 and C07, `bad32ad08`). Codex found one more MEDIUM that the review had missed: the owner and
siege-start callbacks skipped the War Ended check that the launch and the daily step make, so a conquest
between War Ended and the next daily step could pay an Active siege (F1, fixed in `8f2c9e458`).

## Findings (deep review)

The highest severity was C01 (MEDIUM). The rows below are the confirmed findings; every one was applied in
`7e8451555` unless the last column says otherwise. "Why missed" is the builder's blind spot, read off the
claim and the fix.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C01 | MEDIUM | An Active siege window closed Held at its close day while an attacker still besieged the target; a capture a few days later was ignored and the Held children (ents, gondor, blackgate) still fired. | State machine gap | The close was written as a pure date test; no test put a live besieger past the close day. | The close defers while an attacker besieges, capped at one more `windowDays`. Tests: deferred, third party does not defer, capped. |
| C02 | LOW | `ChangeKingdomAction` join, defection and rebellion leave change `clan.Kingdom` without `OnSettlementOwnerChangedEvent`, so an open window noticed a new owner only at its close. | Event-only detection | The design leaned on one engine event as the owner signal. | The daily step re-checks the owner of every Active window; the event stays as the fast path. |
| C03 | LOW | Days were fractional (`ElapsedDaysUntilNow`), so a daily handler running just after midnight carried its overshoot into `CloseDay`. | Unit drift | The ledger floors the same clock; the chronicle did not. | Whole campaign days, the same floor as the ledger. |
| C04 | LOW | Phase-anchored events opened on the live configured phase day, so switching the War of the Ring on late or lowering a phase day fired every overdue event at once. | Config read at the wrong time | The anchor was derived from a setting, not from when the phase began. | Maintainer decision, see "Decisions". |
| C05 | LOW | Two Lapsed causes shared one text; all seven shipped texts say no attack came, which is false when a third party took the town. | Wrong player text | The text was chosen per event, not per cause. | New generic line `taom_wc_siege_lost` for a town that changes hands. |
| C06 | LOW | The queue's own `_showing` latch relied on a close callback that a hidden popup never runs, so the queue could stall. | Duplicated state | The latch re-implemented `IsAnyInquiryActive`. | The latch is gone; the queue asks the engine. |
| C07 | LOW | A Skipped siege skipped its whole chain: Mordor already holding East Osgiliath skipped osgiliath, gondor and blackgate. | Design question | The chain rule was written for a defender that still holds. | Maintainer decision, see "Decisions". |
| C08 | LOW | `IsMapFree` null-checked `PartyBase.MainParty`, a computed getter that dereferences `Campaign.Current.MainParty.Party` and throws with no main party. | `?.` on a computed property | The rule is in a rules file, not in a gate. | The presenter no longer reads `PartyBase.MainParty`. |
| C09 | LOW | `TryShowNext` ran the co-op probe (reflection, an allocation) before the cheap map check, on every tick. | Check order | Cost order was not considered on a per-frame path. | Map check first, then the probe. |
| C10 | LOW | The Army Focus hint named "Army Strategic Intelligence"; the MCM setting is "AI Strategic Intelligence". | Wrong name | The name was typed from memory. | Hint and docs say "AI Strategic Intelligence". |
| C11 | LOW | The 2.5 focus cap and the 300 momentum cap were hand-synced across the registry, the provider and the MCM slider literal. | Duplicated constant | Each consumer got its own literal. | One constant each, and a test that the slider spans the provider's clamp. |
| C12 | LOW | A process-singleton registry held per-campaign state and reset through `Clear()`, so the second-campaign audit grep missed it. | Naming a rule | `ResetForNewSession` is the audited name. | The registry gets a session reset. |
| C13 | LOW | The clamp rationale for `GetEffectivePhaseDays()` sat above the new `GetEffectivePhaseDay`. | Comment rot | Method inserted above an old comment. | Comment moved. |
| C14 | LOW | The doc said `ChronicleShippedContentTests` pins the target owners; the test reads only repo files and cannot. | Over-claim | The doc described the intent. | Doc says what is pinned (ids, Hostile pairs) and that the owners were confirmed by hand. |
| C15 | LOW | A doc cell said 357 and 225 but still ended "rather than 222 values". | Stale count | Half-edited cell. | Cell corrected. |
| C16 | LOW | `TryAwardScripted` was absent from `DivergenceProneMutators`; the scan skips files ending in `Service`, so no call site was checked. | Gate gap | The new mutator was not added to the list. | Entry and a classification test row (`CoopVetoClassificationTests`). |
| C17 | LOW | A misspelled key in `war_chronicle.json` (`momentum`, `outcomes`, `effects`) was dropped silently. | Silent data loss | The validator read known keys and ignored the rest. | Unknown keys warn; a `_` prefix is an author's note. |
| C18 | LOW | `TryEnum` accepted `"+1"` and `" 1"` on this .NET Framework, so a number loaded as a kind, anchor or outcome. | Parser leniency | The guard rejected only a leading digit or minus. | Names only, trimmed, any case; a number, sign or comma list never parses. |
| C19 | LOW | `ChronicleConfigProvider` had no test: path, missing file, read failure. | Missing test | The shipped-content test read the file by its own path. | `ChronicleConfigProviderTests` (5). |
| C20 | LOW | Two of `Close()`'s four outcomes had no test: Fell at the close and Lapsed "lost to another". | Skip-guard exhaustion | Tests followed the happy window. | Both added to `ChronicleServiceTests`. |
| C21 | LOW | The analyzer's outcome count dropped an exact repeat and left it where first read, so `[Held 69, Fell 60, Held 69]` counted Fell. | Over-claim | The dedupe rule was written for order, not for "last line read". | `chronicle_last` keeps the last line read, repeats included; three Python tests. |

**Not individually verified (C22 to C56), by disposition.**

- **Applied:** C22 (the LINQ `ActiveSiegesAt` helper is gone), C23 (switching the chronicle or announcements off
  drops the queue), C24, C25, C26, C31, C38, C39, C42 (doc sentences: defender focus waits for an attack,
  listener order, the second owner-change fire needs a multi-clan kingdom, the cohesion-boost side effect, the
  army-targeting rows, the 13-and-7 wording, Test Mode lore order), C29 (non-finite clock documented), C30
  (a resolved row without `res` is skipped on load), C32 (`taom.chronicle_status` marks the focus inactive when
  AI Strategic Intelligence is off), C33 (a refused momentum award logs), C34 and C35 (the English default and
  the singleton registration are pinned), C37 (feature-map row names the event chain), C40, C41 (summary and
  test name), C46 (typed rows through the codec), C47 (`ChronicleFocusSync` deleted), C48 (shared
  `SettingClamp`), C49 (`ITextLocalizerAdapter` instead of the presenter), C50 and C51 (dead clause removed,
  `TryGetValue`).
- **Left as notes** (no change in `7e8451555`): C27 (use `BesiegerCamp.MapFaction`), C28 (a negative escape
  magnitude passes the validator and does nothing), C36 (untested refusals of `OpenChronicleEventNow`), C43 to
  C45 (analyzer docstring and `--compare` for chronicle rows).
- **Follow-up:** C52 to C56, below.

## Decisions (maintainer, 2026-10-14)

Taken on the orchestrator's recommendation; the code changes are in `bad32ad08`.

- **C04, phase anchors (changed).** A Phase 1 or Phase 2 event counts from the later of the configured phase
  day and the day the chronicle first saw the phase begin (saved as `phases`; the day before the phase machine's
  tick, because the chronicle's listener runs first). A campaign on schedule keeps its days; a late War of the
  Ring, a lowered phase day or a mid-campaign Test Mode now opens events one by one.
- **C07, lost siege at open (changed).** When an attacker already owns the target as a siege opens, the event
  resolves Fell at once, pays its Fell effects and momentum, announces the fell line, and its Event-anchored
  children still open. A target owned by a third kingdom or by nobody still Skips.
- **Kept:** the Batch A fix choices; #772 F25 and F26; the `taom_wc_siege_lost` text.

## Codex review (gpt-6-astra, on `ac0a3e0c0..bad32ad08`)

Raw output `docs/reviews/raw/codex-adversarial-war-chronicle-events-2026-10-14.md` (gitignored). 0 CRITICAL,
0 HIGH, 1 MEDIUM, 0 LOW, 0 false positives. Codex did not run the full build or suite (a no-file-write
constraint); its F1 rests on a production-service reproduction and the cited v1.5.4 callback code.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | `OnSettlementOwnerChanged` and the siege-start callback skipped the War Ended check that launch and the daily step make, so a conquest between War Ended and the next daily step could resolve and pay an Active siege and leave a stale focus. | Terminal state checked on one path | The check was written where the contract (`war-chronicle.md`, War Ended skips everything) was read, the daily step; the engine-callback entry points were added later. | The check lives in the shared callback wrapper, so every entry point has it. A test fires an owner change after War Ended. |

Suspect dispositions: S1 (resolution, duplicate conquest notifications, rebellion, overtime) disputed as an
ordinary-resolution defect, confirmed only through F1; S2 (persistence, reset, reload, adoption) disputed,
F1 confirmed for the callbacks; S3 (C04, listener order, load edges) disputed, no finding; S4 (C07, eliminated
defender, dead recipients, pre-war momentum) disputed, no finding; S5 (queue, map gate, inquiry, co-op
callback) disputed, rendering and live co-op UNVERIFIED; S6 (empty-focus cost, Strategic Intelligence off,
stale focus) disputed except the stale focus inside F1's interval; S7 (settlement and kingdom ids, Hostile
pairs, localization) disputed, no finding.

## Root-cause patterns

**A terminal state is checked on every entry point, not on the one the contract names.** F1 and, one level
down, C02: the design had a clean daily path (War Ended check, owner check) and then two engine callbacks
that reached the same resolve method around it. Put the guard where all paths converge.

**One engine event is not the only way the state changes.** C02: owner changes through defection and rebellion
raise no owner event. A listener is a fast path; the daily step is the source of truth.

**A date test that ignores the live world.** C01: the close was a comparison against `CloseDay`, and the
besieger was never consulted. The test fixtures had no besieger past the close day.

**Strict parsing is a feature of the data file, not of the caller.** C17 and C18: a typo or `"+1"` loaded
silently. A data file the maintainer hand-edits needs unknown-key warnings and name-only enums from the first
version.

**Words that outrun the code.** C10, C14, C15 and several INFO docs: a hint, a count or a "pinned by" claim
written from intent. The fix is mechanical (recount, reread the test), the gate is the review.

## Follow-ups (outside this change; issues on Mike's word)

- C52: the ledger analyzer's capital verdict reports PASS for a campaign with no kingdom lines (a chronicle-only
  log from an old save loaded and closed). Needs a "no kingdom lines" UNKNOWN.
- C53: `RepoPaths.StripComments` over all `Main` files in three wiring tests; same one-place fix as Batch A's
  B31 and B44, cost never timed.
- C54: Bree (`town_AN3`) and every Arthedain town exist only in the live `TAOM_Map`; the packaged TAOM_Map in
  the releases folders has none, so `north` is Skipped at session launch until the next package.
- C55: issue #765 still names Dale and Fornost as siege targets; the data targets Eldby (`town_S3`) and Bree.
- C56: `check_external_loc_coverage.py` fails on live-install drift (+38 TAOM_Map rows and +5 Armory rows per
  language, from 2026-10-07 edits), not on this diff.
- Context, not this work: the installed game moved to v1.5.5 on 2026-10-09 while the target stays v1.5.4, and
  9 data tests fail on trunk from `08b30bfc4`.

## Lessons recorded

`docs/reviews/lessons/state-lifecycle-save.md`: every engine callback path re-checks the terminal state, not
only the daily tick.
