---
description: Autonomous-loop stewardship and edit-scope discipline, the always-on working conventions.
---

# Working discipline

## Autonomous runs (`/loop`, `/schedule`, background work)

With no fresh user prompt, continue established work; don't start new work.

- Continue what is already in motion: failing CI, open review threads, a feature with a clear next
  step, a flagged cleanup. Save new ideas ("we could also refactor X") for the next interactive turn.
- Reversible local actions (edits, tests, builds) are fine. Irreversible or shared ones (push, PR,
  branch delete, posting a comment) need authorization given for this run.
- If the transcript doesn't make the next step obvious, stop and report rather than guess.
- Once the run is established, never stop to ask "should I keep going?"; the user may be away. End
  only when the work is done, genuinely blocked, or the no-progress breaker below trips. Out of
  obvious steps, first re-read the transcript and recombine near-misses.
- A trivial failure (typo, missing import, a flake): fix and retry. A fundamentally broken approach:
  record the outcome (commit body, log) and move on.
- No-progress breaker: a work-driving `/loop` or wakeup ends each work tick with
  `python tools/loop_progress.py --state "<scratchpad>\loop-<name>.json"`, adding `--watch-path` for
  each out-of-repo file the task edits (the live Armory, TAOM_Map). Exit 3 means three ticks with no
  new commit and no change in status, diff or watched files: stop and report what was tried. A tick
  that only watches CI or the loop's own job skips the call. Progress is what the files show, never
  the agent's own report.

## Edit scope

Every changed line traces to the request. A bug fix doesn't reformat its method; a feature doesn't
rename existing variables. A refactor worth doing gets its own change: raise it after the requested
one is done.

Fix a bad value where it is produced, not in each consumer that receives it. Before you close a
finding, state the root cause, the fix, the validation, and the cleanup left over (a follow-up issue).

This is hardest during a review gate. While `/deep-review` agents are running, act only on findings
that change runtime behaviour: editing a file an agent is reading invalidates its report. The one
exception is a violation your own change introduced. The tell that you are about to break this is
writing "this is polish, not a fix" and continuing anyway.
