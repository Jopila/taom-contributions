# RCA: English-only overrides of vanilla keys (VanillaKeyOverrideTests gap, deep-review, 2026-10-26)

## Top line

A review of #899 found that `VanillaKeyOverrideTests` could not see vanilla keys that vanilla writes only in DLL code
and registers only in its root-level `Languages/std_*.xml` files. Mike asked for the gap to be fixed at once, with no
issue. Extending the test found 177 such ids with TAOM language rows. All 177 are English-only rewordings, mostly "the"
dropped before a faction name, whose English rows are data for the English override feature
(`LocalizationOverrideLoader`, `Patch25_LocalizationOverride`, English only).

The approved fix keeps the English rows and deletes the language rows, so non-English players get vanilla's curated
translation, except for 369 (language, id) pairs where vanilla's own row is wrong for TAOM. Those keep TAOM's row,
recorded with a reason in `kept-vanilla-key-rows.txt`. Two review waves and a convergence pass found three HIGH findings, three MEDIUM findings
and a set of LOW findings, all fixed before the commit.

The pattern: the first fix was right about the rule and wrong about the ground. It assumed vanilla's row is always
better, that one tool writes language rows, and that one gender problem explains every article. Each assumption failed
against a different engine or tool path.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | HIGH | `rebuild_translation_files.py` and `generate_translation_template.py` bypassed `skip_ids_for` and wrote all 177 deleted rows back, from the cache or as English that the translator then filled. | The fix guarded the translator only; the guide prescribes two other writers. | One shared rule (`override_ids_to_skip`) used by all three writers, a test per writer, and the stale cache entries pruned. |
| 2 | HIGH | In 35 pairs, vanilla's translation uses a variable the game never sets (for example German call-to-war texts with `{CALLING_KINGDOM}`), so a kingdom name or a gold amount rendered blank where TAOM's row was right. | The plan took vanilla's curated rows as correct. | Those pairs keep TAOM's row (reason `variable`). |
| 3 | MED | Vanilla's article logic needs a gender tag or a `{@Type}` attribute that TAOM's names lack: BR `{#GENDER}` (170), DE `{#TYPE}` (34), FR `{.cl}`/`{#GENDER}` (23) and IT `{#GENDER}` (2), so a wrong fixed article or an empty verb appeared. | The first triage judged rewordings by English meaning only. | Those pairs keep TAOM's row (reasons `gender-tag` and `article`); German `{#TYPE}` is not a gender problem, and the docs say so. |
| 3b | HIGH | Italian vanilla rows write the prepositions as tokens (`{.di}`, `{.a}`, `{.da}`) that print only before a name with a gender tag; no TAOM Italian name has one, so 105 deleted pairs dropped "di", "a" or "da" in 132 places ("dichiarerete guerra Mordor"). | Wave 2's re-derivation scanned `{#FUNC(` selections only, not dot tokens. | The 105 pairs keep TAOM's row (reason `gender-tag`); found by the convergence pass. |
| 4 | MED | `LanguageFileCoverageTests` read the install on every run, so the no-game run reported it as Inconclusive and lost the #434 gate. | The exemption used the exact vanilla set. | An install-free shape rule, with a LiveInstall test that checks the shape against the install. |
| 5 | MED | `skip_ids_for` kept its own copy of the override rule instead of the shared one. | Built in two steps. | One rule; each sibling file parsed once. |
| L | LOW | The convoy pair's rows dropped Turkish suffix logic; two dead overrides of keys v1.5.5 no longer has; a weak loader pin; a stale-line message that said "delete"; a misleading comment; stale counts and notes in five docs; the README and the guide disagreed about the rebuild tool; a newline test that did not test the trap. | Hygiene, and data nobody re-read after the engine moved. | Fixed; the dead ids deleted on Mike's word. |

## Accepted limits

- The 78 French vanilla rows that are not kept use `{.dl}` before an untagged TAOM name and give "de " with no elision
  and no "du" or "des" ("de Erebor" for
  "d'Erebor"); the rest read correctly. Not kept, because the only defect is a missing elision.
- The remaining fix for the Portuguese `gender-tag` pairs is gender tags on TAOM's translated names (about 4,000 rows),
  which is content work outside this change.

## Lessons

- `lessons/localization-ui.md`, "Before deleting a translation row as redundant, render the vanilla row it falls back to".
- `lessons/build-tooling-workflow.md`, "A data rule enforced in one writer is not enforced: list every tool that writes the file".
