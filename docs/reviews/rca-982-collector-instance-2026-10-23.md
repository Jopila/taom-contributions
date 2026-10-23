# RCA: #982 crash-bundle collector and inherited `Instance`, 2026-10-23

**Summary.** #982 reported that crash bundles skip third-party MCM settings classes, because
`McmSettingsCollector.ResolveInstance` looks `Instance` up without `FlattenHierarchy`. The first fix walked the
base types and invoked the inherited `GlobalSettings<T>.Instance`. The tier 2 review (Codex `xhigh`, lens 5 Data
flow) found that this ran other mods' code inside crash handling and copied their settings into the uploaded
bundle. Mike chose the long-term option: crash bundles list only TAOM's own settings classes, by design. The walk
was reverted, and the code, tests and docs now say why. The Codex fix review then showed the revert enforced the
decision with a proxy (`DeclaredOnly`), not with ownership; the collector now reads only TAOM's own assembly
(finding 8).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | Invoking the inherited `GlobalSettings<T>.Instance` runs `new T()` on a cold MCM cache before any provider check (MCMv5 5.12.3). Inside `Collect()`, which `CrashReportService.cs:231` calls synchronously before the bundle is written, a third-party constructor that waits could block the whole bundle. Codex reproduced the wait | A diagnostic path executes code it does not own | The issue framed the defect as reflection correctness ("the lookup misses an inherited property"), and its proposed fix, written in this session, answered that question only. Nobody asked what code the read would run | Reverted: `ResolveInstance` reads only a class's own `Instance` (`DeclaredOnly`). Test `ResolveInstance_ClassWithoutItsOwnInstance_IsSkipped_AndRunsNoThirdPartyCode` proves no constructor runs. Lesson in `lessons/misc.md` |
| 2 | MEDIUM | Third-party free-text settings would go verbatim into `report.txt`, `report.json` and `taom_debug.log`, which players upload; the environment collector filters, this one did not | Unfiltered third-party data in a public artifact | Same as 1: the fix was judged on what it finds, not on what it publishes | Resolved by the same decision; the reason is in the collector header and `crash-report.md` |
| 3 | MEDIUM | The walk took the first `Instance` on the way up without checking its type, so an unrelated inherited `Instance` or an open generic candidate gave a bogus provider with failed rows | Unvalidated reflection result | The tests covered the two intended shapes only | Resolved by removing the walk |
| 4 | LOW | `crash-report.md` claimed "every MCM settings class (TAOM and third-party)" | Doc overclaim | Written from the intent, not from the discovery code | Row rewritten: TAOM's four classes, third-party skipped by design, with the reason |
| 5 | LOW | Every provider listed MCM's `DiscoveryType` metadata as a setting | Missing skip entry | Never noticed while the section listed nothing | `DiscoveryType` added to `SkipPropertyNames`; test `ExtractSettingEntries_DoesNotListMcmDiscoveryType` |
| 6 | LOW | The collector's header comment described a `*Settings` name filter the code does not apply and a single lookup | Stale comment | Comment predates the current discovery code | Rewritten to match the code |
| 7 | LOW | `server-settings.md` said third-party classes are listed since #982 | Doc follows a reverted change | Written with the first fix | Corrected, with `coop-interop.md` and the B8 RCA follow-up |
| 8 | HIGH | `DeclaredOnly` alone did not enforce TAOM-only collection: a foreign class that declares its own `Instance` still passed, so its static constructor, constructor, `Instance` getter, `Id`, `DisplayName` and property getters ran inside crash handling and its values were published. Found by the Codex fix review and reproduced with a synthetic foreign assembly | A proxy check standing in for the decision | The first rework tested the shape TAOM's classes have ("declares its own `Instance`") instead of the rule the maintainer chose ("only TAOM's own classes") | Fixed: `Collect()` skips every assembly except `typeof(McmSettingsCollector).Assembly` before enumerating types; `DeclaredOnly` stays as the second guard. Test `Collect_SkipsAForeignClassThatDeclaresItsOwnInstance_AndRunsNoCode` (watched fail first), with `Collect_ListsTaomSettings` as the positive case |
| 9 | MEDIUM | The docs claimed that no third-party settings code runs inside crash handling. TAOM's own `Instance` getters still ask MCM (`GlobalSettings<T>.Instance`), and when no TAOM settings are registered, MCM 5.12.3's `DefaultSettingsProvider.GetSettings` asks the external settings providers other mods registered. Codex reproduced one such call. The path predates #982 (`CrashReportService` reads `CrashReportSettings.Instance` too) | An absolute claim wider than the guarantee built | The claim was written from the gate, not from a trace of every call the remaining reads make | Docs now say no third-party settings class is read and name the provider path as a known limitation. Closing it needs settings captured before the crash (a snapshot design); not built |
| 10 | LOW | `crash-report.md` still listed the collector's discovery as untested after `Collect()` tests landed | Stale coverage inventory | Not re-read after the tests changed | Paragraph rewritten |

## Root-cause pattern

Findings 1 to 3 and 8 share one cause: **a fix to a diagnostic path was judged by what it would find, not by what it
would execute and publish.** A crash handler runs when the process is least able to tolerate surprises: on a
worker thread, after a native fault, with locks held. Reading another mod's static `Instance` is a call into that
mod, and MCM's getter constructs the settings object. The bundle is also a public artifact, so every value it
gains is a value a player uploads. Finding 8 adds that a decision about ownership must be enforced by an ownership
check (the assembly), not by a property of TAOM's classes that other mods can also have.

## How the review lenses found these

- **Codex (`xhigh`)** found 1 by decompiling MCM's getter and reproducing a blocked `Collect()`, found 3 with two
  probe shapes, and found the doc overclaim in 4.
- **Data flow (5)** found 2 by tracing values to the renderers and comparing with the environment collector, and
  found 5, 6 and 7. It also traced 1's precondition (the getter runs off the main thread) but rated it LOW, because
  it did not consider the cold-cache constructor.
- Lenses 1, 2, 3, 4 and 6: SKIPPED BY TIER.

## Feedback to codify

- **Lesson** (`lessons/misc.md`): before a crash or diagnostic path reads through another mod's API, list the code
  that read runs and the data it publishes; read only what TAOM owns, or snapshot it at a normal moment.
