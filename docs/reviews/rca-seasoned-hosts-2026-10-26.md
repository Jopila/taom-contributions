# RCA: Seasoned Hosts, AI lord training and skill XP (#890, deep-review 2026-10-24 to 2026-10-26)

## Top-line

`/deep-review` ran seven lenses (1 Standards, 2 Engine, 3 Efficiency, 4 Completeness, 5 Data flow, 6 Design, 7 XML) on the
uncommitted #890 change in the `feat/recruitment-garrisons-890` worktree, then one convergence pass after the fix round.
Nothing was HIGH. The first round found 4 MEDIUM and about 15 LOW findings; the convergence pass found one more MEDIUM that
the fix round itself introduced (MCM hints that described the co-op rule backwards). All were fixed before commit, with no
runtime change beyond the approved co-op deferral of the two bonuses.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Under a co-op mod TAOM cannot probe (BannerlordTogether), other human players' clans read as AI, so they would get the training and skill XP bonuses meant for AI lords. | Player-identity gate without a co-op fallback | The plan relied on `IPlayerControlQuery` alone; that query only knows BannerlordCoop's registry, and nobody asked what it answers under a co-op mod it cannot read. | Both bonuses also require `!ICoopSessionProvider.ShouldDeferToHost` (true for every peer under an unprobed co-op mod); tests pin it. Lesson in `lessons/gamemodels-services.md`. |
| 2 | MED | The settings provider sent a NaN or out-of-range value to the neutral answer instead of the compiled default, and clamped in a private helper. | Settings validation | Hand-rolled clamp instead of the shared `SettingClamp`. | Per-field `SettingClamp.Clamp` accessors with compiled defaults; the settings type was deleted. |
| 3 | MED | `CoopPlayerControlQuery` used `MethodInfo.Invoke` (about 0.2 µs and 104 B per call in a session), and its own comment required a compiled delegate before any hot caller. Seasoned Hosts asks it on every skill XP grant. | Shared code's documented precondition | The plan reused the query without reading its preconditions. | The probe compiles once to a `Func<object,bool>`, with `Invoke` as the fallback for another shape; a LiveInstall test binds it to the installed BannerlordCoop. |
| 4 | MED | The Loremaster's Line doc said the learning floor moves the marker. It does not: vanilla's rate falls in steps of 0.125, so the first level at or under 0.05 is where the rate already reached 0. | Doc claim from the plan, not the engine | The marker claim was reasoned, not computed. | `LearningFloorCalculatorTests` rows pin the same marker level with floor 0 and 0.05. |
| 5 | MED | Convergence: the fix round's MCM hints said both bonuses and the steward skip are "off in a co-op session". The code defers only on clients: a BannerlordCoop host applies them, and the unprobed co-op mod is the case TAOM detects but cannot read, not "cannot detect". 13 string rows and the feature doc carried the wrong text. | Player text written from a summary of the gate | The hint was rewritten from the brief's one-line description of `ShouldDeferToHost`, not from `CoopSessionPolicy.ShouldDeferToHost`'s truth table. | Hints, strings, 12 seeds and doc rewritten to sibling wording ("only the host's value applies"; "a co-op mod TAOM cannot probe"). Lesson in `lessons/localization-ui.md`. |
| 6 | LOW | Unused training label: both engine consumers read only the result, so no screen shows an explainer line. | Dead text | Label added by habit. | Key deleted from 13 files; the doc names the reason. |
| 7 | LOW | Adapter source pins passed vacuously when a token was absent (IndexOf -1). | Test vacuity | Repeat of #888 finding 7. | `AssertPrecedes` asserts each index is >= 0. |
| 8 | LOW | `Patch118` is a bool prefix but sat outside the pinned co-op veto set; the test that excluded it was renamed and the array extended. | Co-op classification | The new patch was classified in prose, not in the pinned array. | `GatedVetoes_AreExactlyThePinnedSet`; two docs that named the old test name updated in convergence. |
| 9 | LOW | GroupOrder 72 was taken on trunk by another feature. | Shared ordinal | Checked only against the branch base. | Moved to 73; a test asserts it appears once. |
| 10 | LOW | Docs: the over-limit drop is 1.375, not 1.25 (`DefaultCharacterDevelopmentModel.cs:240`); companions made lords get the bonus; the 2.5 percent Leadership spillover; registry table break; coop-interop row; BannerlordTogether row; roadmap slot; lord-skills cross-reference; "the floor moves no replicated state" (it scales the skill XP `AddSkillXp` writes; BannerlordCoop lets only the host write). | Doc claims | Written from the plan. | Each corrected against the v1.5.5 cache. |
| 11 | LOW | Tests: no behavioural Patch118 tests, wiring gaps, test names without the method segment, comments inside test bodies. | Test gaps | The brief listed decision tests only. | `Patch118ProjectStewardXpTests` (5), wiring tests, renames. |

## Root-cause pattern

Findings 1, 5 and 10's replicated-state claim share one shape: a co-op statement written from a one-line summary of the gate
("defers in co-op") instead of the gate's full truth table (host, client, solo, unprobed mod). Finding 5 is the same mistake
made again by the fix round, one day after finding 1 was fixed.

## Why each lens caught what it did

- **Standards (1):** findings 2 and 7. **Engine (2):** 4, 6 and most of 10. **Efficiency (3):** 3.
- **Completeness (4):** 9, the issue body and the doc rows in 10. **Data flow (5):** 1. **XML (7):** the 13-file edit for 6.
- **Design (6):** no change beyond the shapes above. **Convergence:** 5, two stale test-name references and the replicated-state claim.

## Feedback memories to codify

None. The lessons carry the rules.
