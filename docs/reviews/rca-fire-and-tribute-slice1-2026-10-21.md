# RCA: Fire and Tribute slice 1 deep review, #870 (2026-10-21)

**Summary.** Fire and Tribute slice 1 sends raiding hosts of the outlaw clans against the strongest enemy realm once the War of the Ring is in Full War. Seven `/deep-review` lenses read the uncommitted tree on `feat/war-armies` before its first commit. They found no CRITICAL defect, two HIGH, five MEDIUM and a set of LOW findings. The orchestrator re-read each against the code, the live map data or the v1.5.5 decompile before entering it here. All were fixed in the working tree before the first commit. The maintainer decided two points: side names parse through the shared `EnumNames.TryParse`, and ADR-003 now allows a service split into partial files by concern.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The orchestrator moved the Blacklock host (`erebor_warriors`, a dwarf clan "fallen into shadow") to the Evil side in the JSON only; the MCM hints and the settings comments still listed it under the Free switch. | Half-done data change | The move was made after the build, in one file, and the texts that name the sides were not searched. | Hints and comments fixed. Lesson below. |
| 2 | HIGH | The Blacklock host's anchors were Erebor's own halls, inside the Free realm it raids, while all ten of its hideouts sit beside Rhûn fiefs. | Data from the wrong source | Anchors were picked from the clan's id (`erebor_*`), not from where TAOM places the clan. | Re-anchored in Rhûn; a LiveInstall test now checks that each Free or Evil host's anchors belong to realms of its own side. |
| 3 | MEDIUM | An army leader was judged on its own roster for "weaker than the host", but engaging it pulls in the whole army; a pinned host never flees, so it would be destroyed. | Engine semantics | The strength read copied the lone-party case; vanilla's own AI reads `Army.EstimatedStrength` for a leader. | Fixed with an IL pin. Lesson below. |
| 4 | MEDIUM | The singleton adapter cached the campaign's villages and the `Campaign` itself, which kept a finished campaign's whole object graph alive into the next one. | Singleton holds engine objects | A per-campaign cache was keyed on the campaign object, the trap `state-lifecycle-save.md` already records. | Cache deleted (the engine's `Village.All` is free to iterate); an IL test forbids engine-object fields on the adapter. Repeat offender. |
| 5 | MEDIUM | The Gondor host was anchored on the west coast, while its camps are on the Ithilien front, so it marched across the map to go home. | Data from the wrong source | Same as 2. | Re-anchored in Anórien and Ithilien. |
| 6 | MEDIUM | Four host names were plural ("The Corsairs of Umbar"), so every singular notice read wrong, and three repeated their clan's ordinary bandit-party name. | Player text | The names were written without the messages they fill. | Singular collective names; a data test pins them and their registration. |
| 7 | MEDIUM | Switching a side off while its host fought released the pin and dropped the record with no cooldown, so a full-size host roamed untracked and toggling could stack hosts. | Lifecycle | The retire path was written for the common case of a host out of battle. | Retire keeps the record and retries each hour; co-op release starts the cooldown; tests for off-then-on while fighting. |
| 8 | LOW | The raidable-village rule was written twice (adapter and service) and had already drifted; the selection was untested adapter code. | Logic in the adapter | The adapter was allowed to filter for convenience. | Rule and selection moved into `FireAndTributeMath` with unit tests. |
| 9 | LOW | Three private parsers for one enum, against the standing `EnumNames` lesson (its fourth recurrence). | Repeat offender | The lesson lives in a category file the builder did not read. | `EnumNames.TryParse`; a "Recurred" line added to the lesson. |
| 10 | LOW | Copied helpers (out-of-sight spawn, nearest town), dead snapshot fields, an unreachable host cap, a grudge path nothing could set in this slice, compiled regexes used a few times, wrong comments about the engine's engage order and spawn point, and doc gaps (no budget, the old spawn message, bandit scaling). | Simplicity and evidence | Written ahead of need or from memory. | Reused or deleted; comments and doc corrected from the decompile. |
| 11 | LOW | Live-map data tests had no `LiveInstall` tag and a hard-coded game path; no test checked that the JSON host names were registered. | Test hygiene | The sibling live-data tests were not used as the template. | Tagged, `BANNERLORD_GAME_DIR` fallback, a registration test. |

## Root-cause pattern

Findings 1, 2 and 5 share one cause: data about a clan was taken from its id or from a single file, not from what TAOM's own data says about it (its hideouts, its description, every text that names its side). Findings 3 and 4 are engine semantics already recorded elsewhere in the repo, missed because the builder followed the plan rather than the vanilla code it imitates.

## Lessons

- `docs/reviews/lessons/data-content-cultures.md`: place and side an outlaw clan from TAOM's data, and move every text with it.
- `docs/reviews/lessons/adapters-taleworlds-api.md`: judge a party's fighting strength as the engine does for armies.
