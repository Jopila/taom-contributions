# RCA: Hammerhand (#895) shield bash and kick knockdown, first deep review (2026-10-20)

**Summary.** Hammerhand lets a shield bash or an empty-handed kick knock a man down: the chance is the attacker's
Athletics lead over the victim, cut by race and race-ability resistance, weaker for a kick and for the player as victim.
It was reviewed uncommitted in the worktree `E:\repos\taom-battle-ai-missions` (branch `feat/battle-ai-missions`, base
`639df895b`) before any in-game run. Seven lenses ran in two waves (standards, engine compatibility, efficiency and data
flow; then completeness and design), then one convergence pass on the fixes.

- **Severity:** 0 CRITICAL, 1 HIGH, 3 MEDIUM, the rest LOW. The HIGH would have shipped a feature that almost never fires
  (row 1). The convergence pass found four LOW items and five nits.
- **Where the HIGH came from:** the approved plan. Its engine facts said the ShrugOff flag "already blocks" Hammerhand for
  creatures and a dwarf standing fast, and never asked what else sets that flag.
- **Mike's decisions on the review (2026-10-20):** a hero's career shrug-off passive protects; a live race ability's
  knockdown resistance divides the chance; only humanoid attackers count; a race multiplier of 0 means no resistance, as
  in the charge rule.
- **Outcome:** every confirmed finding is fixed in the working tree. The in-game spike (Blow Diagnostics) is owed and
  comes first.

Feature doc: [combat-mechanics.md](../features/combat-mechanics.md), section "Hammerhand".

## Findings

Severity is the finder's. "Lens" names the finder. Each row was re-checked against the source, the v1.5.5 cache or the
native decompile before it was entered here.

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | HIGH | Hammerhand declined any blow carrying `BlowFlags.ShrugOff`. Vanilla sets that flag when the victim survives and the damage is at most the stagger threshold (`MissionCombatMechanicsHelper.cs:16-25`). A bash or kick has magnitude 1 or 2 (`SandboxAgentApplyDamageModel.CalculateAlternativeAttackDamage` :1172-1195), so 1 to 3 damage against a blunt threshold of 5.0 (Native `managed_core_parameters.xml`). Nearly every bash and kick was declined. Native still reacts to a NoIgnore blow (every bash and kick, `Mission.cs:5569`) and honours KnockDown (decompile RVA 0x5EB4E0, 0x601350). Lenses: engine, data flow. | Engine flag used as a gate without tracing its producers | The plan read who READS ShrugOff (the knockdown helper) and two TAOM writers, not vanilla's own writer. The doc's existing ShrugOff row ("suppresses knockback/knockdown") made the flag look like a deliberate-immunity marker. The tests pinned the gate on hand-built contexts, so they could not see that real input always trips it. Third instance of the class: `rca-race-abilities-2026-10-04.md` R3, `rca-lost-footing-2026-10-20.md` row 2. | The flag is not read; the three deliberate immunities are asked directly (creature stagger immunity, a live race ability's shrug-off, the career passive). A pin keeps the flag out. Lesson in `lessons/adapters-taleworlds-api.md`. Rule promotion proposed (see below). |
| 2 | MEDIUM | A live race ability's knockdown resistance (+100% for four rows with no shrug-off) did not count against Hammerhand, though vanilla's weapon knockdown and the charge rule read it through `GetKnockDownResistance`. Lenses: completeness, data flow. | Second input of a shared stat missed | The plan listed the race table as "the" knockdown resistance. | Mike: it divides the chance (`RaceAbilityHooks.KnockDownResistance(victim, 1f)`); `race-abilities.md` lists the path. |
| 3 | MEDIUM | The owed in-game list could not be run as written (no mode, troops, Athletics source or expected counts), its troll step expected a fall that creature immunity prevents, and the spike that decides the feature had no procedure. Lens: completeness. | Owed checks not executable | Repeat of `rca-lost-footing-2026-10-20.md` row 8 the same day, and of `lessons/testing-qa.md` on executable owed checks: the list was written from the plan's last section after the build. | Rewritten with the Blow Diagnostics spike first and the computed counts. |
| 4 | MEDIUM | The #895 body still described the old design (opposing side only, player only, an AI toggle); Mike's decisions lived only in a scratchpad. Lens: completeness. | Decision record | Decisions were applied in code and doc, not on the issue. | Decisions comment posted on #895. |
| 5 | LOW | A creature's kick passed whenever its agent had a Character (riderless spiders); mounts were excluded only because their Character is null. Lens: data flow. | Implicit eligibility | Eligibility followed from a guard written for another reason. | Mike: humanoid attackers only (`AttackerIsHuman`). |
| 6 | LOW | A race multiplier of 0 meant "never falls" for Hammerhand and "always falls" for the charge rule. Lenses: data flow, design. | One field, two meanings | A guard copied from the NaN rule without reading the sibling consumer. | Mike: match the charge rule; the guard is deleted. The doc notes the 0/0 case (convergence). |
| 7 | LOW | The facade and the service tested the same five conditions, three context fields were constant in production, skills were `float` though the source is `int`, the service re-validated the provider's config, and four facade tests could not fail (a null agent declined before the gate mattered). Lenses: standards, design. | Duplicated gate, tests that cannot fail | The builder made the cheap gate complete instead of minimal, then tested it through a path that declines anyway. | One-clause facade gate; the service decides the rest; int skills; trust the provider; the four tests deleted and the gate pinned on the source. Lesson in `lessons/testing-qa.md`. |
| 8 | LOW | The shield-class gate was pinned only by source text. Lens: standards. | Test strength | | `IsShieldClass` is internal and tested on three classes. |
| 9 | LOW | The doc called `MissileHitCallback` (`Mission.cs:5917`, `:5937`) a second melee path, and said "perks and race abilities count" for Athletics, which neither does in v1.5.5. Lenses: standards, engine, data flow, completeness. | Doc citations | Line numbers matched to methods by eye. | Corrected. |
| 10 | LOW | Stale text: "four services" in five places, the Custom Battle list, `gamemodel-registry.md`, `race-abilities.md`, `configs-balance.md`, `INDEX.md`, the race multiplier limitation line, the hot-path comment. Lenses: standards, completeness, design. | Docs | | All updated. |
| 11 | LOW | The MCM hint hard-coded numbers that live in JSON and said trolls are "hard to floor" (they are immune while Creature Stagger Immunity is on); the plain-English waiver for /localize was not recorded. Lenses: completeness, data flow, convergence. | Player text | | Hint names no numbers and qualifies the trolls; the waiver is in the doc (batch precedent: Fell Foes, Lost Footing). |
| 12 | LOW | The facade read the MCM toggle before the cheap field test on every hit. Lens: efficiency. | Cost | | Superseded by row 7: no toggle read is left in the facade. |
| 13 | LOW | Doc notes owed: in single player an ally does not fall (a friendly melee hit is cancelled, `Mission.cs:6351-6372`); `IsMainAgent` follows a takeover. Lens: engine. | Docs | | Added. |
| 14 | LOW | Convergence: the builder called the counting `RaceAbilityHooks.ShrugsOff`, so every bash or kick on a live Stand Fast or Berserk soldier added to the shrug-offs telemetry, even with Hammerhand off. | Side effect of a query | The immunity source was reused without reading its body. | `RaceAbilityHooks.IsShruggingOff`, a non-counting query; a pin keeps the builder on it. |
| 15 | LOW | Convergence: a duplicate winning-roll test, the knock-back half of the ShrugOff doc row, a test named Bash that passes a kick, the MCM page name, a stale test comment. | Tests and docs | | Fixed. |

## Root-cause patterns

**The plan carried the HIGH again (rows 1, 2).** As in Lost Footing the same day, the defect sat in the approved plan's
engine facts and the build implemented it faithfully. The plan read the knockdown helper's body (the Lost Footing lesson)
but treated a flag it gates on as known. Reading a member's body is not enough when the decision depends on a flag or a
stat: every writer of that flag or every input of that stat must be listed, vanilla first.

**Tests pinned the defect (rows 1, 7).** The service tests built contexts by hand with ShrugOff false, so they proved the
gate's logic and hid that real input always trips it. The facade tests passed null agents, so they could not fail when a
gate clause was deleted.

## Why each lens caught or missed what it did

- **Standards** found the duplicated gate, float skills, the source-only pin and the stale counts (rows 7, 8, 10).
- **Engine compatibility** found row 1 from vanilla's shrug-off rule and the alternative-attack damage, and settled the
  native reaction in the decompile; also rows 9 and 13.
- **Efficiency** found the gate order (row 12); the shrug-off question was outside its lens.
- **Completeness** found rows 2, 3, 4 and 11.
- **Data flow** found row 1 independently by tracing the blow from collision to verdict, plus rows 2, 5 and 6.
- **Design** gave the fix shapes (row 7) and row 6.
- **Convergence** found rows 14 and 15.

## UNVERIFIED until the spike

- That the knockdown plays visibly on a bash or kick (native says yes; the visual is owed).
- That a shield bash reaches the model with the shield as `attackerWeapon` (five engine consumers expect it).

## Feedback memories to codify

None. The rule is promoted (Mike, 2026-10-21) as the "Writers before gates" row of `.claude/rules/csharp-architecture.md`
"Non-Negotiable Rules": an engine flag or stat a plan or gate relies on lists every writer, vanilla first, with citations.
Three instances of the class are on file.
