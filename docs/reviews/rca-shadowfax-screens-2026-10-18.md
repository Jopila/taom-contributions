# RCA: Shadowfax Screens (deep review, 2026-10-18)

## Top-line

`/deep-review` of Shadowfax Screens (#850; a UIExtenderEx `CustomPatch<XmlDocument>` that zeroes the
VisualDefinition durations of nine vanilla movies, plus zero durations in TAOM's party prefabs) ran
seven lenses in two waves. No CRITICAL or HIGH finding. Two MEDIUM and eight LOW findings were
confirmed and fixed in the same session; the maintainer approved two behaviour changes (the party
header press squish and the career screen slides now also instant). One finding repeats the class
recorded in `rca-binding-gate-no-silent-skips-2026-09-24.md`: a binding gate that skips a shape it
was never written for.

Every finding below was confirmed against the installed v1.5.5 DLLs or the files themselves; the
orchestrator re-read `Widget.UpdateVisualDefinitions` (`TaleWorlds.GauntletUI.BaseTypes.Widget.cs`
lines 1771 to 1800) and `PrefabExtensionBindingTests.cs` lines 72 to 79 before fixing.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MEDIUM | `PrefabExtensionBindingTests` skipped every `[PrefabExtension]` without an XPath before it looked the movie up, so none of the nine movie names was ever checked against the installed game. UIExtenderEx files a whole-document patch under the movie name and does nothing, with no error, when no prefab of that name loads. | Gate skip on a new shape (repeat class) | The skip predates this change and had never fired: this was TAOM's first null-XPath extension. The author wrote a hand-copied name list instead of reading the gate for its handling of the new shape. | Gate now resolves the movie, with exact file-name case, before the XPath skip; proven by a mutant (`ClanScreen` to `Clanscreen` fails). Lesson in `lessons/testing-qa.md`. |
| 2 | MEDIUM | Code comment, feature doc and #850 said the character screen's top extension "waits 0.7 s and then slides for 0.3 s, about 1 s", and that each open costs "up to a second before the player can act". The engine counts `DelayOnBegin` inside `TransitionDuration` and snaps at the duration, so 0.7 over 0.3 can never slide; no widget uses that definition; transitions hold no mouse input (two widgets hold gamepad focus for 0.15 and 0.2 s). The real worst case is 0.45 s. | Engine claim from attribute values, not from the consumer | The timing came from reading XML attribute values and adding them; nobody read the method that interprets them. | Corrected text. Lesson in `lessons/localization-ui.md`. |
| 3 | LOW | Rewriter comment said "it never adds an attribute"; the code adds `TransitionDuration` where it is missing (needed: Gauntlet's default is 0.2 s), and no test covered that branch. | Comment against code; one-sided guard test | Fixtures all carried the attribute, so the add branch never ran. | `MakeInstant_DefinitionWithoutTransitionDuration_AddsZero`; comment states the reason. `tests.md` Skip-Guard Exhaustion already covers it. |
| 4 | LOW | `InstantScreensPatch.cs` broke the `UI/<Feature>Prefab.cs` convention, so the `gui-ui.md` path rule (and its PrefabExtension safety section) would not load for it, while the `*Patch.cs` Harmony rule would. | Naming convention | Named after what it does, not after the convention. | Renamed `ShadowfaxScreensPrefab`. One-off. |
| 5 | LOW | A test pinned the null XPath, which a `CustomPatch<XmlDocument>` never reads; its only effect was keeping the class out of the binding gate. | Test pins a non-behaviour | The null was added to fix CS0121 and then pinned as if it mattered. | Replaced with a live-install test that each vanilla movie still has a definition to zero and no VisualState with its own duration. One-off. |
| 6 | LOW | `DelayOnBegin` handling had no runtime effect: the engine uses it only while the timer is below the duration, which a zero duration never is. | Dead code | Same as #2: the attribute's meaning was assumed. | Deleted with its two tests. |
| 7 | LOW | The new tests read `Main/_Module/GUI/Prefabs/...`; git stores `PreFabs`. Green only on case-insensitive NTFS. | Path casing | Path typed from the vanilla folder name. Found by the orchestrator before the lenses ran. | Fixed. One-off (one older test, `FactionScreenPrefabBindingTests`, has the same casing; not in scope). |
| 8 | LOW | Feature doc said `#TBD`, lacked a Configuration section, named only the solo registration path, and gave no reason for two party durations it kept. | Doc completeness | Issue number not found at write time. | Fixed; the two kept durations are now zero by the maintainer's decision. |
| 9 | LOW | The rewriter walked the whole document with a live `GetElementsByTagName` list; the engine reads only `Prefab/VisualDefinitions`. 1 to 31 microseconds per prefab load. | Efficiency | Convenience API chosen without reading what the engine reads. | Narrowed to that block, with a test that a stray definition is left alone. |
| 10 | LOW | `FormationListPanel` and `FormationSeperator` belong to a commented-out dropdown, so their edits have no effect today; the doc presented them as live. | Doc accuracy | Edited by name without checking the widgets exist. | Doc says so. One-off. |

## Root-cause pattern

Findings 2, 6 and 10 share one cause: the change reasoned from XML attribute values and node names
without reading the engine code that consumes them. The values were read correctly from the installed
prefabs, which made the claims look verified. Finding 1 is the second instance of a binding gate that
silently skips a case it was not written for.

## Why each lens caught or missed these

- **Standards, Engine compatibility, Data flow, Completeness:** all four found #1 independently.
  Engine compatibility and Data flow found #2 and #6 by reading `Widget.UpdateVisualDefinitions`.
- **XML:** found #2, #8 and the doc side of #10; confirmed the 11 edits byte-exact.
- **Efficiency:** found #9 with measurements.
- **Design:** turned #3, #5, #6 into one collapse and proposed the one-rule party change.
- Nothing was missed by every lens. The author missed #1, #2 and #7 before review.

## Feedback memories to codify

None. Both systemic lessons are cross-feature rules, recorded in `docs/reviews/lessons/`.
