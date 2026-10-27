# RCA: battle-load triage chose its silence anchor from the whole log, 2026-10-26

**Summary.** For #1025, `tools/triage_battle_load.py` learned to measure the render-wait silence to the last line of
the log when no watchdog line exists, so a player log that ended 117 s after its last `WaitingForRender` sample no
longer read as a draining shader cache. That change shipped in `b9eabd712`. The deep review of it found that the
anchor was still chosen by asking whether the log held any watchdog line at all. The watchdog fires once per loading
window, so the only watchdog line can belong to an earlier load, or to a long scene load earlier in the same window.
A line written before the last sample made the gap negative, and the verdict read DRAINING again. The fix uses the
watchdog line only when it is not older than the last sample, and otherwise the newest stamped line of any kind.

Review: risk tier 2 (a `tools/` script and its tests), one wave: the Data flow and Tooling correctness lenses. Codex
did not run: no paid dispatch was authorized in this session. The UIExtenderEx change in the same commit had its own
tier 1 review before the commit (`rca-uiextender-ongamestart-2026-10-26.md`) and was not reviewed again. One MEDIUM,
four LOW and one NIT were confirmed against the code and fixed; two pre-existing LOW defects are follow-ups.

Sources: the two lens reports of 2026-10-26; player log `taom_debug_2026-10-25_17-36-25.log`; issue #1025.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | A watchdog line older than the last sample (an earlier load's, or a scene-load fire earlier in the same window) was taken as the anchor; the negative gap gave `silent_s` None and the DRAINING verdict | Anchor scope | The fix was tested on the one log shape in hand (no watchdog line). Nobody asked whether a watchdog line in the log belongs to the wait being judged; `BattleLoadStallWatchdog` latches once per window (`_firedForCurrentWindow`), which the author did not read | `_render_silence` compares stamps and uses the watchdog line only when it is not older than the last sample; `test_watchdog_from_earlier_in_the_same_load_is_not_the_anchor`, `test_watchdog_from_an_earlier_mission_is_not_the_anchor`; lesson entry rewritten |
| 2 | LOW | No test pinned the "before the watchdog fired" label, and none had a last line other than `[MemSample]`, so two mutants survived the whole suite | Test strength | The new tests were written to make the change pass, not to fail its mutants | Assertion on `293s before the watchdog fired`; `test_silence_runs_to_the_last_stamped_line_of_any_kind`; a mutation run (label fixed, any watchdog wins, `[MemSample]`-only stamp) now fails each mutant |
| 3 | LOW | The module docstring (the `--help` text), two comments, the "Why the two samples mislead" note, Next steps and `battle-load-diagnostics.md:721` still described only the watchdog anchor; the note told a player with no watchdog line that the watchdog fires on a count unchanged for 60 s | Text drift | Only the edited function's docstring was updated; nobody grepped for the rule's other statements | Reworded; the new no-watchdog test asserts the report has no "watchdog fired" and no "two samples" |
| 4 | LOW | The lesson stated as fact that the player closed the game (#1025 marks it unverified) and called the end of the log an anchor that always exists (the memory sampler can be off) | Overstated evidence | Written from the conclusion, not checked line by line against the issue | Reworded with the condition and the uncertainty |
| 5 | LOW | `test_no_watchdog_line_keeps_the_draining_message` named the contract the change reversed; it passed only because its log ends on the sample | Stale name | The existing test still passed, so nothing prompted a read of its name | Renamed `test_no_watchdog_and_no_later_line_keeps_the_draining_message` |
| 6 | NIT | Two helpers derived one decision; `_render_silence_s` copied `_seconds_between`; a default argument nobody used; the parse loop matched each line's stamp up to three times | Duplication | First cut added beside existing code instead of reusing it | One helper returns `(seconds, anchor)`; `_seconds_between` reused; parameters required; the stamp is matched once per line |

**FOLLOW-UP, no issue: awaiting Mike's word to file (issue creation is his).** Pre-existing, outside the changed lines:
`format_report` prints the session's last watchdog line and the JSON's `watchdog_fired` reads the same session-level
value, so a stale watchdog line from an earlier load appears under a later load's verdict, with its own advice.

**FOLLOW-UP, no issue: awaiting Mike's word to file.** Pre-existing: the verdict's silence arm needs the terminal
sample's `shaders` above 0, while the timing line's silence branch needs only some sample with `shaders=`. A series
ending at 0 gets "a real wedge" in the verdict and "cannot separate a slow compile from a wedge" in the timing line.

## Root-cause pattern

Findings 1 and 3 share one cause: the change was judged against the single input that prompted it. The fix made that
log read correctly and stopped. The same function failed the same way on 2026-10-18 (the two-sample "draining"
verdict), so this is the second incomplete fix of the render-wait silence rule. Both times the defect was an
assumption about which lines a log holds, not an arithmetic error.

## Why each lens found or missed these

- **Data flow** found 1, 3, 4 and 5, and the two follow-ups: it traced `Timeline.watchdog` to its single assignment
  and asked where else a watchdog line can come from, then followed the new rule into every text that states it.
- **Tooling correctness** found 1, 2, 3 and 6: it built the two stale-watchdog shapes from the C# latch and the
  setting's default, and ran mutants against the whole suite.
- Both lenses found finding 1 independently, with the same fix (compare stamps, not event order, because crash-report
  echo lines are parsed after the watchdog line).

## Lesson

A silence measured against an anchor needs the anchor to belong to the window being judged: compare stamps, and treat
an older anchor as absent (`docs/reviews/lessons/state-lifecycle-save.md`, "The end of the log is a second silence
anchor when no watchdog line exists"). No new memory: the lesson entry is the durable record.
