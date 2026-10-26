# RCA: Bandit Voices (#900, deep-review, 2026-10-26)

## Top line

`/deep-review` ran seven lenses plus the tooling lens in two waves, then a convergence pass, on the feature that gives
eight TAOM bandit clans their own robbery lines and a first-meeting greeting. No HIGH or MEDIUM finding: the hook, the
first-meeting read and the data merge were right as built. The review found a set of LOW findings and several
simplifications, all applied, and Mike approved one new guard, an expression-tag check in the translator, before the
paid #763 run translates the 45 tagged rows.

The one pattern worth a rule: lines a hostile NPC speaks to the player assumed the player was a Man of the West
("tall-folk", "softskin", "lowlander"), while a TAOM player can be a dwarf or an orc robbed by the clan of their own
people.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | LOW | Eight robbery and greeting lines named the listener's people ("tall-folk", "softskin", "lowlander", "westerner"), and one used "landsman". | The text was written for the clan's voice, not for every listener. | Neutral words, a pattern test for listener names, and a wider gendered-word test; the 12 seeds mirrored. |
| 2 | LOW | Nothing pinned the first-meeting wiring: passing `true` in place of the read kept every test green, which is exactly what decision 4 prevents. | The tests covered the gates one by one, not the wiring between the read and the roll. | A test where the read is false and the roll is low, proven by a mutation run. |
| 3 | LOW | The postfix body and the SubModule.xml node that loads all 80 rows were untested. | The data tests read the file directly. | Postfix tests (false and true paths, one warning on a throw) and a node pin. |
| 4 | LOW | The translator did not protect expression tags, and its prompt invited translating bracket words. | No tagged text had shipped at this volume before. | The tag guard, a prompt exception, and tests, including a moved tag. |
| 5 | LOW | Doc drift (a repeat meeting still robs in the clan's voice; "the first time you attack"; the co-op bucket), a comment that called body tags facial, and decompile line numbers in code. | Text written before the review traced the engine. | Corrected. |
| D | - | Simplifications: one `TrySetGreeting` lookup, a `bool` first-meeting read, the clan list left to the data, `void Apply()`, a field read as `object` (no BHA0006), binding at construction. | - | Applied; behaviour unchanged. |

## Lesson

`lessons/localization-ui.md`, "A line a hostile NPC says to the player must not name the player's people".
