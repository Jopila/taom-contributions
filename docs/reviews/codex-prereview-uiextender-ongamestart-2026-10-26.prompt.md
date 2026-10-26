# Codex pre-review: UIExtenderEx registration moves to OnGameStart (2026-10-26)

You are the reviewer (`.ai/roles/reviewer.md`). This is a review-only assignment: do not modify,
create or delete any file, do not run a build that deploys (`./build.ps1`), and do not commit. Read
`AGENTS.md`, `.ai/policy.md` and `.ai/review-reference.md` first. Every claim needs a source location
you read in this run; mark anything you cannot prove UNVERIFIED.

## The change

Uncommitted working tree on `bannerlord-1.5.x`, HEAD `f2e6efd23`. Run `git diff` to see it. Files:

- `Client/ClientSubModule.cs`: the behaviour change. `UIExtender.Create`, `ClientUiExtensions.Register`
  and `Enable` move from `OnSubModuleLoad` (now removed) to an override of
  `MBSubModuleBase.OnGameStart(Game, IGameStarter)`, guarded by `if (_uiExtender != null) return;`.
- `Client/ClientUiExtensions.cs`, `TAOM.Tests/Infrastructure/ClientIsolationGateTests.cs`: comments.
- `TAOM.Tests/Composition/ClientSubModuleTests.cs`: a source-text test and an IL caller test that pin
  `ClientSubModule.OnGameStart` as the only caller of `UIExtender.Create` and of
  `ClientUiExtensions.Register(UIExtender, Assembly)`.
- `docs/reviews/lessons/harmony-il.md` (new last lesson), `docs/features/shadowfax-screens.md`.

## Why (the claimed root cause, verify it)

UIExtenderEx 2.13.3 `ViewModelWithMixinPatch.Patch` patches each mixin target's declared
constructors, `OnFinalize` and named refresh method when `Register` runs, which compiles vanilla IL.
`ClanLordItemVM.UpdateProperties` and `EncyclopediaHeroPageVM.Refresh` (v1.5.5) read the static field
`CampaignUIHelper.SkillObjectComparerInstance`. On .NET Framework the JIT runs a beforefieldinit
type's static initializer when it compiles such a read. `CampaignUIHelper`'s initializer runs
`GameTexts.FindText("str_party_morale")` (`CampaignUIHelper.cs:210`), which throws while
`GameTexts._gameTextManager` is null, that is before `Game.Initialize` (`Game.cs:297-300`). The
runtime swallows the JIT-time throw and keeps the type failed: every later use throws
`TypeInitializationException`. A player-machine log showed three TAOM patch categories failing to
apply (Patch17_TroopWeight, Patch23_BannerColorPersistence, Patch105_HiddenHosts) and a crash at
character creation. `Game.Initialize` calls `GameType.OnInitialize` (`Game.cs:301`), and
`Campaign.OnInitialize` calls `GameManager.OnGameStart` (`Campaign.cs:1410`) before
`OnGameInitializationFinished` (`:1471`); `CustomGame.OnInitialize` calls `OnGameStart` (`CustomGame.cs:43`).

Engine signatures and bodies: the installed v1.5.5 DLLs are the authority (`pwsh tools/taom-src.ps1
path <Type>`, or the `ilspy` MCP server). The dump `E:\Decompiled_Bannerlord\_categories_v1.5.5\` is
for browsing. UIExtenderEx ships in
`E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM.Dependencies\bin\Win64_Shipping_Client\Bannerlord.UIExtenderEx.dll`.

## Known suspects (give each a verdict with evidence)

1. **Other poisoned initializers, later.** At `OnGameStart`, `GameTexts` exists, but in a campaign
   `OnGameStart` runs inside `Campaign.OnInitialize` before many objects exist (default skills and
   traits, models, campaign data). For the 17 mixin target ViewModels (`grep -rn "ViewModelMixin"
   Client`) and the methods UIExtenderEx patches on each, find every type whose static field their IL
   reads (include small static getters the JIT may inline) and check its static initializer against
   what exists at `OnGameStart`, in a campaign and in a custom battle. One unsafe initializer would
   poison its type in the same way.
2. **Missed targets.** Is any target ViewModel constructed, or any of the prefab-extension target
   movies (`grep -rn "PrefabExtension(" Client`) loaded or cached, before the client's first
   `OnGameStart` on any path (main menu, options, custom battle menu, new campaign and character
   creation, load from the main menu or the launcher's Continue, a BannerlordCoop client join)? Does
   UIExtenderEx handle a movie that Gauntlet loaded before `Register`?
3. **Consumers that assumed early registration.** PatchShield pass 1 (`Dependencies/SubModule.cs:249`)
   and pass 2 (`:319`); `PatchShieldPolicy` (`Dependencies/Foundation/PatchShieldPolicy.cs:40`); the
   Harmony census in `Main/SubModule.cs` (around `:1986-2000`); the `CoopPresence` probe inside
   `ClientUiExtensions.Register`; the log line "[CoopInterop] no co-op module detected at UI
   registration", which a comment says a boot matrix relies on.
4. **Second game in one process.** The guard skips registration on every later `OnGameStart`. Does
   anything need registration per game?
5. **Failure mode.** What does a throw from `Create`, `Register` or `Enable` do to a campaign start
   now, compared with a throw in `OnSubModuleLoad` before?
6. **Tests.** Would the new tests fail if registration moved back to `OnSubModuleLoad`? Are they
   tagged correctly for a no-game (`RefAsm`) run (`.claude/rules/tests.md`)?
7. **The lesson text** in `docs/reviews/lessons/harmony-il.md`: is every factual claim and line
   citation correct?

Also report any defect in the changed lines that these suspects do not cover.

## Output

For each finding: severity (P1 blocks the change, P2 should be fixed before commit, P3 minor), the
file and line, what is wrong, the evidence you read (file:line in TAOM or the decompiled engine), and
a concrete failure scenario. Then a verdict for each of the seven suspects (CONFIRMED SAFE, DEFECT, or
UNVERIFIED with what is missing). End with an overall verdict.
