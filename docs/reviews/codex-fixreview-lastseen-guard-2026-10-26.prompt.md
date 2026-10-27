# Codex fix re-review: Patch123_LastSeenTextGuard (uncommitted, 2026-10-26)

You are an adversarial reviewer for TAOM (Bannerlord v1.5.5 mod, .NET Framework 4.7.2, Lib.Harmony
2.4.2). Read `AGENTS.md` and `.ai/roles/reviewer.md`. Review only: no edits, no deploying builds, no
other AI calls. Report defects with file:line, impact and proof; try to refute each claim; mark
anything unverified UNVERIFIED.

## What changed since your pre-review

Your pre-review (`docs/reviews/raw/codex-prereview-lastseen-guard-2026-10-26.md`, end of file) found:
HIGH 1, the finalizer swallowed any `NullReferenceException`, including one from another owner's
prefix; MEDIUM 2, the tests allowed the fallback text to vanish and did not prove log-once; LOW 3,
"always threw" overstated the engine behaviour.

The fix, in the working tree (base `429713746`):
- `Main/Features/Encyclopedia/Hooks/HeroHelper_GetLastSeenText_Patch.cs`: the finalizer now runs at
  `Priority.First` and swallows an NRE only when `ThrewInOriginal` (frame 0 of the exception maps to
  the patched target through `Harmony.GetOriginalMethodFromStackframe`, or is the target itself) AND
  `LastSeenState.ReachesUnguardedLeaderRead` holds. Everything else goes back through
  `RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod)`.
- `Main/Features/Encyclopedia/LastSeenState.cs` (new): the captured hero state and the rule.
- `TAOM.Tests/Features/Encyclopedia/HeroHelperLastSeenTextGuardTests.cs`: 27 tests, including a
  real-Harmony synthetic target with and without a foreign throwing prefix, the fallback text with a
  real `GameTextManager`, log-once per hero, and a real apply to the installed target.
- Docs: the registry section, `docs/features/encyclopedia.md`, `docs/reference/feature-map.md`,
  `docs/reference/taleworlds-api-snapshot/patch-targets.md` (regenerated), the v1.5.5 migration
  note, a lesson in `docs/reviews/lessons/adapters-taleworlds-api.md`.

Other uncommitted files in the tree (`tools/triage_battle_load.py`, its test,
`docs/features/battle-load-diagnostics.md`, `docs/reviews/lessons/state-lifecycle-save.md`) belong to
another session: out of scope.

## Evidence

Full suite 27,528 passed, 0 failed, 7 skipped; binding gate (`TestCategory=BindingVerification`) 797
passed, 0 skipped; a mutation run (origin check forced true, fallback forced empty, log-once removed)
failed 5 tests, the file then restored byte for byte. Not run in game.

## Examine

1. Is HIGH 1 closed? Can any NRE not caused by a missing leader still be swallowed? Consider the JIT
   inlining small callees (`GameTexts.FindText`, `EncyclopediaLinkWithName`, `EncyclopediaLink`) into
   the replacement, other owners' patches, and PatchShield.
2. Can a real leaderless-read NRE now escape (a false negative re-opens the player crash)? Check the
   rule in `LastSeenState.ReachesUnguardedLeaderRead` against every branch of the installed
   `HeroHelper.GetLastSeenText` (`pwsh tools/taom-src.ps1 path Helpers.HeroHelper`), and check
   whether `Harmony.GetOriginalMethodFromStackframe` maps an exception's frame 0 inside a Harmony
   replacement on .NET Framework 4.7.2 (the synthetic test claims it does).
3. Does `Priority.First` really order this finalizer before PatchShield's, and does it matter?
4. Are MEDIUM 2 and LOW 3 closed? Any new defect in the changed tests or docs.

## Output

Findings by severity with file:line, impact, proof; then claims checked and correct; then UNVERIFIED.
