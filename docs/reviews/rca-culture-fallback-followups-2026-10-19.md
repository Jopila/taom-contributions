# RCA: culture fallback follow-ups (#932, #933, #937, #938; deep review 2026-10-19)

## Top-line

Four follow-ups from the culture fallback batch were built together and reviewed together (seven lenses,
then one convergence pass): bearer banners for Lindon and Blue Craig (#932), a banner in the reward for
joining a TAOM-culture kingdom (#933), default dishes for the tavern maid in TAOM-culture towns (#937), and
a gate on patch category numbers (#938). The review found no runtime defect. It found 5 MEDIUM and 17 LOW
findings; the two patterns that matter beyond these fixes are below the table. All were fixed in the same session;
the paid translation of #937's two strings waits in #941.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | #937 shipped a postfix plus a copy of vanilla's seven culture ids (rule, rule tests, an IL-literal test); vanilla's own branches set both variables, so a prefix that pre-sets the defaults needs no list | Copied engine logic | The plan read the method's culture checks and wrote their complement, without asking whether vanilla overwrites what a prefix sets | Prefix; rule and both test files deleted. Lesson in `lessons/harmony-il.md` |
| F2 | MEDIUM | #938's feature-map check read 10 of 53 rows that name a patch number; Telchar's Mark (renumbered the same day) was outside it | Gate that checks little | Matched one phrasing ("feature module, PatchNNN") instead of every mention | Every PatchNNN mention on a row is checked against the owning feature folder |
| F3 | MEDIUM | #938 had no floor on what it parsed: after the rebase it would silently check one row fewer | Vacuous gate | Same | Floors on categories, headings and mentions. Lesson in `lessons/testing-qa.md` |
| F4 | MEDIUM | All four issue bodies described the plan, not what shipped | Docs drift | Filed before the build; fourth and fifth time this batch | Bodies rewritten |
| F5 | MEDIUM | No translation backlog issue for #937's English-seeded keys | Process | House pattern not followed | #941 |
| F6 | LOW | #933 shipped `PickReward`, a copy of the engine's random pick with a clamp that can never fire, plus six tests of the copy | Copied engine logic | Same habit as Bard Town Guard F4, the same day | The engine's `GetRandomElement` on the service's pool. Lesson in `lessons/gamemodels-services.md` |
| F7 | LOW | #932's coverage test keyed on lord culture; BannerBearers keys on the formation's troop culture, and Lindon and Blue Craig lords already field rivendell and goblin troops | Gate on the wrong value | The test copied the Lord Banners gate's culture source | The gate reads the cultures of infantry troops in party templates. Same lesson as F3 |
| F8 | LOW | #938's shared-number allowlist was keyed by number only, so a third feature on Patch63 would pass | Weak allowlist | Keyed by convenience | Keyed by number to the exact owner set |
| F9 | LOW | Doc claims: "a prefix would have to reproduce the culture branches", a PatchShield sentence that did not fit the patch kind (a throw swallowed in a postfix leaves vanilla's result; in a prefix vanilla is skipped and the call returns false), "an empty line" (an unset variable renders blank), "whenever a tavern maid is addressed" (every conversation start reaching the line), PatchShield's co-op exception missing | Docs | Written from the plan | Corrected |
| F10 | LOW | Counts nothing computes in the BannerBearers doc, config comment and tests ("28", "30 of 38") | Repeat offender | Counts written by hand again | Counts removed; uncovered kinds named instead |
| F11 | LOW | Test hygiene: an IL test tagged `RequiresGameIL` without `BindingVerification`, tests outside their service's test class, a regex with a raw line break, a pointless cast, the apply test bypassing the category | Standards | Normal review yield | Fixed |
| F12 | LOW | Docs gaps: #933 missing from the Lord Banners overview, changelog and in-game check; no INDEX row; the new gates not named where authors look | Completeness | Normal review yield | Fixed |

## Patterns

**Copy the engine's case list or pick only as a last resort.** F1 and F6 are the same habit as Bard Town
Guard F4 that morning: when TAOM needs "vanilla's answer, plus a fallback", it re-wrote what vanilla does
(seven culture branches, a random pick) instead of letting vanilla run and filling only the gap. A copy
drifts silently and needs its own tests. Pre-set and let vanilla overwrite, or call the engine's own pick on
a TAOM-built pool.

**A gate must read the value the runtime reads, and say how much it read.** F2, F3 and F7: the number gate
matched one phrasing and passed whatever it did not see, and the bearer gate checked the lord's culture
while the runtime reads the troops'. A gate that checks little reads exactly like a gate that passes.

## Why each lens caught or missed these

- **Engine compatibility (2):** F1 (from the installed IL) and most of F9.
- **Data flow (5):** F2, F7, F8.
- **Design (6):** F6, the restructured gate, the merged bearer test.
- **Completeness (4):** F3, F4, F5, F12.
- **Standards (1), XML (7), Efficiency (3):** F10, F11; the XML lens confirmed the strings and seeds are
  byte-faithful and marked for the later translation run.

## Feedback memories to codify

None. The patterns are lessons in `lessons/harmony-il.md`, `lessons/gamemodels-services.md` and
`lessons/testing-qa.md`.
