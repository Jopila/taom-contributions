# Codex pre-review: Patch123_LastSeenTextGuard (uncommitted, 2026-10-26)

You are an adversarial reviewer for TAOM, a Bannerlord v1.5.5 mod on .NET Framework 4.7.2 with
Lib.Harmony 2.4.2. Read `AGENTS.md` and `.ai/roles/reviewer.md` first. Review only; do not edit
files, run builds that deploy, or call any other AI. Report defects with file:line, impact, and
proving code or decompile lines. Try to refute each claim you make. Mark anything you cannot verify
UNVERIFIED.

## Requested outcome

A player crash bundle (signature 9b2a103e, TAOM v2.0.34, Bannerlord v1.5.5.124170) shows a managed
`NullReferenceException` thrown inside vanilla `Helpers.HeroHelper.GetLastSeenText(Hero)`, reached
from `HeroVM.ExecuteLink` -> `EncyclopediaHeroPageVM..ctor` -> `RefreshValues` -> `Refresh`. The game
ended. The change must stop that crash without hiding unrelated faults.

## Change under review (working tree on `bannerlord-1.5.x`, base `429713746`)

- `Main/Features/Encyclopedia/Hooks/HeroHelper_GetLastSeenText_Patch.cs` (new Harmony finalizer)
- `Main/SubModule.cs`: the 4 added lines after `TryPatchCategory("Patch49_ArmyGatheringNreGuard")`
- `TAOM.Tests/Features/Encyclopedia/HeroHelperLastSeenTextGuardTests.cs` (new)
- Docs: the new `## Patch123_LastSeenTextGuard` section of `docs/reference/harmony-patch-registry.md`,
  added lines in `docs/features/encyclopedia.md`, `docs/migration/v1.5.5-impact.md`,
  `docs/reviews/lessons/adapters-taleworlds-api.md`

Use `git diff` and `git status` to see the exact hunks.

## Test evidence

`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`: 27,506 passed, 0 failed, 7 skipped
before the last three small edits; the 11 targeted tests (new tests, `PatchCategoryNumberTests`,
`HarmonyPatchBindingTests`) pass after them. Not run in game.

## Engine sources

Installed-DLL decompiles: run `pwsh tools/taom-src.ps1 path <Type>` (cache
`C:\Users\mikew\.taom-src\v1.5.5`). Browsable dump: `E:\Decompiled_Bannerlord\_categories_v1.5.5`
(and `_categories_v1.4.8` for the previous engine). Installed game:
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord`.

## Areas to examine

1. Is the root cause right? Which statements of `GetLastSeenText` (and anything it calls or the JIT may
   inline) can throw `NullReferenceException`, and in which game states? Are any reachable states not
   covered, or is the fallback text wrong for any of them?
2. Harmony behaviour: a value-returning finalizer with `ref TextObject __result` and
   `MethodBase __originalMethod`; writing `__result` after the original threw; the interaction with
   TAOM.Dependencies' PatchShield finalizer on the same target (`Dependencies/Foundation/PatchShield.cs`,
   `PatchShieldPolicy.cs`) and `RethrowStackPreserver`.
3. Apply timing: the category is applied in `OnSubModuleLoad`. Can patching (and so JIT-compiling)
   this vanilla method that early run a static initializer that fails before `Game.Initialize`
   (see `docs/reviews/lessons/harmony-il.md`, "A UIExtenderEx mixin compiles vanilla code at Register")?
4. Side effects vanilla performs before the throw (for example `hero.UpdateLastKnownClosestSettlement`)
   and whether swallowing leaves any state inconsistent.
5. Co-op and dedicated server: `docs/features/coop-interop.md`, `ServerLoadGateTests`.
6. Test quality: does each test fail when the behaviour breaks (two-edit test in `.claude/rules/tests.md`)?
   Static state across tests.
7. Accuracy of every engine claim in the four docs.

## Output

Findings grouped by severity (CRITICAL, HIGH, MEDIUM, LOW), each with file:line, impact, proof.
Then a list of claims you checked and found correct. Then UNVERIFIED items.
