# RCA: Good Deeds (deep review, 2026-10-20)

## Top-line

The `/deep-review` of Good Deeds (#864, relation gains for releasing prisoners, donating them to a settlement and winning
tournaments) ran seven lenses on the uncommitted worktree `E:\repos\taom-relations-politics`: 1 Standards, 2 Engine
compatibility, 3 Efficiency, 4 Completeness, 5 Data flow, 6 Design and 7 XML. It found one HIGH defect, three MEDIUM and
four groups of LOW findings. The HIGH one meant the most common release, handing a captive back on the party screen,
paid nothing. Every finding was re-verified against the installed v1.5.5 DLL decompile and fixed in the working tree;
the full suite then passed (20997 passed, 0 failed, 5 skipped).

Two of the eight findings are repeats of lessons that were already on file: reading what an event argument holds at its
raise site (finding 1, see the co-op gate entry of #886 in `campaign-mechanics.md`) and wording that agrees with the
speaker's gender (finding 6, first seen in the Tokens of Esteem review on 2026-10-19). A repeat check of
`docs/reviews/rca-*.md` and `docs/reviews/lessons/` for "party null", "raise site", "stale translation row" and
"gendered" found no earlier lesson about a null party on a release, and none about an ordering claim made without
reading `MbEvent`.

## Findings and root causes

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | A party-screen release raised `HeroPrisonerReleased` with a null party. The screen's transfer runs `Hero.OnRemovedFromPartyAsPrisoner`, which nulls `PartyBelongedToAsPrisoner`, before `EndCaptivityAction.ApplyInternal` reads it, so the main-party gate paid nothing (Lenses 5, 2) | Data flow; engine event argument assumed, not traced | The plan said "`ReleasedByChoice` plus party == `MainParty` covers the conversation and the party screen" without opening the party screen's raise site. The builder's tests fed the event a party | The adapter reads the raw `ReleaseSource` facts; a null party counts only while the active `PartyState`'s `RightOwnerParty` is the main party. New lesson in `campaign-mechanics.md`; a test per raise site |
| 2 | MED | A "let go" conversation in the player's own dungeon carried the settlement's party, so it paid nothing, a regression against #864's postfix design (Lens 5) | Data flow; gate too narrow | The gate was written for the main party and the other raise sites were not listed | Accept a settlement whose `OwnerClan` is the player's clan. Never accept a player-clan mobile party, because `MobileParty.RemoveParty` releases its prisoners `ReleasedByChoice` |
| 3 | MED | Changing the English "honour" to "honor" took 13 hand edits. No tool rewrites an existing row (harvest skips registered keys, `--sync-ids` only appends), and the paid translator queues a row only while it equals the English, so a stale row ships in English for good (Lenses 4, 7) | Localization; seeded key edited late | The seed tooling has no "rewrite" mode, and nothing compares a seeded row to the English it came from | Hand edits across all 12 files, then `GoodDeedsWordingTests`: each language row equals the current English or is a translation. Lesson in `localization-ui.md` |
| 4 | MED | The tournament step was placed inside TournamentRewards' listener, on a claim that a new `TournamentFinished` listener would disturb the load-bearing order. `MbEvent` puts each new listener at the head and invokes from the head, so the claim was wrong. The routing coupled the modules: a parked Good Deeds still paid, and a resolve failure would drop TournamentRewards' own behavior (Lens 6) | Design; ordering constraint asserted, not read | The claim was copied into the plan and the build from memory of "listeners are order sensitive" | Good Deeds owns its listener, wrapped in a try; TournamentRewards is back to trunk. Lesson in `gamemodels-services.md` |
| 5 | LOW | Docs and engine claims: the donation ceiling was "up to 40 for a king" (real: `sqrt(ransom)` x 0.5, 0.25 or 0.1, capped 40, 30 or 20, then x1 to x1.5, so at most 60, 45 or 30; a x2 setting can ask 130 before the 100 clamp); a donation was said to show no line (vanilla shows its quick notification); the checklist cheat `add_hero_relation` was described as "sets" (it adds, and lands on clan leaders); a "Harad" lord was listed as Neutral (`aserai` is evil in `execution/alignment.json`); the post-battle "free to go" release and the rival-quest bribe release were missing from Known Limits (Lenses 2, 4, 5) | Docs claim not read from the source | The numbers and behaviors were written from the plan, not from the decompile or the data | Each claim corrected from the decompile and the alignment file; Known Limits lists both extra release paths |
| 6 | LOW | Gendered MCM labels and hints ("his spouse", "his leader"); `SideMultipliers` shared a file with another class; no test that the hints quote the compiled defaults (Lenses 1, 4) | Standards; a REPEAT of the gendered-wording lesson | The localization lesson of 2026-10-19 sits in `localization-ui.md`, which the build never loaded | Neutral wording; the class got its own file; a hint-vs-default test |
| 7 | LOW | `(decimal)` of a float slider such as 0.6999999f made 5 x 0.7 round to 3 (Lens 6) | Float conversion before rounding | The slider stores a float and the multiplier was cast straight to decimal | Multipliers round to 2 decimals before use |
| 8 | LOW | Tests and docs: the slot-filler guard read `Main` twice and would fail on a reader of `Models.PrisonerDonationModel`; the Pair rows covered 7 of 9 pairings while the doc said nine; the tournament call's arguments were not pinned; the API snapshot `gamemodel-bases.md` was not regenerated; no co-op host-only row in the table; issue #864's text predates the approved plan (Lenses 3, 4) | Test shape and doc drift | Builder self-check; the guard was written against today's file layout | Guard reads each file once and excludes readers; all nine pairings; arguments pinned; snapshot regenerated; co-op row added; an as-built comment on #864 |

## Root-cause pattern

Findings 1, 2 and 4 share one habit: a constraint was stated from what a name or a mechanism suggests (a release with a
party, a listener that must not be added) instead of from the code that runs at that moment. `HeroPrisonerReleased`
carries a party argument, so the plan assumed it holds the party the prisoner left. It holds whatever
`PartyBelongedToAsPrisoner` is when `ApplyInternal` reads it, and on the party screen that is already null. The same
plan claimed a new `TournamentFinished` listener would break the order of the existing one, when the event's own
`AddNonSerializedListener` makes the order predictable (newest at the head). Both were cheap to check and expensive to
get wrong: one shipped a feature that paid nothing on its main path, the other coupled two modules.

Finding 3 is a tooling gap, not a habit. The localization tools assume English is final before seeding. A one-word
English edit after seeding has no tool, so a per-feature test is the only guard.

## Why each agent missed these

- **The builder** worked from an approved plan that named the event and the gate but not the raise sites, and that
  asserted the `MbEvent` ordering constraint. Its tests fed the adapter a party, so the null-party path never ran. The
  slot-filler guard and the Pair rows were written to pass today's files, not to pin the rule.
- **Lens 1 (Standards)** found the gendered labels, the shared file and the missing hint test (6, 8).
- **Lens 2 (Engine)** re-read the decompile and found the null party on the party screen (1), the real donation
  formula, the notification and the extra release paths (5).
- **Lens 3 (Efficiency)** found no runtime cost; it found the slot-filler guard reading `Main` twice, and the reader
  of `Models.PrisonerDonationModel` that the guard would wrongly fail (8).
- **Lens 4 (Completeness)** found the stale language rows the "honor" edit would leave (3), the gendered hints (6), the
  missing snapshot, co-op row, Pair rows and issue record (8), and moving the release decision into the service (1, 2).
- **Lens 5 (Data flow)** found the null party (1), the player's own dungeon (2), the stale donation text (5) and the
  quest bribe release (5).
- **Lens 6 (Design)** found the tournament step coupling TournamentRewards (4) and the float rounding (7).
- **Lens 7 (XML)** found the missing backlog entry for the new key and chose the "honor" spelling (3).
- **No lens missed a confirmed finding.** The HIGH finding was reached by two lenses independently.

## Feedback memories to codify

None. The rules are in `campaign-mechanics.md`, `gamemodels-services.md`, `localization-ui.md` and `testing-qa.md`.
