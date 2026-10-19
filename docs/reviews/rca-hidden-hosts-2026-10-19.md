# RCA: Hidden Hosts phase 1 (deep-review, 2026-10-18 to 2026-10-19)

## Top-line

`/deep-review` of Hidden Hosts phase 1 (#846, nameplate troop counts masked by distance) ran seven
lenses in two waves and found no CRITICAL or HIGH defect, 6 MEDIUM findings and about 20 LOW ones,
several reported by more than one lens. The convergence pass on the fixes found one more MEDIUM and
four LOW (rows 13 and 14). Every finding was fixed before the first commit, three of them
through behaviour changes Mike approved (the mask design, the popup text, a fault budget). The
largest change came from the design lens: the first build kept a mask inside vanilla's nameplate bind
fields between frames and tracked vanilla's writes by comparing field text. That one choice produced
a state machine, a value collision with vanilla's own "?" and a mask that a fault could leave on
screen. It was replaced by a prefix that lays the mask for the one call that copies the fields and a
finalizer that restores them.

The new tests were written alongside the code rather than run red first, so they were proven by
mutation instead: removing the name restore in `VanillaBinds.Restore` and flipping the encyclopedia
prefix's polarity failed 4 tests (2 each); both files were restored byte-identical and the suite went
green again (227/227 filtered).

## Findings and root causes

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The one-time popup explained a `~` remembered-count symbol that only phase 3 draws, and said a horizon host cannot be named although its hover tooltip names it until phase 2. The popup's flag rides the save, so a player who read it could never get a corrected one. | Player text ahead of the build | The plan and the builder's brief asked for the phase 3 symbol "already", to avoid a second popup later. Nobody weighed that a save-flagged popup is shown once and cannot be corrected. | Popup now describes phase 1 only; phase 3 gets its own key and flag. Lesson in `lessons/localization-ui.md`. |
| 2 | MED | The mask-to-field mapping and the toggle-off lift sat in the patch, untested; every hook test passed a null instance, so it proved only the null guard. | Vacuous hook tests | The builder judged that `PartyNameplateVM` and `MobilePartyVisual` "cannot be built in a test host" and wrote the null-path tests as the best available. The claim was never tried; 21 TAOM test files already build engine objects with `FormatterServices.GetUninitializedObject`. | Hook tests now run on uninitialized instances and cover every branch. Lesson in `lessons/testing-qa.md`. |
| 3 | MED | The encyclopedia block was documented and tested as a map CLICK. The engine calls `OnOpenEncyclopedia` only on game key 67 (`MapGoToEncylopedia`, unbound on keyboard by default) while a party is hovered; a click engages or escorts. The planned in-game check could not reach the block. | Trigger described from the spec, not the caller | The spec described the reference behaviour as "clicking them does not open the encyclopedia", and the plan and brief copied that. Signature research read the patched method but not its caller (`MapScreen.cs:1649-1651`). | Docs, comments, the co-op classification reason and the in-game check corrected. Lesson in `lessons/adapters-taleworlds-api.md`. |
| 4 | MED | `PartyIntelAdapter` (a process singleton) cached the main party's `IFaction` for 0.5 s. With no session reset it kept the finished campaign's kingdom or clan reachable through the main menu and the next load, or for the rest of the process with the toggle off. The cache also saved nothing: `MapFaction` is two property reads. | Retention by a singleton (REPEAT) | The brief and the builder reasoned about staleness ("self-heals within half a second") and the plan said "hold only strings and numbers" for the service but not the adapter. The existing lesson ("ask what it keeps alive until it does") was not applied to a time-bounded cache. | Field deleted; `PartyIntelAdapterTests` now fails if any instance field of the adapter is a TaleWorlds reference type. Lesson extended in `lessons/state-lifecycle-save.md`. |
| 5 | MED | A fault that repeats would throw once per plate per frame for the rest of the process; only the log line was deduplicated. | No stop on a per-frame fault | The fault handling copied the "log once" half of the repo pattern and not the "stand down" half used by per-tick hooks (`BattleCorpseMissionBehavior`, `MissionPerfHeartbeatBehavior`). | `HiddenHostsCalls.FaultBudget` (32): the hooks stop and vanilla text shows. One-off; the precedent exists. |
| 6 | MED (design) | The mask lived in vanilla's bind fields between frames, tracked by `MaskedBindField` comparing field text with the last mask. It produced findings 7 and 8 and per-frame work for every plate even with the toggle off. | State tracking where save and restore suffices | The plan chose the remember-and-restore approach before the design lens asked whether the mask needed to outlive the call that reads it. TAOM already had the prefix `__state` plus finalizer shape (Patch88, Patch92). | Replaced by `VanillaBinds` (prefix lays, finalizer restores). Lesson in `lessons/harmony-il.md`. |
| 7 | LOW (latent) | A one-digit unknown count masked to "?", the same text vanilla writes for an `IsInfoHidden` party, which broke `MaskedBindField`'s rule that a mask never equals a vanilla value. Reachable only through a pooled plate reused for an info-hidden party; no loaded code sets the flag today. | Sentinel collision | Follows from 6. | Gone with 6. |
| 8 | LOW | A fault on a plate already masked left the mask on screen, while three texts promised vanilla's text. | Follows from 6 | Follows from 6. | Gone with 6: the finalizer always restores. |
| 9 | LOW | Docs: the horizon is not "the last tenth of the range" for besiegers and raiders of a settlement the player can see; vanilla shows the name and wounded text only while the indicators key (Left Alt) is held; `_fullNameBind` is also rewritten on forced refreshes and plate reuse; the doc put the `~` symbol in phase 2 instead of 3. | Engine behaviour stated from the spec | Same as 3. | Docs corrected. |
| 10 | LOW | Exemption and same-realm policy lived in the adapter, untested. | Policy in an adapter | The brief put the exemptions in the snapshot builder. | Snapshot carries raw facts; the service composes them, one test per state. |
| 11 | LOW | Nothing pinned the engine fact the mask depends on (`RefreshBinding` copies the three fields to `Count`, `FullName`, `ExtraInfoText`). | Unpinned engine assumption | The binding tests proved the fields exist, not that they are read. | `HiddenHostsBindingTests` IL pin; it also proves `RefreshBinding` is the only reader in its assembly. |
| 12 | LOW | Smaller gaps: an untested `IsFinite(ratio)` gate; an `IsMapFree` throw logged every tick; four comments that did not match the code; no PatchShield paragraph in the registry; the feature doc missed thresholds, How-to, Performance, the owed translation and the deviations from #846; popup glyphs open to translation; no section comment in the strings file; a dead check, a duplicate field (`HasText`) and an extra dependency in the behavior. | Completeness | Each is a one-off. | All fixed; glyphs now `{MASK_EXAMPLE}` and `{HORIZON}` filled from `HiddenHostsMath`. |

| 13 | MED (convergence pass) | The new prefix and finalizer pair was tested by calling both methods by reflection and passing `__state` between them by hand; no test ran `RefreshBinding` through real Harmony, which `.claude/rules/harmony-patches.md` requires. Moving the finalizer to another class (Harmony keeps one `__state` per declaring type) would have passed every test and brought the stuck mask back. | Pair tested off-engine | The fix list for finding 6 asked for unit tests of `VanillaBinds` and the hooks, and did not repeat the harmony-patches rule; the builder covered the logic, not the Harmony wiring. | Real-Harmony test class covering the masked call, a stripped prefix, a throwing later finalizer and a skipping bool prefix. One-off: the rule and lesson already say it; the fix brief now quotes the rule it implements. |
| 14 | LOW (convergence pass) | The popup could still fire after the fault budget stopped the hooks, explaining a mask that is off; a popup fault was documented as "for the session" though its flag is saved; `ReportFault`'s return value was read only by tests; the Performance section understated the per-plate cost. | Follow-on from 5 and 12 | Introduced by the first round of fixes. | All fixed. |

Not applied, with reasons: a shared "map is free for a popup" gate (it would edit HunterParty and
WarChronicle code outside this change; done as #929); mirroring the engine's siege and raid visibility
rule in the horizon test (display only, and the party really is far away; documented instead);
reference comparison in `MaskedBindField` and moving the visibility check above the toggle (both made
moot by 6).

## Root-cause pattern: describing engine behaviour from a description

Findings 3 and 9 share a cause. The spec described what the player sees in another implementation,
and the plan carried those descriptions into TAOM's docs and tests as engine facts: "a click opens
the encyclopedia", "the horizon is the last tenth of the range". The signature research the batch
rules require checks that the patched member exists and what it does; it does not check what calls
it or under which input. Lens 2 found both by reading the callers.

## Why each lens caught or missed what it did

- **Standards (1)** found the untested patch logic (2) and the singleton without a reset (4, as LOW).
- **Engine compatibility (2)** found the hotkey (3) and the documentation errors in 9 by reading
  `MapScreen` and the visibility model, and confirmed all 36 engine members.
- **Efficiency (3)** found the retention as MEDIUM by applying `state-lifecycle-save.md` line 826 and
  the missing fault stop (5).
- **Completeness (4)** found that the hook tests were vacuous and that `GetUninitializedObject`
  works, which overturned the builder's premise.
- **Data flow (5)** traced the "?" collision (7) and the stuck mask (8) end to end through plate pooling.
- **Design (6)** found the root of 6, 7 and 8 and proposed the finalizer shape.
- **XML (7)** found the popup text problem (1) and the cost of fixing it after the paid translation:
  a seed row that keeps old English is never re-queued.

Finding 1 was reported by four lenses; finding 4 by three.

## Lessons appended

- `lessons/harmony-il.md`: show a different value for one engine call by saving and restoring around it.
- `lessons/testing-qa.md`: a hook test that passes a null instance proves only the null guard.
- `lessons/state-lifecycle-save.md`: a time-bounded cache of an engine object still pins the campaign.
- `lessons/localization-ui.md`: one-time player text describes only what ships in the same build.
- `lessons/adapters-taleworlds-api.md`: read the caller of a patched handler before describing its trigger.
