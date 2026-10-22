# RCA: Fire and Tribute slice 2 (tribute envoy) deep review, #870 (2026-10-21)

**Summary.** Slice 2 adds the tribute envoy: a popup that asks the player for denars to spare their clan's villages from a raiding host, with haggle, delay, refuse and seize. Seven `/deep-review` lenses read the uncommitted tree before its first commit. They found no CRITICAL or HIGH defect, four MEDIUM and a set of LOW findings. The orchestrator re-read the load-bearing ones against the code and the v1.5.5 decompile. All were fixed before the first commit.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Paying only recorded the spared clan; an engage order on the main party or a raid order on a spared village, given earlier in the same hour, kept running until the next hourly step. | State change without its effect | The payment was written as data that the next step reads, not as an event that must act now. Vanilla's own bribe sets `SetDoNotAttackMainParty` at once (`SafePassageBarterable`). | Payment re-steers the host at once and sets the engine's do-not-attack span; tests for an engage order given in the same hour. Lesson below. |
| 2 | MEDIUM | The seize texts promised "the next host of its people", but the grudge is held per side (five Shadow hosts). | Text promises more than the code | Written from the lore, not from the data structure. | Texts say "its side"; all 14 copies changed together. |
| 3 | MEDIUM | The console command `taom.fat_envoy_now` had no verb, against the documented `<verb>_<object>` rule that no test enforces. | Naming | The rule lives in the console doc only. | Renamed `taom.set_envoy_due`. |
| 4 | MEDIUM | While an envoy waited, the service walked every village in the world each hour to count the clan's; the engine keeps the clan's fiefs. | Hot path | The count reused the raid picker's village list. | Count from `Clan.Fiefs` and `Town.Villages`; an IL pin forbids the world scan. |
| 5 | MEDIUM | A guard that keeps an unscheduled host (a slice-1 save, a host that rode while the player was elsewhere) from producing an envoy had no test. | Test coverage | Tests asserted the schedule, not the hourly tick. | Test added, with five other guard tests. |
| 6 | LOW | A host being sent away (switch off, still fighting) could still ask for tribute; a paid host could fight the player inside an AI-led army; an envoy could arrive while its host raided the clan's village. | Gate gaps | Each gate was written for the common case. | `SideEnabled`, a "leads the main party" flag, and a map-event blocker. |
| 7 | LOW | A popup-open latch never cleared if the popup was closed with no answer (`HideInquiry`), silencing every envoy for the session; the docs claimed exactly one callback always runs. | Redundant state | The engine's own "an inquiry is active" check already prevents stacking. | Latch and session counter removed; a stale click is ignored by record identity. |
| 8 | LOW | `{HOST}` mid-sentence ("An Envoy of The Host of Gundabad"), a hard-coded "week" for a configurable delay, "1 days", and "the villages (1 in all) lie". | Player text | Written without substituting the real names and values. | Reworded with the engine's plural form; all copies changed together. |

## Root-cause pattern

Findings 1 and 6 share a cause: a player decision (pay, switch off) was stored for the next hourly step to read, instead of changing what the host is doing now. Findings 2 and 8 share another: player text written from intent instead of from the data and the real substituted values.

## Lessons

- `docs/reviews/lessons/campaign-mechanics.md`: a player's deal with a party acts at once.
