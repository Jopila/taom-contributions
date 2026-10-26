# Codex fix review: Provender-master remote selling (#909) review fixes, 2026-10-24

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` first. Scope is the uncommitted working tree of this worktree on top of `ed3bb65cc`.
Engine source for v1.5.5 is in `C:\Users\mikew\.taom-src\v1.5.5\`. Mark anything you cannot prove
from code as UNVERIFIED. An earlier pass is in `docs/reviews/raw/codex-prereview-provender-sale-2026-10-24.md`.

## The fixes to verify (do not re-review the rest)

1. `Main/Adapters/ProvenderSaleAdapter.cs` `Settle`: each sold stack moves on its own (`Deliver`); a
   stack whose `AddToCounts` throws goes back to the main party's roster; `PartyScreenCommit` runs
   in a `finally`. Earlier finding: one throw lost the rest of the goods and skipped the commit.
2. `ProvenderSaleService.Open(string townId)`: the click opens the town the row named, found in a
   fresh quote at that quote's fee, or nothing. Earlier: `Open(rank)` opened the rank-th town of the
   fresh list.
3. `ProvenderSaleRules.TownGoldChange`: a player gain of 0 or less changes nothing (the screen can
   only sell; a buy-back takes back a sale at its own price).
4. `ProvenderSaleRules.Rank`: drops a distance that is not finite, is 0 or less, or is at or past
   1e7 (the engine's unreachable placeholders 1e8 and float.MaxValue; 0 for an uncached pair).
5. A camp with a live encounter returns `ProvenderBlock.InEncounter` with its own text.
6. Text: the master-switch hint names selling; the sold message reads "sold {COUNT} of your goods".

## Questions

- Is each fix right and complete, and does any fix break a path the first pass called sound (gold,
  items, prices, gates)? In particular: can returning an undelivered stack to the party, after the
  town or the engine already paid for it, make a profit or a loss the player can repeat?
- Is 1e7 safe: can a real TAOM_Map distance reach it, or can an uncached real pair report 0?
- Any test that now asserts a state the engine cannot produce.

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact and proof.
Say PASS for each fix you verified.
