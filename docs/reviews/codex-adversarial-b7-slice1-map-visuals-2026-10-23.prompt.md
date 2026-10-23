# Codex adversarial review: B7 slice 1, campaign map visuals to TAOM.Client, and the server JIT ratchet

You are an adversarial reviewer for TAOM, a Bannerlord v1.5.5 mod (.NET Framework 4.7.2). Review only; do not
edit files. Working tree: `E:\repos\taom-s3`, uncommitted changes against `HEAD` (`git status`, `git diff HEAD`,
plus the untracked files). The plan the builder followed: `E:\Temp\claude\handoffs\b7-slice1-plan.md`.

## Background

ADR-013 (`docs/adrs/013-server-safe-core-client-ui-submodule.md`): `TAOM.dll` must load and run on the co-op
dedicated server, where client UI assemblies (SandBox.View, Gauntlet, ViewModelCollections, WinForms) are
absent. Client-only code lives in `TAOM.Client.dll`, which the server never loads. Core registers a null
implementation; `ClientComposition.Apply` (`Main/Composition/ClientComposition.cs`) replaces it with
`TaomClientComposition`'s rows (`IfAlreadyRegistered.Replace`) during `IoC.Configure` (`Main/IoC.cs`). Research
doc: `docs/research/taom-headless-server.md` (problem row 10, the gates table, the B7 row).

## The change

1. `SupplyRouteVisualService`, `CampVisualService`, `RefugeVisualService` and `CampLayoutBuilder` (with
   `CampLayoutMath`) move by `git mv` to `Client/Features/...`. Core keeps the interfaces and gains
   `NullSupplyRouteVisualService`, `NullCampVisualService`, `NullRefugeVisualService`, registered by the feature
   IoC files; `Client/TaomClientComposition.cs` registers the real ones.
2. `ServerJitGateTests`, `ServerJitScan`, `ClientReferenceScan` (extracted from `DependenciesClientIlTests`),
   `ServerJitFixtures` and `ServerJitBaseline.txt`: an IL-token ratchet over every `TAOM.dll` method. A method
   whose IL names a client-only assembly must be exempt (`ClientOnlyAttribute` by name, declared in the scanned
   assembly) or listed in the baseline, which may only shrink.
3. Test, doc and rule updates; `tools/tests/test_prefab_asset_packages.py` scans `Main` and `Client`.

## Questions to answer with evidence

1. **DI timing.** Can any consumer capture the core null before `ClientComposition.Apply` replaces it, on a
   rendering client? Trace every resolve of `ICampService`, `IRefugeService`, `IRefugeBook`,
   `IRefugeDefenseService`, the three visual interfaces and the campaign behaviors that take them
   (`Main/SubModule.cs` `RegisterCampaignLifeBehaviors`). Include static caches, lazy delegates and
   `FieldCampMapView` (TAOM.Client).
2. **Null semantics.** `NullCampVisualService.Show` returns false. `CampState.VisualShown` is saved. Can a
   save written on the server or by a client without TAOM.Client break a later client load, or can a false
   `Show` cause per-frame work, log spam or a retry storm? Same for refuge (`_visualShown`) and the supply route
   `Update` per frame.
3. **Behaviour preserved on the client.** Does anything differ for a player: singleton lifetime, constructor
   dependencies DryIoc now resolves for the client types, `internal` members reached across assemblies
   (`SupplyCaravanService.ComputeNavPathPoints`, `CampLayoutBuilder`, `CampLayoutMath`), `InternalsVisibleTo`?
4. **The gate.** Can `ServerJitGateTests` pass while a core method names a client assembly? Look for: skipped
   methods (binding flags, generic types, property accessors, events, `.cctor`, nested and compiler-generated
   types, async and iterator state machines, local functions), tokens it cannot resolve treated as clean,
   `ClientOnlyAttribute` matching too broadly, key folding that merges an offender into an exempt key, a
   baseline compare that lets a new key hide behind a stale one, and the scanned-count floor. Build a fixture
   or mutation that escapes it if you can.
5. **Extraction parity.** Is `ClientReferenceScan` a pure move of `DependenciesClientIlTests`' walker, so that
   the Dependencies gate is no weaker?
6. **Leftovers.** Any reference to the moved types left in core (code, `using`, XML, docs that claim a path),
   any scanner or gate that reads only `TAOM.dll` and now misses the moved code, any test that passes for the
   wrong reason (order dependence on `GameAssemblies.EnsureLoaded`, vacuous `Validate` instead of `Resolve`).

## Output

Findings as CRITICAL, HIGH, MEDIUM, LOW, each with file:line, the failure scenario and the proving evidence
(a command you ran or code you read). Mark anything you could not verify as UNVERIFIED. If you find nothing,
say PASS and list what you checked.
