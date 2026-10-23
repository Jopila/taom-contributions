# RCA: Personal kill loot for the enlisted soldier (#834, deep-review, 2026-10-23)

## Top-line

`/deep-review` (seven lenses in two waves, then a convergence pass) on #834 found **no engine
incompatibility** and **two MEDIUM defects**, both fixed before the first commit, plus a set of LOW
findings. Both MEDIUM defects came from reasoning about the engine by a name instead of by its body: a
"cap" applied before the engine's roll, and "the player" read as `Mission.MainAgent`.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Engine compat) | The policy cut the kill list to the cap before any item was rolled. Kills are ranked by expected value (`7.25 x level^2`), so trolls (level 51) always came first, but their gear is all non-merchandise and vanilla's roll skips it (`DefaultBattleRewardModel.cs:109`). Three troll kills and twenty orc kills paid nothing. Even ordinary troops roll nothing about a third to two thirds of the time, more when the gear is worth more than the target (`:142`). | Cap applied to inputs, not outcomes | The plan assumed the roll returns an item "near the target value"; the body walks items from most to least valuable and often returns nothing. | The policy ranks every kill; the payout stops once the cap has landed. Lesson appended to `lessons/gamemodels-services.md`. |
| F2 | MEDIUM (Data flow) | Kills were attributed to `Mission.MainAgent`, which moves to a soldier the player takes control of after his hero falls (`Agent.cs:1255`), so that soldier's kills paid kill XP and spoils. | Identity by role, not by character | The existing kill counter already had the flaw; the feature reused it. | The killer must be `CharacterObject.PlayerCharacter`, as vanilla credits combat XP by character (`BattleAgentLogic.cs:233`); a controlled soldier is never a hero (`Mission.cs:6394`). The merit sampler in the same file had the same flaw and now uses the same check. |
| F3 | LOW (several) | Spoils were described as never reclaimable (an unmodified spoil merges with an issued item's stack); the adapter rebuilt the `EquipmentElement` from string ids (adapters rule: carry the element); a hand-written sort; a guard read `Hero.MainHero` before the campaign check (harmless inside the try/catch, reordered anyway); no wiring test for the payout's new dependencies; engine claims in comments; the issue body still the plan. | Accuracy and simplicity | Written before the engine trace. | One adapter call rolls and adds the engine's element; `OrderByDescending`; docs and the issue as built. |

## Convergence pass

One reviewer checked the applied fixes against the installed v1.5.5 sources: the landed-item cap, the guards in the single roll-and-grant call (a character with only civilian sets has no battle equipment and is skipped), the modifier kept by `AddToCounts`, and the character check (a controlled soldier never matches; mount kills were skipped before and still are). It found three LOW items, all fixed: the reclaim wording missed the common case (a worn issued piece), the doc named only kill XP although kills also feed the merit score, and the merit sampler in the same file still read `Mission.MainAgent`. It also corrected two RCA statements.

## Root-cause pattern

F1 and F2 share a cause: an engine concept was used by its name (a loot "roll", the "main agent") rather
than by what the body does. Both were caught only by reading the decompiled body and the setters behind
the name.

## Why each lens caught or missed these

- **Standards:** the sort, the guard order, the wiring gap, the garbled doc.
- **Engine compatibility:** F1 with the troll scan and the roll odds; the reclaim merge; the comment errors.
- **Efficiency:** no findings.
- **Completeness:** the issue body, the missing tests, the hero-cache timing risk, the armour-gate note.
- **Data flow:** F2, the reclaim merge, and every gate connected.
- **Design:** the landed-item loop, one adapter call, the character check that needs no cache.
- **XML:** no findings.

## Feedback memories to codify

None; the lesson covers the pattern.
