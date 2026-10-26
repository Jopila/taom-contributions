# RCA: UIExtenderEx registration broke CampaignUIHelper at boot, 2026-10-26

**Summary.** TAOM registered its UIExtenderEx extensions in `ClientSubModule.OnSubModuleLoad`. Registration
patches each mixin target's constructors, `OnFinalize` and refresh method, which compiles the vanilla IL. Since
2026-10-23 two targets, `ClanLordItemVM.UpdateProperties` and `EncyclopediaHeroPageVM.Refresh`, read the static
field `CampaignUIHelper.SkillObjectComparerInstance`. When the JIT compiled them it ran `CampaignUIHelper`'s
static initializer, whose `GameTexts.FindText("str_party_morale")` (`CampaignUIHelper.cs:210`, v1.5.5) threw
because `GameTexts` is null until `Game.Initialize` (`Game.cs:300`). The runtime swallowed that throw and kept
the type failed, so every later use threw `TypeInitializationException`. Both launches on 2026-10-26 crashed
when character creation opened its first narrative stage; one of them also lost three patch categories. The fix
moves `Create`, `Register` and `Enable` to a once-only `ClientSubModule.OnGameStart`, which every game type calls
from inside `Game.Initialize` after `GameTexts.Initialize`.

Review: risk tier 1, one wave. Codex (gpt-6-astra, ultra) plus the Data flow and Engine compatibility lenses.
No CRITICAL, HIGH or MEDIUM; no engine incompatibility. Eight LOW findings, all about text, a test and stale
references, were confirmed and fixed in the same change. One LOW finding is recorded and not changed.

Sources: the `/investigate` debug report in the session of 2026-10-26; TAOM logs
`taom_debug_2026-10-26_16-34-58.log` (lines 33, 201, 394-411) and `taom_debug_2026-10-26_16-37-09.log`
(lines 197, 209, 226, 436-455); Codex output `docs/reviews/raw/codex-prereview-uiextender-ongamestart-2026-10-26.md`
(local, ignored), prompt `docs/reviews/codex-prereview-uiextender-ongamestart-2026-10-26.prompt.md`.

## The original bug

| Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| HIGH | Registration in `OnSubModuleLoad` ran `CampaignUIHelper`'s initializer before `GameTexts` existed; the type stayed failed and character creation crashed | Initialization order | The mixins that read `CampaignUIHelper` statics arrived on 2026-10-23 (247e04278, f61a97bf1) and 2026-10-26 (d8e6333ce). Their reviews checked each mixin, not the boot phase of the registration that compiles its target. No campaign ran in game on the desktop between 247e04278 and the 16:34 launch on 2026-10-26: its TAOM logs of 2026-10-23 end at 14:22, before that commit, none exist for 2026-10-24 or 2026-10-25, and the two launches before 16:34 on 2026-10-26 wrote 5 KB and 21 KB logs that never reached a campaign. The NRE is first-chance and swallowed, so nothing shows it without a debugger. | Registration at the client's first `OnGameStart`; `ClientSubModuleTests` pins `OnGameStart` as the only caller of `UIExtender.Create` and `ClientUiExtensions.Register`; lesson in `docs/reviews/lessons/harmony-il.md` |

This is the second time the mechanism has shipped. `MovementOrder`'s initializer read `Mission.Current` when a
patch compiled at `OnSubModuleLoad` (2026-05-07, the lesson above the new one in `harmony-il.md`). That lesson was
written for TAOM's own patch categories, so it did not fire for UIExtenderEx registration, which applies Harmony
patches as well.

## Review findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The code comment and the lesson said `OnGameStart` runs after `Game.Initialize`; it runs inside it, after `GameTexts.Initialize` and before the models and default objects | Engine claim | The call chain was read to the first line that answered the question (`Game.cs:300`), not to the end of `Game.Initialize` | Corrected; the lesson names what does not exist yet at `OnGameStart` and says a mixin that needs those requires a later hook |
| 2 | LOW | The comments said the JIT "then runs" the initializer; .NET permits it, and it also follows small inlined callees | Engine claim | Wrote one machine's observation as a rule | Wording scoped to what was observed; the Prevent line covers inlined static getters |
| 3 | LOW | The co-op registration log line moved from boot to the first game start, but the boot matrix stops at the main menu | Drift | The comment that names the boot matrix was in a file the change touched only for a comment | `ClientUiExtensions.cs` comment and `bannerlord-together-compat.md` boot matrix say a launch that checks the line must start a game |
| 4 | LOW | A test comment said the UIExtenderEx refusal shows nothing in game; at `OnGameStart` it also posts a red chat message | Engine claim | Copied from the B5 comment, which was true for `OnSubModuleLoad` | Corrected |
| 5 | LOW | Five places still named `OnSubModuleLoad` as the registration point or used the old test name: `Main/SubModule.cs:246`, `CoopSuppressedUiAttribute.cs:15`, `bannerlord-together-compat.md:205`, `taom-headless-server.md:246`, `build-tooling-workflow.md:4362` | Drift | The search for stale references ran on `ClientSubModule.OnSubModuleLoad` only, not on "registration" plus `OnSubModuleLoad` | Fixed |
| 6 | LOW | The lesson presented the three failed patch categories as the symptom; the 16:34 launch applied every category (`failed=0`) and still crashed | Incomplete evidence | Only the newest log was read | The lesson describes both launches and says failed categories are not a reliable sign |
| 7 | LOW | The lesson's "Why missed" omitted `EncyclopediaHeroLevelHeaderMixin` (d8e6333ce), which also patches `EncyclopediaHeroPageVM.Refresh` | Incomplete evidence | Listed the mixins from one commit range | Added |
| 8 | LOW | The source-text test also failed on internal-only edits (parameter names, the exact guard shape) | Test strength | Copied the exact-body pattern of the test it replaced | It now checks that the guard comes before `Create` and that `Create`, `Register` and `Enable` follow in order; the IL test pins the caller |

**Not changed, recorded (LOW, Data flow 12 and Codex suspect 5).** A throw from `Create`, `Register` or `Enable`
now escapes `OnGameStart`, which `MBGameManager.OnGameStart` does not catch (`MBGameManager.cs:159-168`).
Patch37's finalizer on `Module.OnApplicationTick` swallows it, and `_uiExtender` is set before `Register`, so a
later game skips registration. Before the change, the engine caught a throw in `OnSubModuleLoad` and the game
did not start. No registration throws today, and a retry is not a valid recovery, because UIExtenderEx adds the
module to its instances before it registers the runtime (`UIExtender.cs:90-97`). A guard here would be one for a
speculative failure, which `simplicity-criterion.md` leaves to Mike; it is listed for him in the review report.

**Information only (Data flow 11).** TAOM's UIExtenderEx runtime now registers after every other mod's
`OnSubModuleLoad` registration. For a view model that another mod also extends, that mod's Harmony id then owns
the shared patches and its mixins run first. No module in the current load order extends any of TAOM's 16
target view models.

## Why the reviewers found these

- Codex and both lenses found #1 independently by reading `Game.Initialize` to its end.
- Data flow found #3, #5, #6 and #8: it followed the log line to the boot matrix, searched every file for the
  old registration point, read both logs of the day, and ran the two-edit test on the new tests.
- Engine compatibility found #4 and #7: it read UIExtenderEx's `MessageUtils` and Native's chat log setup,
  and listed every mixin on the two affected view models.
- No lens found a second unsafe initializer. Codex and both lenses each scanned the IL of every patched
  method on the 16 target view models, with inlined callees, and found `CampaignUIHelper` as the only
  initializer that reads game state.

## Lesson

A registration that applies Harmony patches, UIExtenderEx's included, compiles its targets, and that compile
can run static initializers. Choose its phase by reading those initializers, and pin the phase with a test
(`docs/reviews/lessons/harmony-il.md`, "A UIExtenderEx mixin compiles vanilla code at Register").
