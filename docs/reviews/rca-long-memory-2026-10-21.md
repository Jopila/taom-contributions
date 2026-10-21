# RCA: Long Memory (deep review, 2026-10-21)

## Top-line

The `/deep-review` of Long Memory (#865, relation loss with a village's notables when the player raids it, burns it or
takes goods by force) ran seven lenses on the uncommitted worktree `E:\repos\taom-relations-politics`: 1 Standards,
2 Engine compatibility, 3 Efficiency, 4 Completeness, 5 Data flow, 6 Design and 7 XML. It found no HIGH defect, three
MEDIUM and seven groups of LOW findings. The costliest was a wrong unit: the penalty scale treated `RaidDamage` as a
share of full health, so a village weakened by a forced taking cost about 13 instead of 50 (it now pays about 10, by design, because that forcing was already charged). Every finding was re-checked against the code or
the decompile and fixed in the working tree; the full suite then passed (23006 passed, 0 failed, 5 skipped) and the
binding gate passed (722 passed).

One of the ten findings is a repeat of a lesson already on file. Finding 3, a past-tense line with the player as its
subject, is the third gendered-wording defect in a row (#863, #864, #865); finding 7, a second copy of Good Deeds'
grouping code, is a first. A repeat check of `docs/reviews/rca-*.md` and `docs/reviews/lessons/` found the gendered
lessons in `localization-ui.md` ("Check new player text for speaker and player gender", #863, and "Gendered wording
shipped again", #864), and found no earlier lesson about copying a sibling feature's per-owner grouping or about a
health-scale unit. The lessons for this review are already filed under #865 in `campaign-mechanics.md` and
`localization-ui.md`; this RCA cites them and adds none.

## Findings and root causes

| # | Sev | Finding | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MED | `RaidDamage` is hit points removed on the 0..1 health scale, capped at 1, not a share of full health. A village weakened by a forced taking gave 0.2 at most, and the 0.25 floor made a burned village cost about 13, not 50; the docs and a comment claimed a share (Lens 2) | Engine unit assumed; docs claim not read from the source | The plan named a "share of health" from the field's name, and the tests fed the service whole numbers it chose itself | Scale is `RaidDamage` alone, no floor; docs and comment corrected. Lesson under #865 in `campaign-mechanics.md` |
| 2 | LOW | A raid interrupted by a relief lord is two raid map events; with the floor each paid at least a quarter, up to about 63 in place of 50 (Lens 5) | Data flow; one raid is not one event | The builder modelled a raid as a single event | The same no-floor rule: the events of one raid sum to at most about one hit (0.05) more than 1, so the total stays within a few points of the base |
| 3 | MED | The coercion line made the player the subject of a past-tense verb ("what you took by force"), which takes the player's gender in Polish. REPEAT of the #863 and #864 lessons (Lens 7) | Localization; a REPEAT of `localization-ui.md` | The lesson file was on disk but the build never loaded it, and English has no gendered past tense to show the problem | Reworded so the villagers are the subject, by hand in 13 files. Lesson under #865 in `localization-ui.md`; run the gender check on every new line before seeding |
| 4 | MED | The adapter built all village facts (name, sides, a list and an owner lookup per notable) for every AI raid in the world, then the service discarded them (Lens 3) | Efficiency; work done before the gate | Facts were gathered in the adapter and the "player leads" gate lived in the service | Early return in the adapter when the player does not lead |
| 5 | LOW | The raid route read the village before the gates, while the menu route did not (Lens 5) | Data flow; two routes, two orders | The routes were built one after the other and never compared | Every route passes a reader that the service calls after its gates |
| 6 | LOW | The two no-resist menu ids were matched by name with no binding gate (Lens 2) | Engine binding unguarded | Menu ids look stable and the binding gate lists only patches and models | `LongMemoryBindingTests` reads the `AddGameMenus` IL for both ids |
| 7 | LOW | Long Memory copied Good Deeds' per-owner grouping, multiplier helper and `DeedHeroOf`, and imported Good Deeds types (Lenses 1, 6) | Design; copy of a sibling feature | The builder used Good Deeds as a template and the shared pieces had no shared home | `RelationTarget`, `RelationAsk`, `GrantStrongestPerOwner` and `SideMultipliers` moved next to `IRelationGrantService`; `RelationTargets.Of` in Adapters; Good Deeds migrated, its tests unchanged apart from the type |
| 8 | LOW | The docs said the per-owner grouping merges a notable with the owner's leader. Vanilla notables have no clan, so it never does; it is a guard only (Lenses 2, 6) | Docs claim not read from the source | The sentence was carried over from Good Deeds, where heroes do have clans | Docs corrected; the checklist step that tested the merge was deleted |
| 9 | LOW | Docs: enlisted shore leave can force a village (the doc said every enlisted case was excluded); the stacking table omitted vanilla's peace-time consequences (`BeHostileAction` `ApplyGeneralConsequencesOnPeace`); TAOM's same-side war veto changes what checklist step 1 can set up; a known limit described a case the menu blocks (Lenses 2, 5) | Docs claim not read from the source | Behavior was written from the plan, not from the decompile | Each claim corrected against the decompile and the veto code |
| 10 | LOW | Records and tests: a stale count in `bannerlord-together-compat.md`, a single pipe in the co-op row, `relations.md` wording, a missing INDEX line, misnamed tests and one that could not fail, multi-Act tests, untested branches (raid switch off still lets coercion cost), the hot-path provider list missing Long Memory and Good Deeds, and the #865 body still the plan (Lenses 1, 4, 5) | Test shape and doc drift | Builder self-check; the counts and lists were written once and not re-derived | Counts recomputed, rows fixed, tests renamed and split, the missing branch pinned, both features added to the provider list, an as-built comment on #865 |

## Root-cause pattern

Findings 1, 2, 8 and 9 share one habit already seen in the Good Deeds review: a claim was written from what a name or a
neighbouring feature suggests instead of from the code that runs. `RaidDamage` sounds like a share, so the scale was a
share; a raid sounds like one event, so one event was budgeted; per-owner grouping merged heroes with leaders in Good
Deeds, so the same sentence went into a feature whose notables have no clan. Each was cheap to check and wrong in
effect: for a village weakened by a forced taking, the burned-village penalty was a quarter of its documented size.

Findings 3 and 7 are the cost of a template. Good Deeds supplied the pattern and the code, and Long Memory took it
whole: the grouping logic and its wording rules. The gendered line is the third repeat in three features because the
lesson sits in a file the build does not read, and nothing machine-checks a new `{=KEY}` line for a player-subject
past tense. Finding 7 shows the other side: a second consumer is when shared code gets a shared home, and the move
happened in review instead of in the build.

## Why each agent missed these

- **The builder** worked from a plan that said "share of full health" and that treated a raid as one event. Its tests
  used round numbers it chose, so the 0.2 ceiling and the two-event case never ran. It copied the Good Deeds types and
  sentences instead of lifting them out.
- **Lens 1 (Standards)** found the Good Deeds copies and imports (7) and the record drift (10).
- **Lens 2 (Engine)** re-read the decompile and found the `RaidDamage` unit (1), the unguarded menu ids (6), the
  clanless notables (8) and the peace-time consequences and shore-leave case (9).
- **Lens 3 (Efficiency)** found the per-raid fact building for every AI raid (4).
- **Lens 4 (Completeness)** found the stale count, the missing INDEX line, the provider list and the plan-only issue
  body (10).
- **Lens 5 (Data flow)** found the two-event raid (2), the route-order mismatch (5) and the stacking and test gaps (9, 10).
- **Lens 6 (Design)** found the sibling-feature copy (7) and the guard-only grouping (8).
- **Lens 7 (XML)** found the gendered coercion line in the 12 translated files (3).
- **No lens missed a confirmed finding.** Findings 1 and 2 were reached by different lenses from different angles, and
  the same no-floor rule fixed both.

## What proves it

- The full suite: 23006 passed, 0 failed, 5 skipped.
- The binding gate: 722 passed, including `LongMemoryBindingTests` for the two no-resist menu ids.
- The Good Deeds tests pass unchanged apart from the moved types, which shows the shared extraction kept its behavior.

## Feedback memories to codify

None. The rules are in `campaign-mechanics.md` and `localization-ui.md` under #865.
