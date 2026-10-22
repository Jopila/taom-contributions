# RCA: TAOM.Client submodule, Batch B3 of ADR-013 (#968), 2026-10-22

**Summary.** Commit `03d9db8dc` added `TAOM.Client.dll` as a second, client-only submodule, with its
own deploy target, a server-mirror exclusion, a release-gate rule and an isolation tripwire. Its
body says it was reviewed by seven deep-review lenses in two waves before it was committed. Codex
(gpt-6-astra, xhigh, 227,323 tokens) then found three MEDIUM defects in the committed code: a
Main-only deploying build leaves an old `TAOM.Client.dll` beside a new `TAOM.dll` in two of the
three client bins (F1), the tripwire that bans patches in `TAOM.Client` misses qualified, suffixed
and grouped attribute spellings (F2), and the release gate skips a secondary client bin that has
`TAOM.Client.dll` but no `TAOM.dll` (F3). Two waves of deep-review lenses then ran on the fix and
found defects in the fix itself: the Game Pass delete was gated on a flag the build package
ignores, two comments overstated what a stale pair does, and the inserted csproj lines staled line
citations in five docs. Every miss, in the first review and in the fix, has one shape: a rule was
reasoned from one member of a set (one bin, one attribute spelling, one assembly, one condition)
and never walked across the rest.

Sources: Codex output `docs/reviews/raw/codex-adversarial-taom-client-b3-2026-10-22.md`, prompt
`docs/reviews/codex-adversarial-taom-client-b3-2026-10-22.prompt.md`. Fix lenses, wave 1:
Standards, Data flow, XML, Tooling. Wave 2: Efficiency, Completeness, Engine compatibility, Design.
Each finding below was re-read against the code before it was acted on.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM (Codex F1) | A deploying build of `Main/TAOM.csproj` alone cleans `TAOM.Client` from `Win64_Shipping_Client` (BuildResources' `CopyBinariesWindows`), but the editor mirror and the Game Pass `CopyModule` are additive, so an old `TAOM.Client.dll` stays beside the new `TAOM.dll` in `Win64_Shipping_wEditor` and `Gaming.Desktop.x64_Shipping_Client` | Stale state across additive deploy destinations | The deploy was designed from the Steam bin, where the package cleans. The other two destinations were assumed to behave the same; nobody tabulated each destination's end state per build mode | Fixed by deleting the stale half where it is additive: a `Delete` in `MirrorWin64ShippingClientToEditor` and a new `DeleteStaleGamePassTaomClient`. The end state is the engine's "Cannot find" box, not a `GetTypes` abort. Tests pin both deletes. Lesson in `lessons/build-tooling-workflow.md` |
| 2 | MEDIUM (Codex F2) | `ClientIsolationGateTests` tripwire regex required `[` right before the short attribute name, so `[HarmonyLib.HarmonyPatch(...)]`, `[HarmonyPatchAttribute(...)]`, `[HarmonyPatchCategory("X"), HarmonyPatch(...)]`, `[PrefabExtensionAttribute(...)]` and a qualified `ViewModelMixin` all escaped it | Text scan beaten by a variant spelling (repeat) | The six DataRows were written in the one spelling the regex was written for; the lenses added more rows in the same spelling (Completeness C3 added three setting-base names) | Fixed with a word match that allows the `Attribute` suffix, five new DataRows and a `HarmonyPatchCategory` alone negative case. A comment names the forms a word scan still cannot see (a subclass under another name, `Harmony.Patch(...)` by hand). Lesson in `lessons/testing-qa.md` |
| 3 | MEDIUM (Codex F3) | `require_build` checked a secondary client bin only when it held `TAOM.dll` (`required[0]`), so a bin with only `TAOM.Client.dll` passed the release gate and ships a client assembly that fails `GetTypes` and aborts the game | Completeness check keyed on the member it requires | The rule was written as "a bin that has the primary is checked", which is the half of the pairing the author was thinking about. Tooling T4 noted "per-bin client presence" as a follow-up and the new test asserted only the `TAOM.dll`-without-client direction | Fixed: a secondary bin is checked when it holds any required DLL. Tests for the lone `TAOM.Client.dll` and for a bin holding neither. Lesson in `lessons/build-tooling-workflow.md` |
| 4 | n/r (Codex S5, coverage gap) | No test asserted `AfterTargets="Build"` on `DeployTaomClientToTaomModule`; Codex removed it in memory and every target-content assertion stayed true | A guard with no test that fails when it is removed | The deployment tests inspected what the target copies, not when it runs (the same family as the lesson "A guard needs a test that fails when the guard is removed") | Fixed: `ClientCsproj_Deploy_RunsAfterBuild`. The Completeness lens added the matching pin for both mirrors (`MainCsproj_Mirrors_RunAfterTheModuleCopy`, Completeness T1) |
| 5 | LOW (Standards S3, XML 1, Data flow D1) | The first Game Pass delete was gated on `DisableModuleCopy`, but `CopyModule` (`Basic.targets:64-65`) ignores that flag, so `-p:DisableModuleCopy=true` alone still left a mismatched pair in `Gaming.Desktop` | One condition generalized to a different target | A defect in the fix: the gate was copied from the editor mirror's condition, and CopyModule's own condition was not read | Fixed: own target `DeleteStaleGamePassTaomClient`, `BeforeTargets="PostBuildCopyToModules"`, on CopyModule's condition. `MainCsproj_GamePassTaomClientDelete_RunsOnCopyModulesCondition` asserts `ModuleId` is present and `DisableModuleCopy` is not |
| 6 | LOW (Data flow D2) | The `Client/TAOM.Client.csproj` comment said the bare `DisableModuleCopy` flag cleans "the client bins", and the new test locked in the `DisableModuleCopy` gate | A comment and a test built on the same wrong premise as row 5 | Same premise, written down twice | Fixed: comment rewritten per bin; the test now asserts the opposite |
| 7 | LOW (Tooling 1, Standards nit) | The new `package_release.py` comment said a stale or lone `TAOM.dll` aborts; the engine shows "Cannot find" and continues, and a lone `TAOM.Client.dll` is what aborts (`GetTypes` CriticalError) | Engine behavior stated from memory | Codex's S3 had split missing from incompatible; the first comment used the short form anyway | Fixed in the comment and the existing test's comment, "stale" became "lone" |
| 8 | MEDIUM, comment only (Engine compatibility, 1 WRONG of 12) | "A stale `TAOM.Client` beside a newer `TAOM.dll` aborts in `GetTypes`" is overstated: the pair binds by simple name, so the old client loads and aborts only if core changed a type or member it binds at load (`IClientComposition`, `ClientComposition.Current`); otherwise it runs old code (the #371 class). Completeness D8 asked for "can abort" | Engine behavior overstated | A single failure mode was remembered and generalized | Fixed in the csproj comments and test messages ("can abort"). The abort versus stale-code split is now written next to the deletes |
| 9 | LOW (Tooling 2, Standards S1) | The old gate rule ("each bin holding `TAOM.dll` must hold every assembly") was still written at `package_release.py:171-172` and in `release-process.md` | A rule changed in one place and not in its copies | The fix changed `require_build` and its comment, not the constant's comment or the doc that restates it | Fixed in both places |
| 10 | LOW (Standards S2, XML 2; Completeness D1 to D5, D7, D9, D10) | The inserted csproj lines staled line citations in five docs; docs named only the server mirror's delete; `module-taom.md` target counts (six total, five in Main); a `lessons/build-tooling-workflow.md` cite; the research doc still said B3 was uncommitted; `scene-scripts.md` omitted that the editor bin holds `TAOM.Client`; `CopyBinariesWindowsStore` was omitted | Documentation drift after an insertion (repeat) | Inserting lines in `Main/TAOM.csproj` moves every citation below it; nothing greps docs for `<file>:<line>` after an edit | Fixed in the docs listed in the working tree. Lesson extended in `lessons/build-tooling-workflow.md` ("an insertion stales every citation below it") |
| 11 | LOW (Standards S4) | `ClientCsproj_DeployRunsAfterBuild_SoItFollowsMainsMirrorsAndDeletes` claimed an ordering it did not assert | Test name claims more than it proves | Named for the goal, not the assertion | Renamed `ClientCsproj_Deploy_RunsAfterBuild` |
| 12 | LOW (Efficiency, robustness) | The new editor-mirror `Delete` fails the build (MSB3061) when the Modding Kit holds `TAOM.Client.dll` open, even when nothing changed | A fix that adds a new failure mode | The delete was written for the stale case; the Kit holding the file open is a case the first version did not consider | Fixed: `TreatErrorsAsWarnings="true"` on that `Delete`, pinned by `MainCsproj_EditorMirror_ToleratesALockedTaomClient`. The `Copy` after it still fails loudly if `TAOM.dll` changed under the Kit |
| 13 | LOW (Completeness T3) | The regex test did not say which spellings the scan still cannot see | A scan that does not state its blind spots | Same cause as row 2 | Fixed with the comment named in row 2 |
| 14 | LOW, outside the diff (Engine compatibility) | The co-op appliance's `Copy-SubModuleXml` keeps only named submodules and strips their tags, so the engine tags are not what keeps `TAOM.Client` off today's server; and "before evaluating the host target's own Condition" in a pre-existing comment has the order backwards | Pre-existing | n/a | NOT APPLIED: outside this change's lines (`working-discipline.md` "Edit scope"). Recorded here |
| 15 | n/r (Completeness D6) | ADR-013 still names only the server mirror's delete | Documentation drift | The ADR is a protected file | NOT APPLIED: Mike decides the ADR edit |
| 16 | UNVERIFIED (Data flow U1), follow-up (Tooling 3) | A Visual Studio fast up-to-date skip or `--no-dependencies` can deploy a client beside a core it was not built against; `required_dlls` reads the installed `SubModule.xml`, not the release commit's | Design limits | n/a | NOT APPLIED: Mike's call on both |
| 17 | n/r (Data flow, out of scope) | The four `Stubs/*` modules deploy to `Modules\<id>\_Module\SubModule.xml`, one level too deep | Pre-existing | n/a | NOT APPLIED: `/investigate` plus an issue, ask Mike |
| 18 | KEEP proposal (Design) | `BuildForWindowsStore=true` instead of `DeleteStaleGamePassTaomClient` would clean every stale Game Pass binary | Alternative | n/a | NOT APPLIED: it changes what ships to Game Pass players (3 NuGet DLLs, 1.3 MB), and Game Pass may not work at all today (`TAOM.Dependencies` stages its libraries for Win64 only, and TAOM's Game Pass bin has no `DryIoc.dll`). Mike decides whether Game Pass is supported |
| 19 | n/r (Codex section 3) | An unreadable manifest passes the build gate because it requires every assembly | Design | n/a | NOT A DEFECT: by design, a parse failure falls back to requiring every known assembly. Recorded, no change |

## Root-cause pattern

Findings 1 to 3 and 5 are one habit: **a rule was reasoned from one member of a set and never
walked across the rest.**

- **One bin.** The deploy worked in `Win64_Shipping_Client`, where BuildResources cleans, so the
  other two client bins were assumed to do the same. They are additive (row 1).
- **One attribute spelling.** The regex matched `[HarmonyPatch(`, so `[HarmonyLib.HarmonyPatch(`,
  the `Attribute` suffix and a grouped list were never tried (row 2).
- **One assembly.** The release rule keyed on `TAOM.dll`, so the opposite half of the pair, the
  client alone, was unchecked (row 3). A completeness check keyed on the very member it requires
  cannot see that member's absence.
- **One condition.** The Game Pass delete borrowed the editor mirror's `DisableModuleCopy` gate,
  while `CopyModule`, the target that fills that bin, ignores it (row 5).

Rows 7 and 8 are the same shape applied to engine behavior: one failure mode (the abort) was
remembered and stated for every stale pair, when a stale pair either loads old code or aborts, and a
missing file only shows a box. Rows 5 to 8, 11 and 12 are defects in the fix pass; Codex never read
them. "Review the remediation as new code" (`.ai/review-reference.md`, 2026-09-01) held again.

Row 10 is the separate, recurring documentation habit: an insertion in a file stales every
citation below it in every doc.

## Why each lens missed these

The pre-commit review of `03d9db8dc` ran seven lenses in two waves (the commit body says only
that). What each was asked beyond that is UNKNOWN to this RCA: the prompts are not in the repo. The
findings the lenses filed are, and they show where each stopped.

- **Engine compatibility** verified 20 engine facts and recorded, as a "B10 note", that a stale
  `TAOM.Client` in the editor or Game Pass bin built against another `IClientComposition` would
  abort. It filed the hazard as a later-batch note and did not trace which target leaves the file
  there after a Main-only build (F1).
- **Data flow** found that the bare `DisableModuleCopy` flag strips `TAOM.Client` from the Steam
  bin (its D-F4) and fixed the comment. It followed the flow in the Steam bin only (F1).
- **Tooling** wrote T1 (follow the shipped manifest) and listed per-bin client presence as T4
  "if cheap"; the rule it produced keyed on `TAOM.dll` (F3).
- **Completeness** added three DataRows for the tripwire (C3) and the `TAOM.dll is missing`
  assertion (C4). Both extended the existing spellings and direction; neither varied them (F2, F3).
- **Standards, XML and Efficiency** filed nothing on F1 to F3 in the pre-commit pass. Why is
  UNKNOWN beyond their stated scope (rule files, manifest shape, cost).

Codex succeeded because it executed counterexamples (the regex against six spellings, the packaging
function against in-memory bins) instead of reading the pattern, and it tabulated the four bins.

## Repeat offenders

- **F2 is another text scan beaten by a variant spelling.** Earlier cases in
  `lessons/testing-qa.md`: "Detection rulesets need a positive firing test per rule" (2026-06-22,
  the JSON form of a rule written for the YAML form) and its "Recurred" note for #644 (2026-09-23,
  one regex spelling of `<xsl:attribute name="race">`); "A text scan cannot see a target-typed
  `new(`" (#957, 2026-10-20); and the `PartyBase.MainParty` scan that missed `?.` (#792,
  2026-10-14, also listed in `.ai/review-reference.md`). Four prior cases, so the preventive action
  is stronger than a test row: write the spellings the scan cannot see next to the regex, and test
  qualified, suffixed and grouped forms.
- **Stale line citations after an insertion** recurred from "Take a cited line number from a read of
  that one file" (`lessons/build-tooling-workflow.md`, 2026-10-21), which covers where a number is
  read from. This case is the other half: a correct number goes stale when lines are inserted above
  it. That lesson now carries the sentence.

## Feedback memories to codify

None. Rows 1 to 3 and 5 are covered by the lessons and review-reference lines listed below; a
memory entry would repeat them.

- **Lesson** (`lessons/build-tooling-workflow.md`): list each destination's end state for each build
  mode before trusting a deploy target; a completeness check must not be keyed on the member it
  requires; correct the BuildResources clean scope; an insertion stales every citation below it.
- **Lesson** (`lessons/testing-qa.md`): a regex scan lists the spellings it cannot see, and its tests
  cover qualified, suffixed and grouped forms.
- **Review reference** (`.ai/review-reference.md`, "Look harder here"): additive deploy
  destinations; a completeness check keyed on the member it requires.
