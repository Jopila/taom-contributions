# RCA: Erebor dwarf dresses (deep-review, 2026-10-16)

## Top-line

Mike asked for the female Erebor NPCs to come back now that the female dwarf skin works. None had
been removed: the 16 `is_female="true"` NPCs dated from the 2026-09-06 sex sweep and had rendered
as men only while the skin was broken. His rule for the rest: `is_female` decides who is a woman,
and every other dwarf wears a tunic. The change swapped 82 Body ids from `sk_dwarf_dress_normal_*`
to tunics (24 male NPCs in `npcs_erebor.xml`, the civilian sets of 29 troops in
`troops_erebor.xml`, 3 male child templates in `taom_child_equipment_templates.xml`) and added the
gate `TAOM.Tests/Core/DwarfDressWearerTests.cs`.

`/deep-review` ran seven lenses in two waves (Standards, Engine compatibility, Data flow and XML,
then Efficiency, Completeness and Design). No CRITICAL or HIGH. Two MEDIUM and five LOW findings
were confirmed and fixed in the same session; two more sit outside the repo or predate the change
and are recorded as follow-ups. One finding changed the scope question: issue #56 had dressed four
notables (6, 7, 12, 13) as women on purpose. Mike decided they stay men in tunics.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MED (Standards, Engine compatibility, Data flow) | The gate called a roster male only on `IsFemaleTemplate="false"` or a `father_` id. The engine sets a flag bit only when its value is true (`MBEquipmentRoster.Deserialize`) and `DefaultEquipmentSelectionModel.GetSuitableEquipmentSet` matches flags exactly, so absent means male. 118 flag-bearing lord, teen and ruler templates and 428 `player_*_m` kits were unchecked, and so were men of other races and enlistment kits. The doc bullet claimed every male template was gated. | Gate modelled on id conventions, not on the engine | The plan wrote the predicate from what the data looked like (`IsFemaleTemplate="false"` on the three offending rosters) without reading the flag parser. A blacklist that starts from known men has the same blind spot as `MOUNTED_DWARF` ("only sees data an NPCCharacter names", `.claude/rules/moduledata-validation.md`). | Fixed: the gate now fails closed. A dress passes only on an `is_female="true"` character, or in a roster that no man names and that is flagged `IsFemaleTemplate="true"` or is a flagless `mother_*` roster; a `father_*` roster never passes. 15 data rows pin each context; the 5 rows the first rule let through went RED first, and the convergence pass's 2 parity rows (a female-flagged `father_*`, a lord-flagged `mother_*`) went RED before the last fix. Lesson: the recurrence bullet on "A documented gate limitation is a backlog item" in `lessons/data-content-cultures.md`. |
| F2 | MED (Standards, Completeness, Design) | The lesson and the test summary blamed a round-robin import and named the blacksmith, barber, headmen and townsman. Issue #56 (`d44b72886`, 2026-04-02) had already moved those to tunics, and it had dressed "neutral" roles in one dress and one tunic on purpose, plus four notables as women. The same history was told to Mike as "dresses carry no gender intent". | Provenance claimed without reading the decision record | The builder read the first import (`244f5922f`) and the sex sweep, and did not search issues or `git log -S` the later passes. The `03320a65` lesson of the same day ("Read the decision record before calling data hand-authored, generated or stale") names exactly this. | Fixed: the standalone lesson was removed and rewritten as the recurrence bullet, with each history clause checked against `gh issue view 56` and the `d44b72886` file. The test summary now states the engine basis and #56. Mike was asked about the four notables #56 coded female. Repeat offender: see "Root-cause pattern". |
| F3 | LOW (XML, Engine compatibility, Data flow, Design) | The letter-parity mapping left `villager_erebor` with one Body look in all sets and `villager_teenager_erebor` with another. Before the change `villager_erebor` had three Body looks (two dresses and a tunic) and `villager_teenager_erebor` two. | Cosmetic regression from a mechanical mapping | The swap script mapped each dress letter alone and did not compare the result with the character's other sets. | Fixed with Mike's yes: three ids now alternate tunic A and B. |
| F4 | LOW (Engine compatibility, Data flow, Completeness) | The save note given to Mike said troops change "on the next visit" and left out children and Field Commission companions. A settlement visit does not show a troop's civilian set; the lenses found it on alley troops, conversation bodyguards and newly promoted companions, among other civilian spawns not traced. Heroes, children and companions already in a save keep the clothes stored on them (`Hero` equipment is a `SaveableProperty`). | Unverified engine claim in a report | Written from memory of how townsfolk spawn, not from the decompile. | Corrected to Mike in the session; the commit body carries the corrected scope. |
| F5 | LOW (Engine compatibility) | `catch { continue; }` in the walk skipped a file that failed to parse, so a broken `npcs_erebor.xml` would have passed while men in other files existed. | Silent skip in a gate | Copied from the sibling walkers. | Fixed: the walk loads without a catch, so a parse error fails the test with its file name (all 138 files load today). |
| F6 | LOW (Standards) | The test file was LF in a CRLF test folder. | Hygiene | Written by a subagent with the Write tool. | Fixed: CRLF. |
| F7 | LOW (Standards) | The new lesson lines ran to 332 characters next to entries wrapped near 110. | Hygiene | Written by script without the neighbours' wrap. | Fixed by the move: the recurrence bullet wraps like its entry. |

Follow-ups, not fixed here:

- **`DualWield_TAOM`** (yotthani's unversioned add-on, v0.1.3): `dw_erebor_twinaxe`, a man, wears `sk_dwarf_dress_normal_a` in its civilian set, copied from `erebor_reg_company`'s set before this change. The repo gate cannot see another module, and an edit to the install reverts on update. Asking yotthani to re-run his generator needs Mike's word.
- **`tools/generate_troop_roster_page.py:190`** skips only `equipmentType="Civilian"`, so inline `civilian="true"` sets of 130 troops draw as battle sets in `docs/reference/troop-rosters.html`. Pre-existing. The page was regenerated with this change (29 dress rows became tunics, nothing else moved), so the 29 tunics still show there as battle sets until line 190 is fixed.

Information, no action: the engine pools the three male child templates (identical flags), so their noble and common tunics mix for every male hero child; five of the 24 NPCs are dead data (no culture slot names them, or the slot is never read).

## Root-cause pattern: provenance from appearance, twice in one day

F2 is the second time on 2026-10-16 that a builder described where data came from by how it looked.
The `03320a65` port called enlistment kits hand-authored that a generator wrote; this change called
deliberate #56 dresses an import artefact. Both had a decision record one search away (an RCA
decision table there, a closed issue here). F1 has the same shape at the gate: the predicate was
written from what the three offending rosters looked like, not from how the engine reads flags.

The existing rules already cover it (`evidence-over-claims.md` C1, and the `03320a65` lesson), so no
new rule is added. The recurrence bullet widens the September lesson from sibling files to sibling
layers: when a sweep settles who is female, every layer that encodes sex (clothing,
`IsFemaleTemplate`) is re-run against its result.

## Why each lens caught what it caught

- **Standards** found F1 by reading the flag parser to check the doc claim, and F2 by reading issue #56 while checking the lesson.
- **Engine compatibility** found F1, F4 and F5 while tracing how civilian sets, child templates and saved heroes are equipped.
- **Data flow** found F1 by counting every roster the engine treats as male, plus the DualWield and roster-page follow-ups.
- **XML** found F3 by comparing each character's set variety before and after, and confirmed byte fidelity.
- **Completeness** confirmed F2 commit by commit and found the missing issue.
- **Design** proposed the fail-closed shape for F1 and the recurrence form for F2.
- **Efficiency** found no runtime path; it shaped the one-pass walk.
- **Tooling** did not run: the swap script is a scratchpad one-off that does not ship.

## Feedback memories to codify

None. The pattern is already a rule and a same-day lesson; the fix is to follow them.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/data-content-cultures.md](lessons/data-content-cultures.md)

<!-- backlinks-end -->
