# RCA: Dunland formation groups (#953; deep review 2026-10-20)

## Top-line

Mike reported that the Dunland Cigfran-lûth Hunter (`dunland_raven_noble_son`), a bow-only archer,
fought in the infantry formation. Its `default_group` was `Infantry`. A sweep of the 904 troops in
`troops/troops_*.xml` found three more: the Falcon horse archers (`dunland_falcon_archer`,
`dunland_falcon_wildrider`, `dunland_falcon_noble_horse_archer`) were tagged `Cavalry`. All four now
carry the group their kit implies. Four lenses ran: Completeness, Data flow, Design and XML. Standards,
Engine compatibility, Efficiency and Tooling were not in scope, because only XML changed. The review found
no runtime defect in the edit. It found that the change was half done (the skills still followed the
old groups), one stale comment, one latent generator effect and a missing gate. This is the fourth time
this class has shipped.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | Four Dunland troops tagged with a melee group while they carry a bow: the Hunter fought as infantry, the Falcons as melee cavalry and, in sieges, as infantry | Data contradicts kit | For a non-hero, `default_group` alone sets `DefaultFormationClass`, `IsRanged` and `IsMounted` (`BasicCharacterObject.Deserialize`). A wrong value raises no error, and no gate compares the group with the kit | Groups fixed. Follow-up gate (F5) |
| F2 | LOW | The four troops' skills were still the curves of their old groups: Falcon Polearm 145/195/250 with no polearm, Riding on the cavalry curve; `dunland_raven_warrior`'s melee raised above the Hunter's infantry curve | Half a reclassification | `rebalance_troops.py` picks the curve from `default_group`, and only a full rebaseline re-reads it. The first fix changed the group only | Restatted the five with `--fix-monotonicity --restat`, as #554 did. Lesson |
| F3 | LOW | Section comment `Falcon Clan Branch (Horse Archers, Cavalry)` kept the old group | Stale text | Every branch comment in the file ends with its group, and the edit touched only the attributes | Fixed |
| F4 | LOW | `generate_enlistment_rosters.py` chooses donors by `default_group`. Its dry run for `empire` now picks the Hunter, which has no sidearm and no helmet, as the archer recruit donor | Latent generator effect | Offline generators read the group too, and nothing runs them on a data change | Recorded in #953 and the commit body; the shipped kits change only on a deliberate `--reseed` |
| F5 | MEDIUM | No validator check compares a troop's `default_group` with what it carries, except `MOUNTED_DWARF` for dwarves | Gate gap | #555 item 3 planned `THROWN_MELEE_MISGROUPED` for Ranged troops that carry no launcher. That is the opposite direction, and it was never built | Follow-up: widen #555 item 3 to both directions. The validator and hook files carry another session's uncommitted edits, so the gate does not land in this commit |

## Root-cause pattern

**One soft-failing attribute feeds several consumers, and each consumer trusts it.** The engine reads
`default_group` for formation, ranged and mounted flags, siege dismounts, auto-resolve modifiers and
perk sets. TAOM's banner bearers, garrison swap, Custom Battle slots and doctrine routing read the
class the engine derives from it. Two offline generators read the attribute itself, for skills and
enlistment kits. A wrong value is wrong in all of these at once, and none of them reports it. That is why
F1, F2 and F4 are one defect seen from three places, and why the fix must restat as well as retag.

**A gate planned for one direction leaves the other open.** #522 was a HorseArcher with no ranged
weapon. #554 was javelin troops tagged Ranged. Both made the planned gate a check on Ranged troops that
carry no launcher. This incident is the reverse: bow carriers tagged with a melee group. The rule that
covers all of them is the one the engine already applies to heroes (`CharacterObject.GetFormationClass`):
horse or no horse, crossed with a bow or crossbow or no launcher.

## Why each agent missed these

This change had no earlier review. The defects came from earlier authoring of the Dunland Raven and
Falcon lines, and I did not trace when those values were written. No `/deep-review` lens checks
`default_group` against equipment by default, and the XML lens's gate list has no such gate to run.

The orchestrator's first sweep also had two blind spots. It matched bows by item-id text, and it
skipped battle sets that come from an `<EquipmentSet id>` reference. The XML and Data flow lenses
closed both with sweeps that type items from their definitions and resolve set references. They found
no further troop.

## Feedback memories to codify

None. The durable rule is the lesson entry in `docs/reviews/lessons/data-content-cultures.md`
("A default_group fix is a retag and a restat, and its gate must check both directions") and the
widened #555 item 3.
