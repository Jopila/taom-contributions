# RCA: Lord Banners (#823, deep review 2026-10-19)

## Top-line

The working-tree deep review of Lord Banners (six lenses: standards, engine compatibility, efficiency,
data flow, completeness, design, then one convergence pass) found no HIGH and no current defect in the
feature's behaviour. It found 2 MEDIUM and 13 LOW findings plus one process finding. Every finding in the changed code
was fixed in the same session; F7 is documented, and F15 records two existing gaps as follow-ups. The one that
matters beyond this feature: by giving TAOM lords banners, the change moves their formations from
TAOM's own banner call, which checks heraldry first, onto vanilla's captain call, which checks nothing.
The data that keeps that safe today was not pinned; a shipped-data gate now pins it. The in-game check
is owed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | Since #823, `GeneralsAndCaptainsAssignmentLogic.OnCaptainAssignedToFormation` calls `SetFormationBanner` for every TAOM lord-led formation (AI teams included), and `BannerBearerAssignmentMissionLogic` then skips them. TAOM's heraldry guard against the native banner-tableau crash (`rca-banner-bearers-siege-ctd-2026-07-23.md`) no longer runs for them. Safe today only because every clan and kingdom `banner_key` parses to at least one layer, and nothing pinned that | Guard bypassed by rerouting | The plan traced the model's reader (the give path) but not where the produced value is consumed downstream, and so not which TAOM guard sat on the old consumer. Caught by the orchestrator before the lenses, confirmed by engine compatibility and data flow | `ShippedHeraldryBannerKeyTests`: every `banner_key` in `clans.xml`, `taom_spkingdoms.xml`, `spclans.xslt` and `spkingdoms.xslt` is whole groups of ten integers, and each file must yield keys. Lesson in `lessons/adapters-taleworlds-api.md` |
| F2 | MEDIUM | Issue #823 described the planned design (BannerItems paths, "wraps the previous model", neutral for everyone, "behind an adapter") | Docs drift | Filed before the plan | Body rewritten |
| F3 | LOW | The doc said existing saves get banners over days; `GiveBannersToHeroes` also runs on `OnGameLoadFinishedEvent`, so they get one at load (prisoners included) | Engine research gap | Research read the give methods, not the behavior's `RegisterEvents` | Doc fixed. Lesson: read `RegisterEvents` before stating when a behavior acts |
| F4 | LOW | The doc named `HeroCommissionAdapter.cs:83` as a writer that clears a lord's banner; that hero is never a lord. The real clearers are vanilla's ruling-clan kit change and the Hard Questions strip | Unchecked grep result | A `FillFrom` grep hit was reported without tracing the hero's occupation and clan | Doc fixed |
| F5 | LOW | Code comments, `banner-bearers.md`, the 2026-07-23 RCA and a lesson said vanilla banners only player-team Order of Battle formations; it banners every team's captained formations | Old premise, never rechecked | Pre-existing text this feature depends on | Corrected, with dated correction lines in the RCA and the lesson |
| F6 | LOW | `BannerBearerConfig.cs` and the BannerBearers JSON comment said every vanilla banner is `neutral_culture` (6 per vanilla culture and 9 neutral) | Old claim | Pre-existing | Corrected |
| F7 | LOW | With BannerBearers `"Enabled": false`, the race gate falls back to vanilla's humanoid check, so a troll can carry a Mordor lord's banner | Interaction | New only because TAOM lords now carry banners | Documented (cosmetic, JSON-only switch) |
| F8 | LOW | The comment rewrap in `BannerBearerAssignmentMissionLogic.cs` pushed the file to 151 lines and stated a wrong condition; the first fix kept 150 lines with a 165-column line | Standard | Line count not checked after a comment edit | Rewritten at normal width, 150 lines |
| F9 | LOW | The test that reads the game install had no `LiveInstall` tag | Standard | Convention missed | Tagged |
| F10 | LOW | No test for a vanilla banner culture with a map row | Test gap | Matrix built from the shipped data only | Added |
| F11 | LOW | `ShippedConfig_EveryValueIsAVanillaBannerCulture` could not fail (the provider drops such rows; two other tests catch them) | Dead test | Written beside the validator without asking what it adds | Deleted after a RED proof that the other two fail |
| F12 | LOW | `BannerFacts`, a type holding one expression with one caller, restating the engine's `ItemObject.BannerComponent` | Over-structure | Copied an adapter shape the case did not need | Folded into a lambda in the model |
| F13 | LOW | The duplicate-key wording promised a warning for exact duplicates, which the JSON reader collapses first | Docs | Same wording slip as Voice Accents had before its convergence pass | Wording fixed in the summary and the doc |
| F14 | LOW | Doc gaps: no Design Challenge, wrong issue title, the heraldry gate missing from Tests, the vassal-reward gap unrecorded; the gate's comment called its rule the engine's when it is stricter | Completeness | Normal review yield | Fixed |
| F15 | LOW | Lindon and Blue Craig have no BannerBearers `CultureBanners` row, and `DefaultVassalRewardsModel` still filters by the kingdom's culture | Existing gaps | Outside this change | Documented as follow-ups; issues on Mike's word |
| F16 | Process | The builder wrote the code before the tests | TDD | Builder skipped RED | Mutation checks afterwards: breaking the neutral rule fails 7 tests, breaking the vanilla-unchanged rule fails 1 |

## Root-cause pattern: a guard that protects TAOM's call, not the engine's

F1 is the second time this exact call has bitten. The 2026-07-23 RCA's rule was that widening an
engine call reopens the preconditions vanilla relied on; it added a guard to TAOM's own
`SetFormationBanner`. This change did the mirror image: it handed the same formations to vanilla's
`SetFormationBanner`, where TAOM's guard does not reach. A runtime guard on one call site protects that
call site only. When a feature moves data onto another path to the same engine call, the guard's
precondition has to hold in the data, so the data needs the gate.

## Why each lens caught or missed these

- **Engine compatibility (2), data flow (5):** both answered F1 with the full chain and a data audit
  (272 keys, 0 malformed) and found F3, F4, F5 and F7.
- **Standards (1):** F8, F9, F10. **Efficiency (3):** no issue; it found F3 too.
- **Completeness (4), design (6):** F2, F11, F12, F14, F15, on the tree with wave 1 fixed.
- **Orchestrator:** raised F1 before the review from the BannerBearers comment, so the lenses could
  settle it instead of discovering it.

## Feedback memories to codify

None. The F1 rule is a lesson and a gate; F3 is a lesson.
