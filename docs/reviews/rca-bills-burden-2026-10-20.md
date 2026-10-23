# RCA: Bill's Burden deep review (#876, 2026-10-20)

## Top-line

The map-bar cargo fill row went through a 7-lens deep review with no CRITICAL, HIGH or new MEDIUM
finding. The one MEDIUM is the known unbaked sprite, which the doc and the issue already carried. The
LOW findings below are all confirmed against the v1.5.5 decompile or the worktree, and all are fixed
except where the table says otherwise. The design lens's simplifications were applied in the same
pass: an int capacity, one fill-text helper, a record struct, and no hand-written setter guards.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | `MapBar\taom_cargo` resolves in no sprite data until the editor bake, so the row draws an empty icon slot with no error; the only pin is `[Ignore]`d. | Known, owed | Not missed: the bake is an editor step the maintainer runs. | Doc lists the bake, the `sync_sprite_bake.ps1` sync-back and the relaunch; the `[Ignore]` reason names #876; #876 stays open until the pin passes. |
| 2 | LOW | The tooltip printed the weight with `(int)Math.Round(weight)`, which rounds half to even, while the inventory screen prints `ToString("#0")`, which rounds half away from zero: 12.5 showed 12 here and 13 there. | Format not taken from vanilla | The builder wrote its own rounding for a number vanilla already displays. | Use vanilla's expression (`weight.ToString("#0")`). Lesson in `lessons/localization-ui.md`. |
| 3 | LOW | The ignored sprite pin merged the SpritePart and GenericSprite names into one list, so a manifest with only the SpritePart passed while `SpriteData.GetSprite` (which reads only the sprite table built from GenericSprite and NineRegionSprite entries) returns null. The brush test also named the layer by a string literal, not the mixin's `ItemId`. | New pin weaker than its sibling | `FiefsNavButtonWiringTests` asserts both lists and uses the C# constant; the new test was written without it open. | Both asserts, `CargoMapBarMixin.ItemId` made internal and used by the test. Lesson in `lessons/localization-ui.md`. |
| 4 | LOW | In an army, the engine's overburdened penalty sums every attached party's cargo and capacity (`DefaultPartySpeedCalculatingModel.cs:104-121, 155-159`), while the row shows the main party only, so the red state can differ. Undocumented. | Undocumented scope | The plan compared the row with the inventory screen, not with the speed model. | Doc and #876 say so; an in-game check compares the two while leading an army. No code change (Mike's design: no army aggregate). |
| 5 | LOW | Three `if (_item.X != value)` guards repeated the engine setters' own guards (`MapInfoItemVM.cs:36, 53, 106`). | Copied pattern | The reference mixin tracks last values for its own reasons; the guards came with it. | Removed; the comment says the setters skip unchanged values. |
| 6 | LOW | The calculator took a float capacity, so a NaN/Inf branch, its comment and two tests guarded a value the engine types as `int` (`MobileParty.cs:924`), and the tooltip round-tripped that int through `Math.Round`. | Type wider than the source | Written before the engine type was read. | `Compute(float weight, int capacity)`; the two unreachable tests deleted. |
| 7 | LOW | The invalid-fill tests asserted only `IsValid`, while the mixin copies `Percent` and `HasWarning` without checking it. | Contract not pinned | The mixin's reliance on the invalid defaults was implicit. | The zero-capacity and NaN-weight tests now pin Percent 0 and no warning (every invalid input takes the one early return); the record struct's summary states the contract. |
| 8 | LOW | The doc did not record the owed translation, the army scope, the value flash or the fast-forward cadence; Key Files omitted the PNG and the strings; the issue link text did not match the title; #876's body described the superseded design. | Doc completeness | The doc and issue were written from the plan's first draft. | Doc rewritten from the final code; #876 body rewritten; the 2 keys added to #763. |

Out of this change's scope:

- The engine lens reported that no test pins the method name in a `[ViewModelMixin("...")]`. That
  was true of this branch's base only: trunk gained `TAOM.Tests/Migration/ViewModelMixinBindingTests.cs`
  the same day (`e7cdb2e96`), which finds every named mixin by reflection, so this mixin is covered
  once the branch is rebased.
- The special-resource mixin skipped its first refresh, so its entry appeared 2 s late; fixed
  separately under #958.
- The external-loc coverage gate is red on trunk from another session's live
  `TAOM_Map/ModuleData/settlements.xml` edit, not from this change.

## Carry-over onto trunk (2026-10-23)

The change waited three days uncommitted while trunk split its UI into `TAOM.Client` (ADR-013). It
was carried over by a 3-way apply, not a rebase. The mixin moved to `Client/Features/BillsBurden/UI/`,
the calculator stayed in core, and the client inventory pin in `ClientIsolationGateTests` went from 26
to 27 extensions. The strings conflicts kept trunk's rows and added the two cargo rows. A delta review
found no runtime defect; it restored a blank line the apply dropped in the lessons file, updated the
core-internals comment in `Main/TAOM.csproj`, and added the two first-refresh pins that trunk gave the
special-resource mixin (#958) to `CargoMapBarWiringTests`.

## Root-cause pattern

Findings 2, 3 and 6 share one shape: the build wrote its own version of something vanilla or a TAOM
sibling already had (a number format, a sprite pin, an engine type) instead of reading the original.
Each was one file away. The two lessons make that read a named step for UI rows and wiring pins.

## Why each lens saw what it saw

- **Standards (1)** found 5 and 6 and the owed `Not-tested:` trailer.
- **Engine compatibility (2)** verified every API and found 4 and the value flash in 8.
- **Data flow (5)** found 2 and 4 by tracing the weight to both screens.
- **XML (7)** confirmed the brush and strings bytes and the silent null sprite in 1.
- **Completeness (4)** found 3, 7 and the doc gaps in 8 by comparing with the sibling test.
- **Efficiency (3)** found no cost issue; **design (6)** proposed the simplifications applied for 5, 6 and 7.

## Feedback memories to codify

None. The two lessons cover the systemic part; the rest is one-off.
