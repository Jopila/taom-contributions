# RCA: War Chronicle and Eye of Sauron review follow-ups, #792 (2026-10-14)

**Summary.** #792 closed the follow-ups that the Batch A, Batch B and M8 reviews of #765 left. It took 18
commits on `bannerlord-1.5.x`, `ea8f2ad52` to `a66b5754a`:

- one engine-faithful save-store fake, `EngineDataStore` (E42, `ea8f2ad52`);
- raw-text wiring scans (B31, B44, C53, `06252ae2c`);
- typed Eye wait tests (E25, `3633b6545`);
- the Refuge pin text (E44, `63630e8a0`);
- the hunt popup and Messenger checks on `Campaign.Current?.MainParty`, with `MainPartyNullCheckScanTests`
  (`5f80f95c9`, `91f369984`);
- the Eye docs and the E41 to E47 closes, E43 rejected (`abd03e851`, `49d1dc170`);
- one guarded chunk loop, `SyncDataChunker.SyncChunks` (B45, `16a34027d`);
- a once-per-day gate for the War Chronicle's daily work, `WarChronicleDailyGate` (`238f00337`, #793);
- the `t=load` ledger marker and the analyzer's rewind (B16, `5479109bf`);
- the docs (`573dc155f`), the Arthedain release check (C54, `612b3759c`) and the TAOM_Map coverage baseline
  (C56, `66053a9da`).

A four-lens deep review (standards, engine, data flow, completeness) of the first fixes found that the
analyzer's reload rule assumed the first daily tick after a load is the next day. v1.5.5
`Campaign.CreateCampaignEvents` (`Campaign.cs:1022-1030`) does not guarantee that; the finding became #793,
the gate and the marker. Codex then found 4 LOW, all fixed in `5ba8d6c82`, `dc1c37031`, `d371745c7` and
`a66b5754a`.

## Findings (deep review)

The four lens reports were not kept in the repo, and the commit bodies of the stack describe the fixes only,
so this table holds the one finding recorded (in the #792 issue's Analysis).

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| L1 | not recorded | The analyzer's reload rule assumed the first daily tick after a load is labelled with the next day; on a load the engine waits only the elapsed part of the day, so a save in the first half of an elapsed day ticks its own day again. | Engine timing assumed | `rca-wotr-phase-ordering-2026-07-30.md`, checked against v1.4.7, said the event re-aligns on load. | `WarChronicleDailyGate` for the chronicle's daily work, the `t=load` marker the analyzer rewinds to, and #793 for TAOM's other daily listeners. |

## Codex review (gpt-6-astra at xhigh, on the 14 commits `bc8c9f71b..66053a9da`)

The raw output is `docs/reviews/raw/codex-adversarial-review-followups-2026-10-14.md` (gitignored). It names
the worktree range `a9437a492..839564ccf`, which holds the same patches. Codex reported 0 CRITICAL, 0 HIGH,
0 MEDIUM, 4 LOW and 0 false positives. It ran no build and no `dotnet test`; it ran 67 Python tests and
compiled C# cases in memory (Eye 242, codec 82, gate 13, chunker 7).

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | LOW | `KingdomWarSnapshotAdapter.FloorElapsedDays` turned a NaN, infinite or negative clock into day 0. The tick lane claimed day 0 and the load marker wrote `day=0`, after which the analyzer drops every retained day after 0. | A fallback used as a decision input (a repeat of `lessons/gamemodels-services.md` "Validate input BEFORE a fallback-returning lookup, never after") | The gate validates its own input, and its tests called it directly; the adapter had already normalized the value. | `TryGetElapsedDay` reports validity. The tick skips the day with one warning, `LogLoad` writes no marker, and the tests go through the adapter boundary (`5ba8d6c82`). |
| F2 | LOW | `MainPartyNullCheckScanTests` matched only `PartyBase.MainParty == null`. The hunt guard it followed was `!= null`, and with that guard restored in memory the scan passed. | A guard never run against its offender (a repeat of `lessons/testing-qa.md` "Verify a new guard RED before accepting it GREEN") | The scan was written from the Messenger case. | Both polarities, the reversed and `is` forms, data rows and a focused `IsMapFree` test, proved red on the `a9437a492` files (`dc1c37031`). |
| F3 | LOW | The E25 tests stayed green (242 of 242) when the wait decision was rewritten as `StartsWith("Waiting")`. | A vacuous test (the F2 lesson, and `lessons/testing-qa.md` "Mutation-check a source-text pin") | Wording-free assertions were taken as proof that the decision ignores wording. | A source-text test that fails if `TryStartHunt` inspects the text, proved red with that mutant (`d371745c7`). |
| F4 | LOW | Comments and docs said a save made "before midday" repeats its day. Elapsed days start at `CampaignStartTime`, 09:00 (`DefaultCampaignTimeModel.cs:7`), so a calendar 18:00 save repeats its day and a 06:00 save does not. | An engine clock described in calendar words | `238f00337` wrote the comment without reading the clock's origin. | "The first half of the elapsed campaign day" in the code, the feature doc, the analyzer docstring, the tools README row and three RCAs (`a66b5754a`). |

Suspect dispositions:

- S1 confirmed in part (F1). A save from before the gate repeats its first day by policy, and repeated
  reloads can skip a day; that is an engine limit, not a TAOM finding.
- S2 and S4 disputed.
- S3 disputed for valid clocks.
- S5 confirmed (F2).
- S6 confirmed (F3, F4).

## Why each agent missed it, or caught it

The lens reports were not kept, so no per-lens account survives. F1 to F3 sit in safeguards this change
added, and none of them was run against the bug it guards; F4 sits in a comment that described the engine
clock without reading its origin.

## Root-cause patterns

- **A safeguard never turned on the bug it was written for:** F2, F3.
- **A fallback used as a decision input:** F1.
- **`PartyBase.MainParty` null checks:** found three times in one week (Batch B C08, `5f80f95c9`,
  `91f369984`; the Messenger copy dates from `03a41b624`, 2026-05-06). They are now gated by
  `MainPartyNullCheckScanTests`.

## Rejected

E43, the per-culture `ConditionalWeakTable` cache: on .NET Framework, `TryGetValue` takes `lock (_lock)` on
every read, and the reads run on the parallel party tick (`rca-eye-of-sauron-2026-10-14.md` E43).

## Follow-ups

#793 stays open: TAOM's other daily listeners are not audited, and the repeat has not been seen in game.

## Lessons recorded

- `docs/reviews/lessons/state-lifecycle-save.md`: after a load, the daily tick can repeat the save's own day.
- `docs/reviews/lessons/adapters-taleworlds-api.md`: never null-check `PartyBase.MainParty`.
- `docs/reviews/lessons/gamemodels-services.md`: F1 recorded as a recurrence of the fallback lesson.
- `docs/reviews/lessons/testing-qa.md`: F2 and F3 recorded as recurrences of the guard-RED lesson.
