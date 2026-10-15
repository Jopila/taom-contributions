# Career forks generator

## Overview

`tools/generate_career_forks.py` writes one kingdom's forked career trees into the two career XML
files from a JSON spec. A fork is a choice group with `max_picks="1"` (a wide fork may allow more):
two or three options, one pick. Each career gets a root choice and six forks (slots 1 to 6, tiers
1, 1, 2, 2, 3, 3 by slot), plus `tree_version` and `point_levels` attributes on its `<Career>`. The
design lives in `tools/career_design/<spec-name>.json`; the generator validates it against
`tools/career_design/_rules.json` and the C# the game runs, and refuses to write on any error.

## Why This Exists

The career redesign ([#769](https://github.com/haterade22/TAOM/issues/769)) replaces the old
six-group, five-choice trees with forks. Hand-writing that XML for every career invites typos and
balance drift, and a hand edit would be indistinguishable from a regeneration.

- **Vanilla behavior:** none; the career system is TAOM's own (`docs/features/career-system.md`).
- **TAOM requirement:** one reviewable source per kingdom, a balance gate that runs before any XML
  exists, a drift gate afterwards, and a guard that a changed tree cannot ship under an old
  `tree_version`.
- **Without this tool:** each tree is authored by hand against caps nobody checks until play.

## Architecture

### Solution Approach

A textual splice, not an XML re-serialise, so every byte outside the generated regions stays
identical (the same approach as `tools/insert_new_faction_careers.py`). Per career:

| File | What is written |
|---|---|
| `taom_career_choices.xml` | the root `<Choice>` and six `<ChoiceGroup max_picks=...>`, between `<!-- career-forks:begin <career> sha256=<hash of the marker's attributes and the body> -->` and `<!-- career-forks:end <career> -->` |
| `taom_careers.xml` | the `<ChoiceGroups>` list between the same kind of markers, plus `tree_version` and `point_levels` on `<Career>`; the begin marker records both values and `shape=v2-<hash>` |

**First adoption** (no markers yet) replaces the career's existing root choice and its existing
groups (ids from `taom_careers.xml`). They must form one contiguous span with only whitespace and
comments between; otherwise the tool refuses and names the career. It also refuses unless the
spec's `tree_version` is above the career's live one (default 1): saves migrate only when the
stored version is lower, so an adoption that does not raise it would strand old picks.

**Later runs** check the region's hash, which covers the marker's recorded attributes together
with the body, and compare the recorded attributes with the live ones. Any difference means someone
edited generated text or its marker by hand (a forged `shape=` included), and the tool refuses for
that career, naming the career and file. To adopt a hand edit, put it in the spec; to discard it, delete both markers and
the tool re-adopts the old tree it finds (which then needs a higher `tree_version`).

**The tree version gate.** The `shape` in the careers marker is a version and a short hash,
`v2-<hash>`, of what decides whether a saved pick is still valid: the root id, the point levels,
per fork the group id, tier and `max_picks`, per option its letter and either its effects, each
as `type@mask#class`, or its mutation properties (sorted, so reordering effects is not
structural). It ignores text and magnitudes. A mask or class is normalised first, so respelling
it is not structural: an omitted `attack_type_mask`, `All`, or a list covering Melee and Ranged
(and no kind) all read `All`, and an omitted `troop_class`, `All` or all three classes all read
`All`; case, order and separators do not matter. A run refuses a `tree_version` lower than the
recorded one, and refuses a changed shape unless `tree_version` rose. The `v2-` prefix names the
shape definition: a marker written under another definition is refused with its own message
(the structure cannot be compared; delete both markers to re-adopt, or run the tool version that
wrote it), so a later change to the definition is not mistaken for an edit of the tree. A text-only or magnitude-only edit keeps the version. Point levels are in the shape because
`point_levels` shrinks or grows the point budget, which only a version bump refunds.

**I/O:** each file keeps its BOM and newline style (majority vote; a file with mixed endings is
refused). The output is parsed with ElementTree before anything is written, and a post-check
confirms the loader will find the six groups, the root and the attributes. Both files are written
to `<name>.gcf-new` first (not `.xml`, because the localization tool globs `career_system/*.xml`)
and then renamed into place, so a failed write leaves both originals untouched. If the second
rename fails after the first file was replaced, the first file is written back from the bytes read
at the start (they round-trip exactly) and the run exits 2; if that restore fails too, the message
names the file to restore. Any unexpected exception exits 2: exit 1 is drift only.

### What the loader reads

The generator writes only what `CareerConfigProvider` uses. It no longer writes `operation="Add"`
or `is_percentage` on a `PassiveEffect` (parsed in an older loader, never read), `icon_sprite` on a
`Choice` (parsed, never drawn: `CareerChoiceObjectVM` uses the first effect's icon, or
`keystone_icon` for a keystone), or the root's empty `group_id`. The career screen therefore shows
an option's icon from its **first effect**, so write the headline effect first.

### Ids and keys written

| Item | Pattern |
|---|---|
| group | `<career>_f<slot>` |
| choice | `<career>_f<slot>_<a\|b\|c>` |
| root choice | the career's existing `root_choice_id` |
| description | `{=taom_cf_<choice id>}text` (root: `{=taom_cf_<root id>}text`) |
| group name | `{=taom_career_grp_<career>_f<slot>}name` |

The option `name` appears in the review table only. A description, name or kingdom may not hold
`{` or `}`: the engine reads braces as a text variable or a loc key.

### Read at run time, never copied

| Source | What the generator takes |
|---|---|
| `Domain/PassiveEffectType.cs` | the effect names |
| `Domain/AttackTypeMask.cs` | the `attack_type_mask` names |
| `Domain/PassiveEffectConsumers.cs` | `Consumed`, `ClassFilteredTypes`, `MaskGatedTypes` |
| `Diagnostics/CareerPerkConsumerMap.cs` | `IsFlat` (a count, shown as "+6", not "+600%"); its body must be a chain of `type == PassiveEffectType.X` joined by `\|\|` (an expression body, or one `return` of that chain), anything else is an error rather than a guess |
| `special_resources/special_resources_config.xml` | the resource a career's `EligibleCultures` resolve to: kingdom rows first, then culture rows, as `SpecialResourceService.ResolveResource` does; a duplicate resource id is an error (the game reads the rows last-wins) |
| `special_resources/troop_resource_costs.xml` | the rows with a `daily_upkeep` above 0 |
| `troops/*.xml`, `characters/*.xml` | the `NPCCharacter` culture of each upkeep row's troop |

**Upkeep troops** are the cost rows with a `daily_upkeep` whose troop's culture is one of the
career's `EligibleCultures`. The row's `resource_id` is not read: it is documentation only, and
`SpecialResourceService` charges every cost-row troop in the party in the player's own resolved
resource. An upkeep row whose troop is found in neither folder (or has no `culture`) is an error.
Kingdoms with no resource in the data (Lindon, Goblin-town, the Misty Mountain Orcs, Blue Craig)
resolve to none, so they cannot use a `SpecialResource*` type.

A set that is missing, empty or names a type the enum lacks is an error (exit 2), never an empty
pass. `_rules.json` holds only the numbers and the rules that are not in C#; a type it names that
the enum lacks is an error too. The spec has no `resource`, `upkeep_troops` or `is_percentage`
field: a spec field could disagree with the data, so the data decides.

## Configuration

### CLI

```
python tools/generate_career_forks.py <spec-name> [--apply] [--check] [--table PATH]
```

| Flag | Effect |
|---|---|
| none | dry run: validate, print a summary, write nothing |
| `--apply` | write the two XML files, then print the localization steps |
| `--check` | drift gate: exit 1 when the XML differs from the spec (a hand-edited region or a refused change counts as drift); no writes |
| `--table PATH` | also write a Markdown review table (written even on a dry run, never for an invalid spec) |

Exit codes: 0 clean, 1 drift, 2 error. The summary prints counts per career, for example
`adopt: root + 6 groups (30 choices) -> root + 6 forks (14 choices)`.

### Spec

```
kingdom, culture_ids[]
careers[]: id, archetype (infantry|ranged|cavalry), tree_version, point_levels[6],
           root {description, effects[1..2]}, forks[6]
fork:      slot 1-6, name, max_picks (default 1), options[2..3]     (the tier follows the slot)
option:    name, description, effects[0..2]            (a Passive choice)
        or name, description, vu, mutations[1..2]      (a Keystone choice)
effect:    type, magnitude, attack_type_mask?, troop_class?
mutation:  property, calculator, value (> 0), operation   (target is the career's ability template)
```

Unknown keys are errors, so a typo cannot pass silently, and so are the retired fields.

### Validation

Every error is listed; nothing is written if any exist.

| # | Rule |
|---|---|
| 1 | Schema: required fields and types, six forks, slots once each, 2 or 3 options, at most 2 effects, a root with one or two effects, keystone mutations from the rules' property list with a value above 0, an explicit `vu` on a keystone |
| 2 | The career exists in `taom_careers.xml` and the spec's `culture_ids` equal its `EligibleCultures` (any order); ids and lists are type-checked |
| 3 | The type is in `PassiveEffectType.cs`, is `Consumed`, and is not banned; `attack_type_mask` names exist, sit only on mask-gated types, and on `TroopDamage` and `TroopResistance` (the rules' `mask_forbidden`) may only be `All`, because `CareerChoicesIntegrationTests` bans a narrower mask there until the Active Effects panel can show masks; `troop_class` is valid and only on class-filtered types; no effect is a penalty |
| 4 | A `SpecialResource*` type needs a resource derived from the data; `SpecialResourceUpkeepModifier` also needs an upkeep troop of the career's own cultures (see "Upkeep troops") |
| 5 | `point_levels` has one entry per fork, strictly ascending and positive; `tree_version` is at least 2 |
| 6 | Balance: option value units (VU) within `budget_tolerance` of the tier budget, options of one fork within `fork_spread_tolerance` of each other |
| 7 | Caps over every build stay under each cap group |
| 8 | Generated ids and loc keys are unique and collide with nothing else in the two files (comments ignored) |

**Sign.** The runtime multiplies a cost by `(1 + magnitude)` for the `lower_is_better` types
(`TroopWages`, `TroopUpgradeCost`, `RecruitCost`, `FoodConsumption`, `TradePrices`,
`SpecialResourceUpkeepModifier`, `SpecialResourceUpgradeCostModifier`), so the **benefit** of those
is the negative magnitude and of every other type the magnitude. An effect with a benefit below 0
is refused as a penalty. VU and the caps are both priced on the benefit.

**Balance maths.** `vu_unit[type]` is the benefit worth one VU. An option is the sum of
`benefit / unit` over its effects. A troop effect narrowed by `troop_class` to fewer than all three
classes is worth `1 / class_bonus` (1.5) as much per unit, so class-only effects may be 50% larger.
A keystone carries an explicit `vu`.

**Caps.** Forks are independent and picks are voluntary (a fork may stay empty), so the worst build
is the root plus each fork's top `max_picks` options with a positive benefit. The report names that
build. Masks and class filters are not discounted: a melee and a ranged damage pip both count.
`TroopUpgradeCost` and `SpecialResourceUpgradeCostModifier` each have a cap group of 0.5 on the
benefit (the negative of the cut): `TroopUpgradeCost` has no runtime floor, so a total cut past -1
would give a negative upgrade price.

### Current values

`tools/career_design/_rules.json` holds the bans, tier budgets 3, 4 and 5, tolerances 0.5, the
`lower_is_better` and `mask_forbidden` lists, a `vu_unit` per type with a note, and the cap groups
(the full list is in the file; `Damage` and `ArmorPenetration` share one cap because armour
penetration is a second damage multiplier). **Every number is a draft** the maintainer approves
through the review table. The 2026-10-15 balance decisions changed `TroopMorale` to a factor
(0.05), `Resistance` to 0.04, `RenownGain` to 0.125, `InfluenceGain` to 0.25,
`SpecialResourceUpkeepModifier` to 0.333, added units for the ten E11 types, and banned
`ShrugOff`, `CompanionLimit`, `InventoryCapacity` and `SmithingCostReduction` next to
`StealthBonus`, `Special` and `BuffDuration`. Banned types carry no unit. A flat unit (a count, per
`IsFlat`) is 1 or more and every other unit is below 1; a test pins both.

## After an apply: localization

When the run added loc keys, the generator prints these steps after `--apply` under the header
"new loc keys" (an update that adds none, such as a numbers-only edit, prints the tests steps alone).
Every command runs unchanged in bash and PowerShell:

1. `python tools/generate_name_localization_strings.py --apply --category career_data` (the tools
   test for that file fails until it runs).
2. One command per language code, twelve in all: `python tools/translate_with_claude.py --lang <XX>
   --module TAOM --seed-only`. `--seed-only` is the free half of `--sync-ids --apply`: it writes the
   English rows each language file lacks, keeps the file's line terminators, and exits before any
   provider, key or API client exists. It refuses `--apply`, `--batch`, `--max-entries`, `--model`,
   a non-default `--provider` and the price flags. A paid translation run needs the maintainer's word.
3. `python -m unittest discover -s tools/tests -t .`
4. `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` with a `--filter` that joins
   `FullyQualifiedName~<name>` by `|` for `CareerChoicesIntegrationTests`,
   `CareerChoiceIntegrityTests`, `CareerConfigProviderTests` and `LanguageFileCoverageTests` (the
   last stays red until step 2).

On an **update**, a key keeps its id while its English may change (keys follow the option's
position), and no step of the pipeline re-translates a row under a changed English. The generator
lists every key whose English changed and points to
[the translator guide's checklist](../localization/TRANSLATOR_GUIDE.md) (substitute the 12 rows,
fix the cache, check the override files).

## Key Files

| File | Purpose |
|---|---|
| `tools/generate_career_forks.py` | the generator; its docstring is the CLI reference |
| `tools/career_design/_rules.json` | balance numbers, bans, caps, the sign list |
| `tools/career_design/<spec-name>.json` | one kingdom's spec (first real spec is a later step) |
| `tools/tests/test_generate_career_forks.py` | unittest suite on temporary fixtures and a synthetic CareerSystem folder |
| `tools/tests/test_translate_seed_only.py` | the `--seed-only` translator mode builds no client |
| `Main/_Module/ModuleData/career_system/taom_careers.xml`, `taom_career_choices.xml` | the generated files |

## Tests

`tools/tests/test_generate_career_forks.py` builds fixtures in temporary directories (CRLF and LF,
with and without a BOM, a synthetic `CareerSystem` folder of tiny `.cs` files and a synthetic
`special_resources` folder) and never touches the shipped XML. It pins the byte-exact splice, the
no-op second run, each refusal, the tree version gate, the atomic write, `--check`, one test per
validation rule, the sign, a brute-force check of the cap search with empty forks allowed, the
review table, the shape's mask and class normalisation, the signed marker hash, the rollback of a
failed second rename, the upkeep count by culture, the `IsFlat` grammar, the printed next steps, junk
input that must produce errors and not exceptions, and the plan's worked example
(`captain_of_osgiliath`, priced with the shipped rules, the real C# and the real resource data).
Read-only classes check the shipped rules against the real C# (every consumed type has a unit,
flat units are counts, the rules name no unknown type) and run `--check` for every spec in
`tools/career_design/` against the shipped XML, plus: every marker career id belongs to a spec and
no career is in two specs (skipped with a message while no spec exists). Run
`python -m unittest discover -s tools/tests -t .`.

## How to write a kingdom

1. Copy the shape above into `tools/career_design/<kingdom>.json`, headline effect first.
2. `python tools/generate_career_forks.py <kingdom> --table out.md`, fix the listed errors, and
   review the table with the maintainer.
3. `--apply`, then the printed steps above. The check of the generated files is the C# test filter
   (`CareerChoicesIntegrationTests`, `CareerChoiceIntegrityTests`, `CareerConfigProviderTests`,
   `LanguageFileCoverageTests`).
4. Later: `--check` is the drift gate (and a tools test runs it for every spec). Change the spec,
   never the generated region; a structural change needs a higher `tree_version`.

`CareerChoicesIntegrationTests` pins the shipped kind-masked pip ids (a `Cut`, `Pierce` or `Blunt`
`attack_type_mask`). Adopting a career that holds one (`black_uruk_captain`, `olog_hai_warchief`,
`avanc_luth_raider`, `watchman_of_stangard`) replaces those pips, so update that test in the same
batch.

## Changelog

- 2026-10-14: first version (C1 of the #769 career redesign), tests only, no kingdom spec yet.
- 2026-10-15: review fixes: benefit sign, C# sets and resource data read at run time, tree version
  gate, atomic write, localization steps, `--seed-only`, the balance decisions.
- 2026-10-15: convergence-review fixes: the shape sees `attack_type_mask` and `troop_class` and is
  versioned (`v2-`), the marker hash covers the recorded attributes, upkeep troops are counted by
  NPCCharacter culture, the second rename rolls back, `IsFlat` has a strict grammar, duplicate resource
  ids are refused, upgrade-cost caps (0.5), and next steps that work in both shells.

## GitHub Issue

- **Issue:** #769, career redesign
- **Status:** Open
