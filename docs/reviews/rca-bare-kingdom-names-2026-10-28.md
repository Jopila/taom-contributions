# RCA: bare kingdom names review, 2026-10-28

## Top line

#1050 renames the kingdom `name` of Arthedain, Cardolan, Rhudaur and Ered Luin from "Kingdom of X" to the
bare realm name, as every other kingdom has it, and adds `KingdomNameTests` so the next realm cannot ship the
old form and no translator run or rebuild can bring it back. The change is four display strings and their
translations, so one `/deep-review` lens ran (7 XML and ModuleData, with a data-flow question in its brief:
does anything match a kingdom by its name text). Codex did not run. The lens found no data defect and every
gate passed; it found one LOW in the stated behaviour of old saves, fixed in this change.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The issue said an existing save keeps the old name. That holds in English only: a save stores the name as `{=taom_arthedain_name}Kingdom of Arthedain`, English shows the inline text, and every other language looks the key up and gets the new row (`MBTextManager.GetLocalizedText`, v1.5.6) | Engine claim stated without a read | The claim came from "the game saves kingdom names" without following how a saved `TextObject` resolves per language | Issue #1050 and the commit body corrected. Lesson: a saved `{=KEY}` text is re-localized on load in every language but English |

Also fixed from the lens notes: `docs/features/ered-luin.md` told the tester to confirm "the Kingdom of Ered
Luin" in the encyclopedia, which now lists "Ered Luin".

## Why it was missed

The builder (this session) wrote the save behaviour into the issue from memory of the engine, not from a
read of `MBTextManager`. The lens read the save path (`Kingdom.Name` is saved; a loaded save never re-reads the
Kingdoms XML) and the text manager, which is where the English-only behaviour shows.

## Process notes

- The first regeneration ran in the main checkout, which held another session's uncommitted banner work in
  the same generator. Only this change's eight lines were undone there, and the fix moved to the worktree
  `E:\repos\taom-bare-kingdom-names`. The other session's own `--apply` writes the same banner bytes, so its
  work is not damaged.
- The lens saw five doc and test files change while it read (the gate and the docs Mike asked for during the
  review); it reviewed the data diff by hash and ran the new tests as evidence.
