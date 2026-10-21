# RCA: Desertion costs the commander's favor (#833, deep-review, 2026-10-21)

## Top-line

`/deep-review` (seven lenses, then a convergence pass) on #833 found **no engine incompatibility** and
**one MEDIUM defect**, plus a set of LOW findings, all fixed before the first commit. The orchestrator
found one more before the review: the first build replaced two already translated lines with new keys,
which would have shown English in all 12 languages.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Standards, Engine compat, Data flow) | The two config values were `double`, validated only as finite and at most 0, then cast to `int` unchecked. A hand-edited value near `int.MinValue` wraps inside the engine (`ChangeRelationAction.cs:19`, `TraitLevelingHelper.cs:183` add before they clamp), so the penalty becomes relation +100 or maximum Honor. | Float-to-int cast without a range | The plan said "validated as 0 or below" and took the sign check for the whole rule. | Both values are `int`, validated with the existing `RevertIfOutOfRange` (relation -100..0, Honor -1000..0). Lesson appended to `lessons/gamemodels-services.md`. |
| F2 | MEDIUM (orchestrator, before review) | Full-body `_v2` keys replaced two lines already translated in 12 languages. | Localization regression | "An English edit stales 12 languages" (trap index) was read as "use a new key", but a new key for an existing line loses its translations just the same. | The original keys stay; one new cost sentence is appended (adapter append parameters, and a composed dialog line). |
| F3 | LOW (Standards) | `EnlistmentReleaseDialogBehavior` grew to 151 lines, past the 150-line entry-point limit, with no gate to catch it; the second time this session (#832). | Ungated rule | No Enlistment test pins entry-point size. | A line-count test for every Enlistment hook. |
| F4 | LOW (Data flow, Completeness) | Step 7c ran unguarded before the record reset; a throw from the Honor adapter would have left the record in Discharging. | Unguarded pipeline step | The relation adapter catches; the trait adapter does not. | A try/catch around 7c, and a test that a throwing penalty still completes the discharge. |
| F5 | LOW (several) | The dialog line was rendered at registration, so a language change mid-session kept the old language; the new sentences said "his favor" although TAOM ships female lords; dead null fallbacks repeated the defaults; a redundant authority check inside the service; stale "one producer" comments and doc lines; a missing zero-Honor test; the adapter doc for the new parameters; the issue body still the plan. | Polish | Various. | All fixed; the dialog line composes `{=!}{LINE} {COST}` at display time; the text is gender-neutral. |

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources. It confirmed that
`AddPlayerLine` returns the registered sentence, that the variables set on its text survive the copy the
conversation makes each time it shows the line, and that the line renders the original sentence followed
by the cost sentence, translated per language. It found six LOW items (a contradictory gate comment, a
duplicated doc paragraph, an ambiguous word, a test name, unenforced line-count exceptions, an unchecked
fallback literal) and one wording nit in the lesson, all fixed.

## Root-cause pattern

F1 and F2 share a cause: a rule was applied by its headline, not by its reason. "Validate the value" was
read as "check the sign", and "do not edit an English row" as "make a new key". The reason behind each
rule (the engine's integer arithmetic, the translations attached to a row) is what the change had to
protect.

## Why each lens caught or missed these

- **Standards:** F1, F3, the dead fallbacks, the stale producer comments.
- **Engine compatibility:** F1 with the engine arithmetic, the language-change behaviour, the Honor threshold fact.
- **Efficiency:** no findings.
- **Completeness:** the issue body, the NaN rows the int change retires, F4, the adapter doc.
- **Data flow:** F1, F4, and every producer, reason and co-op path connected.
- **Design:** the composed dialog line and the redundant authority check; confirmed the service shape.
- **XML:** the gender-specific text; every other row byte-identical to HEAD.

## Feedback memories to codify

None; the lesson covers the pattern.
