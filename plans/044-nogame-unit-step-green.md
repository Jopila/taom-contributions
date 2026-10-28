# Plan 044: The no-game unit step passes: tag or fix the 7 tests that run engine code on BUTR's stubs

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: `git diff --stat 370b4bf2b..HEAD -- TAOM.Tests/Features/CoopInterop/Missions/CoopMissionOwnershipTests.cs TAOM.Tests/Features/CoopInterop/Missions/CoopMissionAuthorityTests.cs TAOM.Tests/Features/Enlistment/EnlistmentHourlyTickTests.cs TAOM.Tests/Features/FieldCamp/CampActivitySettingsTests.cs TAOM.Tests/Features/TelcharsMark/Patch109WiringTests.cs TAOM.Tests/Features/ManyHammers/ManyHammersWiringTests.cs docs/migration/v1.5.6-impact.md .claude/skills/verify/SKILL.md .claude/rules/tests.md .ai/verification.md TAOM.Tests/Migration/GameAssemblies.cs TAOM.Tests/TAOM.Tests.csproj Main/Features/FieldCamp/Domain/CampState.cs Main/PatchCategoryIndex.cs Main/Features/CoopInterop/Missions/CoopMissionOwnership.cs Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs`.
> If a listed file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. Other files (new tests elsewhere) may have changed:
> that is expected, and why every count below is relative to your own Step 1 and Step 2 runs.

## Status

- **Priority**: P3
- **Effort**: S
- **Risk**: LOW (test files and one doc paragraph; no production code)
- **Depends on**: none
- **Category**: tests
- **Planned at**: commit `370b4bf2b`, 2026-10-28
- **Baseline at that commit** (reference values only; your oracles are your own Step 1 and Step 2
  runs, because trunk keeps adding tests: `f7a6368e5`, two commits later, records 27,957 passed):
  - Install: full suite 27,952 passed, 0 failed, 7 skipped; strict binding gate 818 of 818.
  - RefAsm (no-game): build 0 errors; unit step Failed 7, Passed 23,334, Skipped 2, Total 23,343
    (the 2 skips: `CargoSprite_IsDeclaredInTheTaomSpriteSheet` and the HitchProbe benchmark); no-game
    binding gate 591 of 591.
  - The 7 RefAsm failures are the subject of this plan (listed in "Current state"). Python suite: not
    touched by this plan, not re-run at the base.
- **Issue**: #1038 item 14

## Why this matters

`.ai/verification.md` documents a no-game check (three commands against BUTR's metadata-only
reference assemblies, the "RefAsm" build) for machines and reviewers without the game. Its second
command, the unit step, fails 7 tests on the base, so it cannot tell a reviewer anything: a new
failure hides among the known ones. The 7 tests run TaleWorlds code (a static constructor, a base
constructor, an attribute constructor) whose stub bodies throw, and carry no `RequiresGame` tag.
After this plan the unit step reports 0 failed, every one of the 7 still runs with the game, and the
one test whose engine dependency is incidental (reading a save id) runs on both paths with the same
strength. Optionally, on the maintainer's word (Step 0), `/verify` gains the unit step so the next
untagged test fails at once instead of piling up, as these 7 did after the hosted CI was removed in
`c8d69a0dc`.

## Current state

### How the RefAsm path works (do not change it)

- `TAOM.Tests/TAOM.Tests.csproj:55-65` (the stamp `ItemGroup`; its comment is lines 47-54): a RefAsm
  build (`-p:TaomGameRefs=RefAsm`) stamps the test DLL with `TaomGameFolder` =
  `$(TargetDir)refasm-game` and `TaomGameRefs` = `RefAsm` (lines 57-63):
  ```xml
  <_Parameter1>TaomGameFolder</_Parameter1>
  <_Parameter2 Condition="'$(TaomGameRefs)' != 'RefAsm'">$(GameFolder)</_Parameter2>
  <_Parameter2 Condition="'$(TaomGameRefs)' == 'RefAsm'">$(TargetDir)refasm-game</_Parameter2>
  ...
  <_Parameter1>TaomGameRefs</_Parameter1>
  <_Parameter2>$(TaomGameRefs)</_Parameter2>
  ```
  The stamp is a UTF-8 string in the DLL, so `grep -a -c "refasm-game"
  TAOM.Tests/bin/Debug/net472/TAOM.Tests.dll` shows which mode the last build stamped: at least 1
  after a RefAsm build, 0 after an Install build (the Install half was measured at the base; the
  RefAsm half is a draft you confirm in Step 2).
- `TAOM.Tests/Migration/GameAssemblies.cs`: `public static bool EnsureLoaded()` (line 47) and
  `public static List<string> Diagnostics` (line 41). A RefAsm test DLL loads only its stub folder,
  whatever `BANNERLORD_GAME_DIR` says, so `EnsureLoaded()` returns **true** on the stubs. An
  `if (!GameAssemblies.EnsureLoaded()) Assert.Inconclusive(...)` guard therefore does not keep a test
  off the stubs; only a tag does. An Install test DLL uses `BANNERLORD_OVERRIDE_DIR`, else
  `BANNERLORD_GAME_DIR`, else its stamped `GameFolder`.
- `.claude/rules/tests.md`, "Test categories (builds without the game)", the rule you apply:
  > Tag the class `[TestCategory("RequiresGame")]` when a test executes engine code (constructing a
  > `Vec2`, `TextObject`, `ExplainedNumber` or `CampaignBehaviorBase` counts, even through TAOM
  > code) ... A class tag is the default even when some of its tests would pass on the stubs; you may
  > tag only the methods that need the game when that returns checks worth running there
  > (`Patch86HideoutBossFightBindingTests`). ... tag it, never catch the exception.
- RefAsm and Install builds share `TAOM.Tests/bin` and `obj`. When you switch modes, build with
  `--no-incremental`, so the copied TaleWorlds DLLs and both stamps are certainly the new mode's.
- The tag style in this repo is the bare attribute on its own line, no trailing comment (commit
  `8589a3384` added 15 that way: 13 `RequiresGame` and 2 `RequiresGameIL`). Exemplar class tag:
  `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionAuthorityTests.cs:18-20`:
  ```csharp
  [TestClass]
  [TestCategory("RequiresGame")]
  public class CoopMissionAuthorityTests
  ```

### Failure 1 to 4: `CoopMissionOwnershipTests` (4 tests)

`TAOM.Tests/Features/CoopInterop/Missions/CoopMissionOwnershipTests.cs` (68 lines, 4 test methods,
all failing): `IsOwnedHere_NoContainer_Owned`, `IsOwnedHere_ResolverThrows_OwnedAndAsksAgain`,
`IsOwnedHere_ResolverReturnsNull_OwnedAndAsksAgain`,
`IsOwnedHere_WithAuthority_DelegatesLiveAndResolvesOnce`.

```csharp
// CoopMissionOwnershipTests.cs:14-17
[TestClass]
public class CoopMissionOwnershipTests
{
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
```

Cause: `TaleWorlds.MountAndBlade.Agent` is declared
`public sealed class Agent : DotNetObject, IAgent, IFocusable, IUsable, IFormationUnit, ITrackableBase`
(decompiled `TaleWorlds.MountAndBlade.Agent.cs:14`, via `pwsh tools/taom-src.ps1 path
TaleWorlds.MountAndBlade.Agent`). `GetUninitializedObject` skips the instance constructor but still
runs `DotNetObject`'s static constructor, which throws on the stubs: a `TypeInitializationException`.

The fix: add `[TestCategory("RequiresGame")]` to the class, as its sibling `CoopMissionAuthorityTests`
does. Rejected: `BareAgent() => null`. `CoopMissionOwnership.IsOwnedHere`
(`Main/Features/CoopInterop/Missions/CoopMissionOwnership.cs:32-36`) only forwards the agent:
```csharp
public static bool IsOwnedHere(Agent? agent)
{
    var authority = Authority();
    return authority == null || authority.IsLocallyControlled(agent);
}
```
so with a null agent the fourth test (`authority.IsLocallyControlled(agent).Returns(true, false, true)`)
could no longer tell "forwards the agent" from "always forwards null". That weakens the test on every
path to gain coverage on one path.

### Failure 5: `EnlistmentHourlyTickTests.OnHourlyTick_WithoutAuthority_TouchesNothing`

`TAOM.Tests/Features/Enlistment/EnlistmentHourlyTickTests.cs` (45 lines, 1 test method):
```csharp
// :16-17
[TestClass]
public class EnlistmentHourlyTickTests
// :23-33, [TestInitialize] Setup
        _sut = new EnlistmentBehavior(
            Substitute.For<IEnlistmentStore>(), Substitute.For<IEnlistmentStateMachine>(), _reconciler,
            ...
```
Cause: `Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs:16` is
`public class EnlistmentBehavior : CampaignBehaviorBase`, and the engine base constructor
(`public CampaignBehaviorBase() { StringId = GetType().Name; }`, decompiled
`TaleWorlds.CampaignSystem.CampaignBehaviorBase.cs:12-15`) has a throwing body on the stubs: a
`NullReferenceException`. `tests.md` names `CampaignBehaviorBase` as engine code. The fix: class tag
`RequiresGame`. A seam would be a production change for one test class (`simplicity-criterion.md`:
tiny win plus added complexity, reject).

### Failure 6: `CampActivitySettingsTests.TheCampRecord_SavesTheFourWorkFields_UnderUniqueIds_ContinuingTheBlock`

`TAOM.Tests/Features/FieldCamp/CampActivitySettingsTests.cs:171-184`:
```csharp
    [TestMethod]
    public void TheCampRecord_SavesTheFourWorkFields_UnderUniqueIds_ContinuingTheBlock()
    {
        var ids = typeof(CampState).GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => (field: f.Name, attribute: f.GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == "SaveableFieldAttribute")))
            .Where(x => x.attribute != null)
            .ToDictionary(x => x.field, x => System.Convert.ToInt16(Read(x.attribute!, "LocalSaveId")));

        Assert.AreEqual(108, ids[nameof(CampState.Activity)]);
        Assert.AreEqual(109, ids[nameof(CampState.ActivityHoursDone)]);
        Assert.AreEqual(110, ids[nameof(CampState.ActivityTargetHours)]);
        Assert.AreEqual(111, ids[nameof(CampState.InjuryRolled)]);
        Assert.AreEqual(ids.Count, ids.Values.Distinct().Count(), "a save id used twice corrupts the record");
    }
```
`Read` is the file's helper at line 144
(`private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;`);
other tests in the file still use it, so keep it.

The fields it reads, `Main/Features/FieldCamp/Domain/CampState.cs:24-39`:
```csharp
    [SaveableField(101)] public int Type;
    [SaveableField(102)] public CampaignTime EstablishedAt;
    ...
    [SaveableField(107)] public bool VisualShown;
    ...
    [SaveableField(108)] public int Activity;
    [SaveableField(109)] public float ActivityHoursDone;
    [SaveableField(110)] public float ActivityTargetHours;
    [SaveableField(111)] public bool InjuryRolled;
```
Cause: `GetCustomAttributes` constructs each attribute. The engine type, decompiled
`TaleWorlds.SaveSystem.SaveableFieldAttribute.cs` (`pwsh tools/taom-src.ps1 path
TaleWorlds.SaveSystem.SaveableFieldAttribute`):
```csharp
[AttributeUsage(AttributeTargets.Field)]
public class SaveableFieldAttribute : Attribute
{
	public short LocalSaveId { get; set; }

	public SaveableFieldAttribute(short localSaveId)
	{
		LocalSaveId = localSaveId;
	}
}
```
On the stubs that constructor's body throws (`NullReferenceException`). The save id is the
constructor's one argument (`short localSaveId`), so reading the attribute DATA
(`FieldInfo.GetCustomAttributesData()`, which reads metadata and runs no constructor) gives the same
value without engine code. The assertion keeps its strength on both paths. Fallback, only if that
fails on the stubs: tag that one method `RequiresGame`.

### Failure 7: `Patch109WiringTests.Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses`

`TAOM.Tests/Features/TelcharsMark/Patch109WiringTests.cs:50-73` (the file already has
`using TAOM.Tests.Migration;` at line 12 and uses `GameAssemblies` at line 106):
```csharp
    // The three classes live in TAOM.Client (ADR-013, B5). The category pass builds its index from the loaded TAOM
    // assemblies, so on a client the category holds them; on a server (no client) it holds none and applies nothing.
    [TestMethod]
    public void Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses()
    {
        var previous = ClientComposition.Current;
        ClientComposition.Current = new TaomClientComposition();
        try
        {
            var index = PatchCategoryIndex.Build(ClientComposition.LoadedTaomAssemblies());
            var classes = index.ClassesOf(Category);

            Assert.AreEqual(0, index.SkippedClasses.Count,
                "A skipped class belongs to no category and applies nothing: "
                + string.Join("; ", index.SkippedClasses.Select(k => k.Key.FullName + " (" + k.Value.GetType().Name + ")")));
```
Cause: `PatchCategoryIndex.Build` (`Main/PatchCategoryIndex.cs:50-80`) calls
`HarmonyMethodExtensions.GetFromType(type)` (line 64) inside a per-class `try`, which constructs every
class attribute; a throw puts the class in `SkippedClasses`. On the stubs five TAOM classes throw
`NullReferenceException` there (`GauntletSupplyOrderScreen`, `HostReviewMissionViews`,
`GauntletCareerScreen`, `BattleActionBarMissionView`, `AutonomousMovementPlayerController`; this list
was read from a stub run on `7bac0c9d4`, not re-listed on the base), so the `SkippedClasses.Count`
assertion fails. Its twin, `TAOM.Tests/Features/ManyHammers/ManyHammersWiringTests.cs:122-128`, got
the method tag in `8589a3384`:
```csharp
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses()
    {
        // Classes that name SandBox.View types only load once the game assemblies resolve; alone, the index skips them.
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
```
A method tag (not a class tag) is right here: the class's other untagged tests
(`Index_EmptyClientSlot_HoldsNone`, `SubModule_AppliesTheCategory_AndIoCRegistersTheFeature`,
`ToggleButton_IsAppendedAsTheLastChild`) pass on the stubs and are worth running there. The class's
two other tests carry `BindingVerification` and are outside the unit step.

A second, Install-path defect (a lead measured on `7bac0c9d4`, which Step 1 re-measures): the Patch109
test has no `EnsureLoaded()` guard, so run on its own (`--filter
FullyQualifiedName~Patch109WiringTests`) it failed with 55 `FileNotFoundException` skips; it passes in
the full suite only because an earlier class had loaded the module assemblies. The twin's guard fixes
that. Add the guard only if Step 1 reproduces exactly that standalone failure on the base.

### Doc that states the failures

`docs/migration/v1.5.6-impact.md:436-449`, the "RefAsm (no-game) test path" bullet in "## Owed"
(line 411), says (lines 439-443):
```
  `BANNERLORD_GAME_DIR` says. The no-game binding gate passes 591 of 591; as documented before, it
  failed 509 of 593. The unit step fails 7 tests (23,334 passed, 2 skipped), with the variable set
  or unset, and plan 044 hands them off: `CoopMissionOwnershipTests` (4, `TypeInitializationException`
  in `DotNetObject`), `EnlistmentHourlyTickTests` and `CampActivitySettingsTests`
  (`NullReferenceException` from engine constructors), and
```
The same file's "## Done in this bump" section (line 347) ends with the "Parley icon re-base" bullet
(lines 380-381), followed by a blank line and "## Adopted engine code (yotthani and MithrilForge)"
(line 383). Historical records stay as written: `docs/reviews/rca-nogame-gate-2026-10-28.md`,
`docs/reviews/lessons/testing-qa.md:2326` and the REVIEW-LOG are dated records of the gate fix.

### Blast radius

The plan changes four test classes and no production type. `python tools/graphify_taom.py affected
"<Type>" --depth 2` for each of `CoopMissionOwnershipTests`, `EnlistmentHourlyTickTests`,
`CampActivitySettingsTests` and `Patch109WiringTests` printed "No affected nodes found." at the base;
the graph was STALE then (13 code files changed since its build), and nothing references a test class
in any case. Step 1 re-runs it after a refresh, as a record.

### Binding conventions

- ADR-008 (service testability): services are tested through fakes; this plan changes no service.
- ADR-007 (adapters): sealed TaleWorlds types such as `Agent` stay behind adapters in services; the
  tests here touch `Agent` only as an opaque value handed to a fake, which is why the class needs the
  game rather than a production seam.
- ADR-002 (entry points under 150 lines): not touched.
- `.claude/rules/tests.md` "Test categories": tag, never catch the stub exceptions; a class tag is
  the default; a method tag when the class's other checks are worth running on the stubs.
- `.claude/rules/simplicity-criterion.md`: no production seam for one test class.
- AGENTS.md "Banned constructs": no `#if`, `#region` or `[Obsolete]` in what you add.

## Step 0: the maintainer's decision (a `/verify` row for the no-game unit step)

Not a protected file; the brief keeps this optional and on the maintainer's word. The orchestrator
asks once and puts the answer in your dispatch prompt.

- **A (recommended):** `/verify` gains a Step 2e that runs the RefAsm build and unit step in a
  temporary detached worktree (so it never rewrites the checkout's `TAOM.Tests/bin`, which Steps 2c
  and 2d read), carrying the checkout's uncommitted and untracked changes, and fails `/verify` on any
  failure. Measured cost on 2026-10-28: about 12 s for an incremental RefAsm build and 47 to 72 s for
  the unit step; a fresh worktree adds a full build, about 2 minutes end to end.
- **B (rejected):** a static source scan for engine constructors in untagged tests. It misses
  indirect engine calls through TAOM code, which is how 3 of these 4 classes fail.
- **C (the maintainer's call):** a hosted CI job; `c8d69a0dc` removed the workflows on purpose.

**Executor check, first**: read your dispatch prompt. If it says the maintainer chose A, do Step 9.
If it names B, C, no answer, or anything else, skip Step 9 and say so in your report. Never build B or C.

## Commands you will need

Prefix every dotnet command with the TEMP and TMP your dispatch rules give, and run each with a
15-minute timeout. Never `./build.ps1`: it deploys into the game install. Run every step in one shell
environment: do not set, unset or change `BANNERLORD_GAME_DIR` or `BANNERLORD_OVERRIDE_DIR` between
Step 1 and Step 7, so the Install totals compare like with like (a RefAsm test DLL ignores both).

| Purpose | Command | Expected on success |
|---|---|---|
| Build (production) | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Install test build | `dotnet build TAOM.Tests --no-incremental -p:DisableModuleCopy=true -p:ModuleId=` | 0 Error(s) |
| Install full suite | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | Failed 0; totals per step |
| Install strict gate | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter TestCategory=BindingVerification` | Failed 0, Passed = Total |
| Patch109 alone (Install) | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --logger "console;verbosity=normal" --filter "FullyQualifiedName~Patch109WiringTests"` | per step |
| RefAsm build | `dotnet build TAOM.Tests --no-incremental -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` | 0 Error(s) |
| RefAsm unit step | `dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | per step |
| RefAsm gate | `dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall"` | Failed 0, Passed = Total |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |

Three more commands carry a `|` inside their filter, so they are spelled here in full rather than in
the table. Each has exactly one `--filter`; never add a second one.

The **Install four-class subset**:
```
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --logger "console;verbosity=normal" --filter "FullyQualifiedName~CoopMissionOwnershipTests|FullyQualifiedName~EnlistmentHourlyTickTests|FullyQualifiedName~CampActivitySettingsTests|FullyQualifiedName~Patch109WiringTests"
```
The **RefAsm four-class run** (the unit step's category filter, narrowed to the four classes):
```
dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --logger "console;verbosity=normal" --filter "(FullyQualifiedName~CoopMissionOwnershipTests|FullyQualifiedName~EnlistmentHourlyTickTests|FullyQualifiedName~CampActivitySettingsTests|FullyQualifiedName~Patch109WiringTests)&TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"
```
The **RefAsm CampActivity run**:
```
dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --logger "console;verbosity=normal" --filter "FullyQualifiedName~CampActivitySettingsTests&TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"
```
With `--logger "console;verbosity=normal"`, `dotnet test` prints one `Passed <name>`, `Failed <name>`
or `Skipped <name>` line per test; the default output lists only failures, so use the logger
wherever a step checks which names ran.

The RefAsm build and unit step and gate are the three commands from `.ai/verification.md` ("Without
the game ... run these three commands in order"), with `--no-incremental` on the build because you
switch modes. All commands use the Debug configuration; never add `-c Release` to one of them.

Throughout, "totals" means the four numbers of the `dotnet test` summary line (`Failed: F, Passed: P,
Skipped: S, Total: T`). Write each run's totals into your report as you go; later steps compare
against them by name, for example "Step 2 unit-step Total".

## Scope

**In scope** (the only files you modify):
- `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionOwnershipTests.cs`: class tag.
- `TAOM.Tests/Features/Enlistment/EnlistmentHourlyTickTests.cs`: class tag.
- `TAOM.Tests/Features/FieldCamp/CampActivitySettingsTests.cs`: the one method's attribute read.
- `TAOM.Tests/Features/TelcharsMark/Patch109WiringTests.cs`: method tag, and the guard when Step 1
  reproduces the standalone failure.
- `docs/migration/v1.5.6-impact.md`: the "RefAsm (no-game) test path" bullet only (rewritten and
  moved from "## Owed" to the end of "## Done in this bump").
- `.claude/skills/verify/SKILL.md`: only when Step 0's answer is A (Step 9).

**Out of scope** (do NOT touch, even though they look related):
- Production code: `Main/**`, including `CoopMissionOwnership.cs`, `EnlistmentBehavior.cs`,
  `PatchCategoryIndex.cs`, `CampState.cs`. No seam for a test.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` (single-owner), `Directory.Build.props`,
  `.claude/settings*.json`, `docs/adrs/*.md` (protected): nothing in this plan needs them; if it seems
  to, STOP.
- `TAOM.Tests/TAOM.Tests.csproj`, `GameReferences.targets`, `TAOM.Tests/Migration/GameAssemblies.cs`,
  `.ai/verification.md`, `.claude/rules/tests.md`: the RefAsm mechanism and its rule are correct as
  they stand.
- `ManyHammersWiringTests.cs` and `CreatureBanditsWiringTests.cs`: they pass on the base's stubs.
- Any other test that fails on the stubs (one added after the base): the Step 2 STOP covers it.
- The historical records (`docs/reviews/rca-nogame-gate-2026-10-28.md`, `docs/reviews/lessons/*.md`,
  `docs/reviews/REVIEW-LOG.md`).
- The gates themselves. Never turn the unit step green by deleting or `[Ignore]`-ing a test,
  loosening an assertion, wrapping a test in `try`/`catch`, or adding a category the filters exclude
  other than `RequiresGame` on the tests this plan names. STOP and report instead.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.36` at planning time; a hook
  refuses any other). Suggested: `fix(tests): v2.0.36 - no-game unit step passes with 0 failures`
  (check the version first).
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. Never edit `CHANGELOG.md`. No `Co-Authored-By` or other AI attribution. Add `Not-tested:` for
  the laptop, a Release build and an IDE test runner. Write the message to a file and run
  `git commit -F "<file>"`. Stage explicit paths only. Never `--no-verify`.

## Steps

### Step 1: record the Install base, the four-class subset and the Patch109 standalone run

Build in Install mode with `--no-incremental`, then run, in this order: the Install full suite, the
Install strict gate, the Install four-class subset and Patch109 alone (Install), all from "Commands
you will need". Then record the blast radius:

```
timeout 900 python tools/graphify_taom.py refresh --if-stale
python tools/graphify_taom.py affected "CoopMissionOwnershipTests" --depth 2
python tools/graphify_taom.py affected "EnlistmentHourlyTickTests" --depth 2
python tools/graphify_taom.py affected "CampActivitySettingsTests" --depth 2
python tools/graphify_taom.py affected "Patch109WiringTests" --depth 2
```

**Verify**:
- Full suite: Failed 0. Record its totals as "Step 1 Install totals" (27,952 / 0 / 7 at the base;
  a later base has more).
- Strict gate: Failed 0 and Passed equals Total. Record the Total as "Step 1 gate Total".
- Four-class subset: record its totals as "Step 1 subset totals". Failed 0, and all 7 tests named
  in "Current state" appear as `Passed` lines; or, only when Patch109 alone gives outcome 2 below,
  Failed 1, that failure being `Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses` with
  the same `FileNotFoundException` list (the subset may run it before anything loads the module
  assemblies). Any other failure is a STOP.
- Graphify: quote each `affected` output in your report. A refresh that times out or fails is
  recorded (its last lines) and is not a STOP: no type outside the tests references these classes.
- Patch109 alone: record its totals line. Exactly one of three outcomes:
  1. 0 failed, and `Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses` passed: the guard
     is not needed; Step 5 adds the tag only.
  2. Only `Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses` failed, with the "A skipped
     class belongs to no category" message, and every class it lists carries `(FileNotFoundException)`:
     this is the RED for the guard in Step 5. Quote the message (or its first 300 characters).
  3. Anything else (another test failed, or the listed classes show another exception type): STOP.

### Step 2: record the RefAsm base (the RED for this plan)

Run the RefAsm build (`--no-incremental`), then
`grep -a -c "refasm-game" TAOM.Tests/bin/Debug/net472/TAOM.Tests.dll`, then the RefAsm unit step,
the RefAsm gate and the RefAsm four-class run.

**Verify**:
- Build: 0 Error(s). The grep prints a number of at least 1 (the RefAsm stamp is in the DLL).
- Unit step: Failed 7. Record its totals as "Step 2 unit-step totals" (Passed 23,334, Skipped 2,
  Total 23,343 at the base; a later base has more). The 7 failing names are exactly the four
  `CoopMissionOwnershipTests.IsOwnedHere_*` tests,
  `EnlistmentHourlyTickTests.OnHourlyTick_WithoutAuthority_TouchesNothing`,
  `CampActivitySettingsTests.TheCampRecord_SavesTheFourWorkFields_UnderUniqueIds_ContinuingTheBlock`
  and `Patch109WiringTests.Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses`. Read class
  names from the stack frame or the fully qualified name, never from memory. Record the names of the
  skipped tests too.
- Four-class run: Failed 7, the same 7. Record its totals as "Step 2 four-class totals". The messages
  show the causes: a `TypeInitializationException` for the 4 `IsOwnedHere_*` tests, a
  `NullReferenceException` from a `TaleWorlds` frame for the Enlistment and CampActivity tests, and
  the "A skipped class belongs to no category" assertion for Patch109 (quote the class list it
  prints into your report).
- Gate: Failed 0 and Passed equals Total. Record the Total as "Step 2 gate Total".

### Step 3: tag `CoopMissionOwnershipTests` and `EnlistmentHourlyTickTests`

In `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionOwnershipTests.cs`, add the line
`[TestCategory("RequiresGame")]` between `[TestClass]` (line 14) and `public class
CoopMissionOwnershipTests` (line 15). Change nothing else in the file (`BareAgent()` stays).

In `TAOM.Tests/Features/Enlistment/EnlistmentHourlyTickTests.cs`, add the same line between
`[TestClass]` (line 16) and `public class EnlistmentHourlyTickTests` (line 17).

**Verify**: `git diff --stat` shows 1 insertion in each of the two files; the RefAsm build (incremental
is fine now, you stay in RefAsm mode:
`dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=`) gives
0 Error(s); the RefAsm four-class run gives Failed 2 (the CampActivity and Patch109 tests) and Total =
Step 2 four-class Total minus 5, and no `CoopMissionOwnershipTests` or `EnlistmentHourlyTickTests`
name appears in its per-test lines.

### Step 4: read the save ids from the attribute data in `CampActivitySettingsTests`

In `TAOM.Tests/Features/FieldCamp/CampActivitySettingsTests.cs`, method
`TheCampRecord_SavesTheFourWorkFields_UnderUniqueIds_ContinuingTheBlock`, replace lines 174-177 with:

```csharp
        var ids = typeof(CampState).GetFields(BindingFlags.Public | BindingFlags.Instance)
            // The attribute data, not the attribute: its constructor is engine code, and the id is its one argument.
            .Select(f => (field: f.Name, attribute: f.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "SaveableFieldAttribute")))
            .Where(x => x.attribute != null)
            .ToDictionary(x => x.field, x => System.Convert.ToInt16(x.attribute!.ConstructorArguments[0].Value));
```

Leave the five assertions and the `Read` helper unchanged (`Read` is still used by other tests).
Before keeping the comment, re-check it against the decompiled attribute quoted in "Current state":
the constructor `SaveableFieldAttribute(short localSaveId)` takes exactly one argument, the id.
`System.Reflection` is already imported (line 2); `CustomAttributeData` lives there.

**Verify**:
1. RefAsm build (incremental): 0 Error(s). The RefAsm CampActivity run: Failed 0, and a
   `Passed TheCampRecord_SavesTheFourWorkFields_UnderUniqueIds_ContinuingTheBlock` line appears.
2. Mutation: change `Assert.AreEqual(108, ids[nameof(CampState.Activity)]);` to `107`, rebuild in
   RefAsm mode, rerun the RefAsm CampActivity run: Failed 1, that one test, with an `Assert.AreEqual`
   message (expected 107, actual 108). Restore `108` with the Edit tool (never with git), rebuild,
   rerun: Failed 0. Quote both totals lines.
3. If item 1 fails on the stubs (any exception from `GetCustomAttributesData`, `ConstructorArguments`
   or a `TaleWorlds` frame), the assumption of this step is false: put lines 174-177 back to their
   original text with the Edit tool, add `[TestCategory("RequiresGame")]` on its own line between
   `[TestMethod]` (line 171) and the method, rebuild, rerun (the method no longer appears; Failed 0),
   and record the exception in your report. This is "the Step 4 fallback" below.

### Step 5: tag `Patch109WiringTests.Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses`

In `TAOM.Tests/Features/TelcharsMark/Patch109WiringTests.cs`, add
`[TestCategory("RequiresGame")]` on its own line between `[TestMethod]` (line 52) and
`public void Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses()` (line 53).

Only if Step 1 gave outcome 2: add, as the first lines of the method body (before
`var previous = ClientComposition.Current;`), the twin's guard:

```csharp
        // Classes that name module-assembly types only load once the game assemblies resolve; alone, the index skips them.
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
```

No class tag: the class's other untagged tests pass on the stubs.

**Verify**: RefAsm build 0 Error(s); the RefAsm four-class run gives Failed 0 and Total = Step 2
four-class Total minus 6 (minus 7 with the Step 4 fallback), and no
`Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses` line appears in it.

### Step 6: the RefAsm check passes

Run the RefAsm build with `--no-incremental` (to prove a clean build, not the incremental one), the
RefAsm unit step and the RefAsm gate, exactly as in the table.

**Verify**:
- Build: 0 Error(s).
- Unit step: Failed 0; Skipped = Step 2 unit-step Skipped, the same skipped names; Total = Step 2
  unit-step Total minus 6 (the 6 tests the tags take out of the filter); Passed = Step 2 unit-step
  Passed plus 1 (CampActivity now passes). With the Step 4 fallback: Total minus 7, Passed plus 0.
  At the base that is Passed 23,335, Total 23,337 (23,334 and 23,336 with the fallback).
- Gate: Failed 0, and Total = Step 2 gate Total (591 at the base).

### Step 7: the Install check passes

Build the production project, then the Install test build (`--no-incremental`, you switch modes),
then `grep -a -c "refasm-game" TAOM.Tests/bin/Debug/net472/TAOM.Tests.dll`, then the Install
four-class subset, Patch109 alone (Install), the Install full suite and the Install strict gate.

**Verify**:
- Both builds 0 errors. The grep prints 0 (the Install stamp).
- Four-class subset: Failed 0, Total and Skipped equal the Step 1 subset's, Passed = Step 1 subset
  Passed plus Step 1 subset Failed, and all 7 formerly failing tests appear as `Passed` lines (the
  tags remove nothing from an Install run).
- Patch109 alone: Failed 0, and a `Passed Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses`
  line appears (not skipped, not inconclusive).
- Full suite: totals equal the Step 1 Install totals (Failed 0). This is the run you quote.
- Strict gate: Failed 0, Total = Step 1 gate Total.

### Step 8: rewrite and move the impact record's RefAsm bullet

In `docs/migration/v1.5.6-impact.md`, cut the whole "RefAsm (no-game) test path" bullet from
"## Owed" (lines 436-449 at planning) and put the rewritten bullet at the end of "## Done in this
bump", after the "Parley icon re-base" bullet and before the blank line above "## Adopted engine
code". It moves because nothing in it is owed after this plan. The rewritten bullet states the
state after this plan with the numbers from YOUR Step 6 and Step 7 runs. Keep its facts that still
hold (the BUTR pin `1.5.6.125053-beta`, the stamp and its precedence, the gate result from Step 6
and the earlier 509 of 593, the `ManyHammersWiringTests` misattribution and the 29 failures of
`c03ed426d`, the RCA link). A draft for the new part, which you check against the file and your
runs before using:

> The unit step passes: 0 failed, `<P>` passed, `<S>` skipped, since `CoopMissionOwnershipTests` and
> `EnlistmentHourlyTickTests` carry `RequiresGame` (`DotNetObject`'s static constructor and
> `CampaignBehaviorBase`'s constructor run engine code), so does the method
> `TelcharsMark.Patch109WiringTests.Index_FromTheLoadedTaomAssemblies_HoldsTheThreeClientClasses`
> (its patch index constructs engine attributes), and `CampActivitySettingsTests` reads the camp
> record's save ids from the attribute data instead of constructing `SaveableFieldAttribute`.

Adjust the last clause if the Step 4 fallback was taken. Remove "plan 044 hands them off". No em or
en dash; wrap at the file's width (about 100 characters).

**Verify**: `python tools/lint_docs.py --fail-on-drift` exits 0;
`git grep -n "unit step fails 7\|plan 044 hands them" -- docs/migration` prints nothing; and
`grep -n "^## Owed\|^## Done in this bump\|^## Adopted engine code\|RefAsm (no-game) test path" docs/migration/v1.5.6-impact.md`
prints four lines in the order "## Done in this bump", "RefAsm (no-game) test path", "## Adopted
engine code", "## Owed" (the bullet appears once, inside "Done in this bump").

### Step 9: only when Step 0's answer is A: add Step 2e to `/verify`

In `.claude/skills/verify/SKILL.md`, insert this section after "## Step 2d: API snapshot reproduces"
and before "## Step 3: Git Status":

````markdown
## Step 2e: No-game unit step

Nothing else runs the no-game path since the hosted CI was removed (`c8d69a0dc`), so a test that
runs engine code without `[TestCategory("RequiresGame")]` passes in Step 2 and fails only on BUTR's
stubs (`.claude/rules/tests.md` "Test categories"). Run the unit step from `.ai/verification.md` in
a temporary detached worktree that carries this checkout's uncommitted and untracked changes, so
this checkout's `TAOM.Tests/bin` keeps the Install build that Steps 2c and 2d read. Run the block
from the repository root: `git ls-files` and `cp --parents` take paths relative to it.

```bash
# Beside the checkout, so the build stays on the checkout's drive (never C: on the desktop).
wt="$(dirname "$(git rev-parse --show-toplevel)")/taom-verify-refasm-$(date +%s)"
git worktree add --detach "$wt" HEAD
git diff HEAD --binary > "$wt.patch"
if [ -s "$wt.patch" ]; then git -C "$wt" apply "$wt.patch"; fi
git ls-files --others --exclude-standard -z | xargs -0 -r cp --parents -t "$wt"
(cd "$wt" && set -o pipefail \
  && dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -3 \
  && dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= \
     --filter "TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification" 2>&1 | tail -20)
rm -rf "$wt" "$wt.patch"
git worktree prune
```

`set -o pipefail` makes a failed build stop the block before the tests run. Never
`git worktree remove --force`: the confirm hook asks on it; `rm -rf` and `git worktree prune`
remove the folder and its record. Report the totals line. Any failure FAILS the verify: tag the
test by the rule in `tests.md`, never catch the exception. No totals line, or a total under 20,000,
means the step broke: NOT RUN, not a pass. A failed build or apply is NOT RUN.
````

and add `No-game:    [X/Y passed, Z failed | NOT RUN]` to the report block under `API snapshot:`.

You cannot prove this step yourself (your dispatch rules forbid creating worktrees): the orchestrator
runs it once before merge. Say so in your report.

**Verify**: `git diff --stat -- .claude/skills/verify/SKILL.md` shows the file changed;
`python -B -m unittest tools.tests.test_claude_config_lint` ends with `OK` (it lints every SKILL.md's
frontmatter; 22 tests, OK, at the base); `python tools/lint_docs.py --fail-on-drift` exits 0.

### Step 10: commit

Stage the in-scope files you changed by explicit path, write the message to a file, and commit with
`git commit -F "<file>"`. The body says: the no-game unit step now reports 0 failed (quote Step 6's
totals); which tests got `RequiresGame` and why (engine code on the stubs); that the camp record test
reads the save ids from the attribute data and still fails on a wrong id (the Step 4 mutation); the
Patch109 guard if added and the standalone failure it fixes; the Install totals from Step 7; the
Step 9 row if added. Name `#1038 item 14`.

**Verify**: `git status --porcelain` is empty for the in-scope files and lists nothing you created;
`git log -1 --stat` shows only in-scope paths.

## Test plan

- No new tests. The RED is Step 2's RefAsm run (7 failures with named exceptions) and, for the
  Patch109 guard, Step 1's standalone run (outcome 2). The GREEN is Step 6 (0 failed on the stubs)
  and Step 7 (every one of the 7 still passes with the game, and the Install totals are unchanged).
- The CampActivity rewrite is proven by the Step 4 mutation: a wrong expected id fails on the stubs.
- Pattern: `CoopMissionAuthorityTests.cs:18-20` for the class tags;
  `ManyHammersWiringTests.cs:122-128` for the Patch109 method tag and guard.
- Not tested: the laptop, a Release build, an IDE test runner (the commit's `Not-tested:` trailer).

## Done criteria

ALL must hold (the "Step N" values are the totals you recorded in those steps):

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] RefAsm unit step: Failed 0, Skipped = Step 2's, Total = Step 2's minus 6 and Passed = Step 2's
      plus 1 (minus 7 and plus 0 with the Step 4 fallback).
- [ ] RefAsm gate: Failed 0, Total = Step 2's.
- [ ] Install full suite totals equal Step 1's (Failed 0); strict gate Failed 0, Total = Step 1's.
- [ ] Install four-class subset: Failed 0, Total and Skipped equal Step 1's, all 7 formerly failing
      tests `Passed`.
- [ ] Install, Patch109 alone: Failed 0.
- [ ] `git grep -n -i "unit step fails 7\|plan 044 hands them" -- . ':!plans' ':!docs/reviews'` prints nothing.
- [ ] `git grep -n "GetCustomAttributes(inherit: false).FirstOrDefault(a => a.GetType().Name == \"SaveableFieldAttribute\")" -- TAOM.Tests`
      prints nothing (unless the Step 4 fallback was taken).
- [ ] `git grep -n -c "TestCategory(\"RequiresGame\")" -- TAOM.Tests/Features/CoopInterop/Missions/CoopMissionOwnershipTests.cs TAOM.Tests/Features/Enlistment/EnlistmentHourlyTickTests.cs TAOM.Tests/Features/TelcharsMark/Patch109WiringTests.cs`
      prints 1 for each file.
- [ ] `git status --porcelain` lists only in-scope files (none, after the commit).
- [ ] Every comment and doc line this plan supplied was re-checked against the code it describes.

## STOP conditions

Stop and report (do not improvise) if:

- Step 2's RefAsm unit step fails any test other than these 7, or not all 7: someone added an
  untagged test (or changed one). Record its name, stack and the rule it breaks; do not tag it
  without the orchestrator's word.
- Step 1's Install full suite has a failure, or its strict gate does not pass every test it runs.
- Step 1's Patch109 standalone run gives outcome 3.
- Step 1's four-class subset fails anything other than the outcome-2 case its Verify allows.
- Step 7's four-class subset or full suite has a failure, or a Total that differs from Step 1's,
  after your edits: an edit changed behaviour.
- A test can be made green only by catching a stub exception, `[Ignore]`, deleting a test or weakening
  an assertion.
- The code at the "Current state" locations does not match the excerpts.
- A step's verification fails twice after a reasonable fix.
- The fix seems to need `Main/**`, `Main/IoC.cs`, `Main/SubModule.cs`, a csproj,
  `Directory.Build.props` or another out-of-scope file.
- After a RefAsm build, `grep -a -c "refasm-game" TAOM.Tests/bin/Debug/net472/TAOM.Tests.dll` prints
  0 (Step 2), or after an Install build it prints more than 0 (Step 7): the base's stamp mechanism
  changed.

## Orchestrator steps (not the executor's)

- Issue: #1038 item 14 already tracks this; no new issue.
- Step 0: ask the maintainer once (A, B or C) and put the answer in the dispatch prompt.
- If Step 9 landed: run the new `/verify` Step 2e once on the branch and record its totals; it creates
  and removes a worktree, which an executor may not.
- The commit touches C#: run `/deep-review`, then `/verify`, on the branch before merge; merge on the
  maintainer's word.
- No `/localize` (no player-facing text), no feature doc or feature-map row (test-only change).

## After merge: the maintainer's actions

None. If Step 9 landed, `/verify` takes about 2 minutes longer.

## Maintenance notes

- Any new test that constructs an `Agent` (even uninitialised), a `CampaignBehaviorBase` subclass, a
  TaleWorlds attribute through `GetCustomAttributes`, or runs `PatchCategoryIndex.Build` over the
  TAOM assemblies needs `RequiresGame`. Without Step 9, nothing catches a missing tag until someone
  runs the RefAsm commands by hand.
- Review probes: that the CampActivity test still fails on a wrong id (the mutation); that the tags
  removed nothing from the Install run (Step 7 subset and full-suite totals equal Step 1's); that the
  Patch109 guard, if added, is justified by Step 1's outcome 2.
- Deferred: the static-scan gate (B) is rejected; the hosted CI (C) is the maintainer's call.
