# RCA: Voice Accents (#821, deep review 2026-10-18)

## Top-line

The working-tree deep review of Voice Accents (six lenses: standards, engine compatibility, efficiency,
completeness, data flow, design) found 1 HIGH, 6 MEDIUM and 15 LOW findings before any commit. Two of
them made the first accent map wrong in game while every test passed: the engine pairs a culture's accent
with a text variant through a second culture switch, and two of the accents the map used have almost no
recorded lines. Both are data facts no unit test could see, and both now have a shipped-data gate. One
more (Saruman's race row) was a decision the build brief narrowed, caught before the review. All
findings were fixed in the same session; the in-game check is owed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH | Non-tribal cultures (Dunland, Erebor, Umbar, the far south) mapped to tribal accents. `ConversationTagHelper.TribalVoiceGroup` (true only for sturgia, aserai, khuzait, battania, vlandia, nord, vakken) picks the text variant, and vanilla recorded the high and low register variants almost only in `imperial_high` and `imperial_low`. Dunland would fall from 63 voiced variants to 3, a regression from vanilla; dwarves and Umbar from 112 to 38 | Engine data pairing | Research read `DefaultVoiceOverModel` only. The accent looked like the whole decision, so nobody searched for another engine switch keyed on the same culture id | Map non-tribal cultures to imperial (Mike's decision). Gate: `ShippedVoiceAccentsConfigTests` refuses a tribal accent on a non-tribal culture or race row, and imperial on a tribal culture. Lesson in `lessons/gamemodels-services.md` |
| F2 | MEDIUM | Orc cultures and two raider cultures on `mountain_bandits` (5 of 226 SandBox lines recorded) and `mirkwood_stalkers` on `forest_bandits` (0). The doc promised an orc "mountain-bandit voice" and an in-game check that correct code would fail | Engine data coverage | The map was chosen by accent name. Nobody counted the recordings behind each accent | Orcs and `gundabad_raiders` silent (Mike's decision), the other raiders `looters`. Gate: no row may use `mountain_bandits` or `forest_bandits`. Same lesson |
| F3 | MEDIUM | The build brief made race rows silent-only, so Saruman (culture isengard) would have spoken with his culture's accent, against Mike's "imperial" decision and the issue text | Brief narrowed a decision | The brief solved a mechanism question ("`GetAccentClass` gets no race") by dropping the case instead of asking whether a route existed | A speaker scope carries the race into `GetAccentClass` (single synchronous caller, verified). Process: compare a brief against the approved decision list before sending it |
| F4 | MEDIUM | `IsSilent` let a silent or unmapped culture beat a race row with an accent. Latent at first; real once isengard became silent | Rule written twice | The race-first rule lived in two methods and only one honoured it | Race row decides first in one place (`SoundPath`), pinned by `SoundPath_AccentRaceWithSilentCulture_NotSilent` |
| F5 | MEDIUM | The model resolved race names from `FaceGen.GetRaceNames()` itself: a branch in an untested entry point, no null guard, and a clone of the race array per voiced line. `IRaceManager` already does it | Reuse missed | The brief named `IFaceGenAdapter`; nobody searched for an existing race-name service | The service takes `IRaceManager` and validates the id first (`GetRaceNameFromId` answers "human" for an unknown id) |
| F6 | MEDIUM | Issue #821 described the replaced map | Docs drift | The design changed during review | Issue body corrected, with a comment recording why |
| F7 | MEDIUM | No wiring test pinned the module's campaign target, slot and dependencies | Test gap | Generic module tests pass for any target | `VoiceAccentsWiringTests` |
| F8 | LOW | `ResolveAccent` turned a `silent` row into "", the value that reopens the random-voice regex | Sentinel reuse | Unreachable today, so nothing failed | A silent row passes through as the class `silent`, which matches no path |
| F9 | LOW | Two keys that normalize to one id overwrote silently | Validation gap | The rule set listed empty and unknown values only | Warn and keep the first, tested |
| F10 | LOW | The wave 1 fix added a comment inside a method body (ADR-009) | Standard | Fix written in a hurry | Named helper with a summary |
| F11 | LOW | Smaller gaps: the tribal gate checked one direction, an untested empty-file branch, no summary-warning test, undocumented duplicate rule, wrong issue title in the doc, unsourced test id list, model outside `Models/`, test names after a private method, a test path built from `..\..\..\..`, the v1.5.5 impact doc and hero-race doc still describing the old behaviour, authoring checklists without the new row | Completeness | Normal review yield | Fixed in the same change |

## Root-cause pattern: a mapping to engine data chosen by label

F1 and F2 share one cause. The plan mapped TAOM cultures to vanilla accents by what each accent is
called, and checked only the model that returns the accent. Two facts outside that model decide whether
a line plays: which text variant the engine picks for the culture (`TribalVoiceGroup`), and whether
vanilla recorded that variant in the chosen accent. Neither is visible in C#, and every unit test passed
with both wrong. The lenses that caught them (engine compatibility, data flow) did so by simulating the
engine's regex tiers over `sandbox_voiced_lines_en.xml`.

The rule: when TAOM maps its ids onto a vanilla data set, find every engine switch keyed on the same id,
and measure the vanilla data each mapped value reaches. Then pin the measured constraint with a
shipped-data test, because the next row someone adds will not repeat the measurement.

## Why each lens caught or missed these

- **Engine compatibility (2):** caught F2 by counting recordings per accent, and verified every binding.
  It missed F1's text-variant switch because it checked the voice model's own callers.
- **Data flow (5):** caught F1 by tracing the first lord line through `FindMatchingTextOrNull` to the
  recorded variants; the only lens that followed the data past the model.
- **Standards (1), Efficiency (3):** caught F5 from two sides (rule 4 and the per-call clone). Neither
  could see data coverage, which is outside their domain.
- **Completeness (4), Design (6):** ran on the fixed tree and found F6, F7, F11 and the duplicated
  race rule behind F4.
- **Orchestrator:** F3 was caught by re-reading the brief against the decision before review; it should
  have been caught before the brief was sent.

## Feedback memories to codify

None. The F1/F2 rule is a lesson (below) and a gate; F3 is covered by the existing "never drop part of
the request silently" rule in `.claude/rules/think-before-coding.md`.
