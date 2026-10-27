# RCA: grammar tags on TAOM's names, phase 1, BR and IT (#1023, deep-review, 2026-10-26)

## Top line

`/deep-review` ran all eight lenses in two waves, then a convergence pass, on the tag table, its tools, the console command that renders the kept pairs, and the 1,347 tagged repo rows. The review found the Italian tags themselves wrong for kingdoms and clans, a render report that could not show what the game shows, and tools that could silently write the live map or strip its tags. Every finding was fixed before the commit. The live TAOM_Map apply ran on Mike's word after the fixes, behind a new guard that refuses a tagged name that still starts with an article.

The pattern behind most findings: the build tagged and rendered names from the tag's meaning in the abstract, not from what the language's grammar code does with it, and the tools were judged on the repo files, not on the live file they also write.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | MED | IT kingdoms and clans were tagged `{.FN}`. The Italian `GENDER` code maps FN to feminine, and 29 of the 30 vanilla selections differ between MN and FN, so "Erebor è stata distrutta". | The build read only the prepositions, which MN and FN share, and not the agreement code. | Kingdoms and minor factions `{.MN}`, clans `{.MP}` or `{.MN}`; untitled IT castles `{.MN}` ("castello"), as found at convergence. |
| 2 | MED | Names that start with an article ("A Mão Branca", "Il Morannon") would print the article twice once tagged. The live apply's confirm gate counted `{.MS}O Morannon` as correct. | The tag step and the gate compared bodies with the tag stripped, and nobody checked the text after the tag. | The articles were dropped and pinned (58 BR, 59 IT names); `tag_report` counts an article-led tagged row as wrong and the apply exits 2 on one; `castle_ES1` was rewritten first. |
| 3 | MED | The kept-pair render bound each name once, while the game binds Name, InformalName and FormalName, so a block could read SAME where the game differs. A render that failed read SAME, then (because `TextObject.ToString()` catches every exception) never read FAILED. | The render was written from the variable names, not from what `Helpers.FactionHelper` and the engine bind. | Each plain variable renders with Name and InformalName; `*FORMAL*` uses FormalName; an "Error at id:" line is FAILED. |
| 4 | MED | The render logic sat in the adapter, which held state and needed a call order. | The adapter was built first and grew the rules. | Stateless id-keyed adapter reads; the rules moved into `KeptPairRenderService`, tested with NSubstitute. |
| 5 | MED | The rebuild and the translator could tag or strip the live TAOM_Map file with no flag; fresh model output was tagged unchecked; settlements were judged on repo text, not the live text the apply tags. | The tools were tested on repo files only. | One `--allow-live-write` flag gates the live tag step; only cache and override fills are tagged; settlement tags are judged on live text; plan-then-write with exit 2. |
| 6 | LOW | The processor canary read a private field, where a public factory exists. | The field was the first thing found. | `LocalizedTextManager.CreateTextProcessorForLanguage`. |
| L | LOW | Other LOW findings, all fixed: the BR "clan eliminated" row's empty fifth option; three retired ids; a command named outside the `print_` convention; a report written outside `Logs`; two write passes in the translator; an unreachable guard and prompt rule; a second C# copy of the head-noun rule; dead code and always-true conditions; tests that could not fail; uncached test parses; stale counts and docs. | Hygiene. | Fixed. |

## Accepted limits

- 45 BR and 46 IT names stay untagged: 40 rows still in English, 3 Haradric title words and 2 lore town names per language, plus the adjective "Gondoriano" in IT.
- The rebuild and the translator do not yet keep a replaced row's tag when run without the flag; the doc names that as owed with phase 2.
- The kept-pair deletions wait for Mike's in-game render with `taom.print_kept_pairs`.

## Lesson

`lessons/localization-ui.md`, "Before tagging a translated name, check what the engine's grammar does with the tag in that language".
