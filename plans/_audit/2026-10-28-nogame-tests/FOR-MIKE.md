# FOR-MIKE: 2026-10-28-nogame-tests

## Waiting on you
- Plan 044 Step 0: add the RefAsm (no-game) unit step to `/verify`, run in a temporary worktree, so a new test that needs
  the game but carries no `RequiresGame` tag fails at once. Recommended: yes. Cost about 2 minutes per `/verify`.
  Evidence: plan 044 "Step 0" and `baseline.md`.

## Detail
- Without the row, nothing runs the no-game path since the hosted CI was removed (`c8d69a0dc`), which is how 9 untagged
  tests built up (#1038 item 14).
