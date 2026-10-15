# RCA: porting KEYforce's Harad regular weapons (lotraom-assets db981663, #805, 2026-10-15)

## Top-line

KEYforce's `db981663` ("Harad regular weapons, bow material/shader fixes, troop names") gave the Harad and Shaghana
regular lines their own spears, sword, shields, arrows and longbows, four battle-roster variants per regular troop,
and `[Harwan]`/`[Shaghana]` names. Three read-only reviewers (TAOM data, Armory, integration) found his XML clean as
XML and listed the problems in #805: shader-switched bow materials no release carries, a Gondor armour re-stat below
TAOM's curve, raw longbows that bypassed the ranged ladder, spears left out of `TwoHandedPolearm_Couchable`, renames
that staled 12 languages, and 50 unregistered names.

The port then went through `/deep-review`: wave 1 (Engine compatibility, Data flow, XML, Tooling), wave 2
(Completeness, Design), all `deep-reviewer` on Opus 5.5 at max effort. **No engine incompatibility**: the Couchable
edit is correct and sufficient on the managed side, the career kit items equip, and no player path hands out the
non-merchandise longbows. Four MEDIUM and two HIGH findings below. The port fixes every finding in its own data; the
HIGH scratch-proof gap (2) and the pre-existing loc gate (6) go to follow-up issues.

## Mike's decisions

| # | Question | Answer | Applied as |
|---|---|---|---|
| D1 | Fix what the review of his commit found | "Please fix all that you found with KEYforce" | This port |
| D2 | His shader-switched bow materials, already live | "Keep as-is" | Left live; known limitation |
| D3 | His Gondor re-stat | (port default, reversible) | Not installed; TAOM's curve stays |
| D4 | The Harad ranged ladder | "Do #803 now with the tools" | Tier-7 cell added; `rebalance_ranged_ladders.py` placed every Harad archer and raised 12 Bow skills; inversions 398 to 0 |
| D5 | The Shaghana and Abanissa enlistment kits | "Regenerate, keep steppe bows" | 29 kits reseeded; steppe bows re-applied |
| D6 | 8 melee warnings on his spears | "Leave visible + issue" | Kept; #812 |
| D7 | Career kits | The rule (#629), as for #798 | 16 slots now carry his spear, sword and shield |

## Findings

| # | Sev | Bug | Found by | Category | Why missed | Preventive action |
|---|---|---|---|---|---|---|
| 1 | MED | The first bow fix put the six regular archers on their tier cells while the Serpent archers sat two cells low, so the regulars out-shot them and the in-line Harad inversions rose | Tooling, Data flow, XML | balance | **Repeat** of the lesson "A balance rule is verified by measuring the property it promises, never by the case that motivated it" (`lessons/data-content-cultures.md`, from the previous KEYforce port): the fix was checked against the case that motivated it (the tier-7 marksman) and not against the property (no lower tier beats a higher one in the line). The validator was run, but the warning diff against HEAD was not read | D4: the line's own tools. Lesson: diff the whole warning set against HEAD for every troop file a port touches |
| 2 | HIGH (tool) | The scratch merge proof hard-coded which side changed which field, so it would pass wrong merges (six synthetic ones did) | Tooling | tooling | **Repeat** of `rca-keyforce-lamedon-port-2026-09-25.md` finding 2: a check that shares the author's assumption can only agree. The data is right: an independent per-field three-way merge reproduced it | #813: a repo tool that computes each field from the base |
| 3 | MED | TAOM's three edits to his live files (Couchable rows, non-merchandise longbows, 50 English rows) had no in-repo gate; his next drop reverts them | XML, Data flow, Engine, Completeness | data | The "Unversioned modules" trap names the rule; the port wrote the edits and not the gate | `HaradWeaponsLiveEditTests` (fails 3 of 3 on his committed files) and an APPLIED EDIT record |
| 4 | MED | Issue #805's body stated results before they existed: the longbows and art "not installed", the blades "restatted" | XML, Data flow, Engine | evidence | **Repeat** of the lesson "A commit message's claimed deltas are part of the diff" (`lessons/build-tooling-workflow.md`) and of `evidence-over-claims.md` §C1 (writing the summary before its evidence exists): the body was written from the plan | Body corrected to the decisions as made |
| 5 | MED | The 29 Shaghana and Abanissa enlistment kits still issued the old vanilla weapons after their donor troops changed | Completeness | data | **Repeat** of `rca-harad-repair-2026-10-15.md` finding 13: a troop change was followed by every generator the commit hook runs, and the enlistment generator is not one of them (it has no `--verify`) | D5; the enlistment doc now says reseed after a donor change; `--verify` for the generator in #804 |
| 6 | HIGH (pre-existing) | `check_external_loc_coverage.py` passes a tree it cannot read (a missing or malformed file counts as translated) and names the wrong module in its hint | Tooling | tooling | No test file for the gate | #815 |
| 7 | LOW | The Couchable rows sat under KEYforce's own comment, so a later merge could not tell them apart | Completeness | data | | `TAOM-HARAD-COUCH` START/END markers |
| 8 | LOW | Armory catalogue and audit report not regenerated; the file catalogue, armory guide and release process did not know `harad/weapons.xml`; a doc line credited "the lowest Harad troops" | XML, Completeness, Data flow | docs | | Regenerated and corrected |
| 9 | LOW | KEYforce's own `harad/weapons.xml.bak` (synced from the mirror) sits beside the new file | Tooling, XML | data | | Recorded in the APPLIED EDIT section, left for the release sweep |
| 10 | LOW (pre-existing) | `validate_xml_schemas.py` labels a registered XSLT "never read" | XML | tooling | | #804 |

Design notes (not defects): the 8 melee warnings are 6 gate artefacts (the melee pass prices Armory items only, and
those troops also carry an in-band vanilla sword) and 2 horsemen whose spear equals the vanilla spear it replaced;
troops shared by Shaghana, Abanissa, Umbar and the Haradrim Raiders now read `[Harwan]` (a naming decision for Mike
and KEYforce); his light spears are short for lances (1.55 m against vanilla's 1.99 m couchable lance).

## Root-cause pattern

**Every MEDIUM finding repeats a recorded lesson.** Findings 1, 4 and 5 each have a lesson or RCA row naming the
same mistake, and finding 3 is a trap-index line. The lessons were written; the port was run without re-reading them.
For an artist-commit port the remedy is a tool that carries them (the follow-up port tool) and a checklist the
reviewer runs, not a fourth lesson saying the same thing.

## Why each agent missed these

| Agent | Missed | Why |
|---|---|---|
| Engine compatibility | 1, 5 | Balance placement and derived kits are not engine questions |
| Data flow | 2, 6 | It traced data, not the scripts' proofs or the loc gate's internals |
| XML | 2, 5 | It verified the merge independently (finding 2's data is right) and judged the kits valid as shipped, which they were |
| Tooling | 5 | Scripts in scope only; the enlistment kits were not written by a reviewed script |
| Completeness, Design | ran after wave 1 | Built on wave 1; Completeness found 5, Design re-framed 1 as #803 |

## Lessons recorded

- `docs/reviews/lessons/data-content-cultures.md`: a port that touches troop files diffs the whole warning set
  against HEAD, because the commit hook surfaces only errors.

## Follow-ups (issues)

#810 translation run for the new Harad names; #813 the artist-port tool; #814 the tavern-mercenary refresh; #812 the
melee gate's vanilla pricing and the couchable spear balance call; #815 the loc coverage gate; #811 the asks for
KEYforce (carry TAOM's three edits upstream, the Gondor values, shipping the shaders, the shared-troop prefix,
re-syncing the mirror); #804 the enlistment generator's `--verify` and the schema tool's XSLT label. The brief for his
assistant is `docs/ai-includes/artist-ladder-brief.md`.
