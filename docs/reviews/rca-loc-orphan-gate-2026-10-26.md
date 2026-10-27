# RCA: A gate for text rows that nothing reads (#1018, deep-review, 2026-10-26)

## Top-line

#1018 added `tools/check_orphan_loc_rows.py`, which lists every TAOM text row in the English strings files that
no C# literal, data file or declared composed key family reads. Its first run retired 34 rows (the names and
hideout texts of goblin and Misty Mountain orc bandit factions that were never built; checked unread in this
repo and in the installed TAOM_Map and LOTRLOME_Armory). `/deep-review` (a tooling lens and a strings lens,
then a convergence pass) found the retirements clean but **two blind spots in the gate** (HIGH) that let 53
dead rows through, and a set of LOW tool and wiring items; all fixed before the push.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (Tooling) | A row named only inside an XML comment counted as read; 35 rows of the parked Far Harad half-troll career passed that way. | A reader test that ignored comments in data files while it skipped them in C# | The C# tokenizer handled comments; the data tokenizer did not. | Comments stripped before reading; the 35 rows allowlisted as a parked feature's data. |
| F2 | HIGH (Tooling, Data) | The exemption for rewords of vanilla texts accepted any row whose text opened with a non-TAOM key, in any file; the loader reads only one file and the last row per key wins. (The loader registers any key not starting `taom_` and not `!` or `*`; the eight-character vanilla id is the project convention the localization tests pin, and the exemption now matches that convention.) 18 earlier rows were overridden by later rows for the same key and shown in no language. | A rule looser than the code it models | Written from the row's shape, not from the loader. | The exemption matches the loader exactly (shared constants in `_loc_sources.py`); a shadowing check; the 18 overridden rows retired, with 47 byte-identical duplicate rows in four language files. |
| F3 | LOW (Tooling) | An empty run passed; the gate ran in no lane that changes its inputs; a path test was absolute; the shebang; tests weaker than their docstring; composed families passed rows of removed duties. | Gate robustness | First version. | Each fixed and tested; families require their stem in the data. |
| F4 | LOW (Docs) | The guide did not say to check the live modules before retiring a row; the catalog described the skipped rows wrongly. | Accuracy | Routine. | Docs corrected. |

## Convergence pass

A second pass over the fixes found one more gate gap and a set of LOW items, all fixed and tested:

- Shadowing is now judged by row position over every row of the override file, so a repeated id, or a later row whose id does not start with `taom_`, still shadows an earlier row.
- A family's `stem` must appear in a data file; a mention in a C# literal no longer counts.
- A run that finds English files but no `taom_` row exits 2.
- A stale allowlist entry is also one whose row is read another way (a winning reword row, a family-covered row).
- `SubModule.xml` joins the moduledata lane, since it decides which strings files the gate reads.
- The retirements left runs of blank lines in the English file and in the 12 language files (a language row sits between blank lines); 505 whole blank or comment lines were removed, and no row changed.
- A SHADOWED row is retired from the English file only: its language rows, cache entry and override belong to the vanilla key the winning row still uses.

## Feedback memories to codify

None; the gate is the preventive action.
