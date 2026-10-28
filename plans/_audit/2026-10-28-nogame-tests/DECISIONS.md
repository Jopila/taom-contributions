# DECISIONS: 2026-10-28-nogame-tests

| # | Question, with its evidence | Options, recommended first | Answer | Follow-through | Done in |
|---|---|---|---|---|---|
| 1 | Plan 010 made a RefAsm build stamp an empty game folder; D46 (2026-09-24) chose to point the no-game unit step at the stub folder and was reverted on 10 untagged failures. Keep a stub stamp plus 22 `RequiresGame` and 2 `RequiresGameIL` tags (gate 591/591, was 509 failed of 593)? | Keep it; revert to the empty stamp | Keep it | Stamp, tags, RCA | `8589a3384` |
| 2 | A set `BANNERLORD_GAME_DIR` outranked the stamp, so a no-game run mixed real module DLLs with stubs (the wrong "29 failures" count). Let the stamp win in a RefAsm build? | Stamp wins; keep the order | Stamp wins | `GameAssemblies.ResolveGameDir` RefAsm branch and two tests | `8589a3384` |
| 3 | Run Codex xhigh on the gate fix (paid dispatch)? | Skip; run | Skip | none | n/a |
| 4 | Plan 044 Step 0: add the RefAsm unit step to `/verify`? | Yes, in a temporary worktree; no; a hosted CI job | open | Waiting on the maintainer: FOR-MIKE.md | |
