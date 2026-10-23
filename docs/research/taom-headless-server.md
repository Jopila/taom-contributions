# Running TAOM's code on the co-op dedicated server

**Status:** research and plan, 2026-10-21. B1 (the load gates) and B2 (TAOM.Dependencies headless)
are built (`5fcf8390c`); B3 (the TAOM.Client skeleton) is committed as `03d9db8dc`, with its follow-up in `e0a6970dd`. B4a (the 14 front-end types to TAOM.Client, `KnownClientTypesInCore` from 40 to 26) has landed; its client boot is owed. B4b (the six campaign-map types and three helpers, `KnownClientTypesInCore` from 26 to 20) has landed as `a15563a3c`. B4c (the 7 mission, custom-battle engine and WinForms types, 20 to 13) has landed as `c91380274`. B5 (the 13 UIExtenderEx types, the 15 prefab classes, the three Patch109 postfixes and the UIExtenderEx registration to TAOM.Client, 13 to 0) is built: `KnownClientTypesInCore` is empty, so milestone M1 is reached in source. The client boots of B4b, B4c and B5 and the M1 server boot are owed. B6c (the startup-hook guards: problem rows 3 to 6, 8, 9 and 15 to 17) has landed as `8960a0c24`; B6a and B6b are in progress, and the 6c server boot waits for them. The decision this plan asks for is
[ADR-013](../adrs/013-server-safe-core-client-ui-submodule.md), accepted 2026-10-22: option (b), a TAOM
server settings file, missions proven absent before B4c is sized, and the GetTypes shim kept.
**Target:** Bannerlord v1.5.5 server build (`Win64_Shipping_Server`) on the appliance's bundled .NET 6.0.36 host (the staged `dotnet/shared/Microsoft.NETCore.App`).
**Provenance:** the type, patch-class and method-body counts come from a System.Reflection.Metadata scan of
the staged `TAOM.dll` and `TAOM.Dependencies.dll` on 2026-10-21, compared with the engine log of the failing
boot (`rgl_log_46908.txt`). The scan outputs were session scratch and are not kept; the tables below are the
copy. Engine behavior (`Module.LoadSubModules`, `AssemblyLoader`, the tag checks) is from a decompile of the
server build, not re-read for this document. Every TAOM `file:line` below was read in the repo on
2026-10-21. Anything not confirmed is marked UNVERIFIED with the step that settles it.

## The problem

The co-op dedicated server (the BannerlordCoop appliance) loads TAOM's submodules by name. The engine then
does two things for each one, for every module, before any `OnSubModuleLoad` runs: `Assembly.GetTypes()` on
the submodule DLL, then the submodule's constructor. If either throws, the engine reports
`<submodule> could not be loaded correctly` and ends the process with `MBDebug.AbortGame`, exit code 5 and
no further message.

As of a4b8f7e21 three TAOM submodules failed this. B2 fixed the last two rows; `TAOM` still fails:

| Submodule | Failing step | Cause |
|---|---|---|
| `TAOM` (`TAOM.dll`) | `GetTypes` | 40 types derive from, or implement, a type in an assembly the server cannot resolve when TAOM loads (the table below). |
| `TAOM.Dependencies` | constructor | Fixed by B2. The static constructor called `Assembly.Load("TaleWorlds.Engine.GauntletUI")` and wrote `UIConfig` (`Dependencies/SubModule.cs:21-30` as of a4b8f7e21; the work now sits in the client-only helper `ConfigureGauntletPrefabs`, `:34-43`, behind the server check at `:28-31`); a static-constructor throw surfaces as a constructor failure. No `Error while getting types` line was logged for it (run 61152), which is what points at the constructor path. |
| The two untagged UI submodules in the Dependencies manifest | load | Fixed by B2. `UIExtenderEx` (only a `DumpXML` tag) and `Bannerlord Module Loader` (loader tags only) carried neither server tag as of a4b8f7e21; both now carry `DedicatedServerType=none` and `IsNoRenderModeElement=false` (`Dependencies/_Module/SubModule.xml:150-160` and `:228-239`), as ButterLib and MCM already did. |

**How it was found.** On 2026-10-21, by bisection on a v1.5.5 server: stage TAOM modules one submodule at a
time, boot, read the newest `rgl_log_*.txt`. Run 46908 (TAOM staged) logged 4,715 `Loaded Types:` lines for
`TAOM.dll`, whose metadata has 4,756 type definitions. Removing `<Module>` and the 4,715 loaded leaves 40,
and each of the 40 has a client-only base type or interface.

**Why the staging hides the cause.** The appliance's `Copy-SubModuleXml` strips `DedicatedServerType` tags
from a mod's manifest and keeps only the submodules named in `-KeepSubModules`. TAOM's core submodule carries
`DedicatedServerType=none` and `IsNoRenderModeElement=false` (`Main/_Module/SubModule.xml:75-78`); once
staged without them, the engine tries to load it. The strip exists in two places: `Copy-SubModuleXml` in the
server repo's `windows/run-windows.ps1` and its twin in `docker/prepare-and-run.ps1`.

**Tags mean different things in the two builds.** The server build loads an `IsNoRenderModeElement` submodule
only when the value is `"true"`; the v1.5.5 client build loads it only when the value is `"false"`. The
co-op server starts with `/dedicatedcustomserver`, which sets `DedicatedServerType.Custom`, so a submodule
tagged `DedicatedServerType=none` is rejected. Vanilla marks its own UI submodules with both tags (Native's
`ViewSubModule` and `GauntletUISubModule`, `Modules/Native/SubModule.xml`). The engine already has a
client-only submodule convention, and the plan uses it.

**The engine builds every submodule before any `OnSubModuleLoad`.** A second TAOM submodule's constructor
runs before the first one's `OnSubModuleLoad`. Two consequences: the existing `CollectAssemblyTypesShim`
(installed from `TAOM.Dependencies` `OnSubModuleLoad`, `Dependencies/SubModule.cs:245-246`, `:229` as of a4b8f7e21) runs after every
`GetTypes` and cannot help as wired; and a hand-off from a client submodule to core can ride on the client
constructor ("The client hand-off" below). Since B2 that shim is live on the server, because
`TAOM.Dependencies` now boots there and installs it; whether the server should keep it is still open (the
`CollectAssemblyTypesShim.Install` row of the keep table below).

## What makes a type fail on .NET 6

**The base type and the interfaces, including generic arguments of the base, and three layout shapes besides:
a declared field of a client-only VALUE type (an enum, a struct, `Nullable<T>`, `KeyValuePair<Client,int>`), a
generic parameter constraint on the type or on a declared method, and a declared override of a virtual from
another assembly whose return or parameter names a client-only type.** Reference-typed fields, `List<Client>`
fields and arrays do not fail. The B2 review reported the extra shapes from an experiment on .NET Framework 4.8
and CoreCLR 10; the server's .NET 6 is UNVERIFIED for them. All 40 failures have a client-only base or
interface; for the view-model mixins it is the generic argument of
`BaseViewModelMixin<T>` (for example `TimeAccelerationMixin : BaseViewModelMixin<MapTimeControlVM>`,
`Client/Features/TimeAcceleration/UI/TimeAccelerationMixin.cs:18`). Twenty-six other types whose only
client-only references were instance or static fields (17 field-only, 7 static-only, 2 both) loaded fine
in run 46908, and 190 types with client-only references in method bodies or signatures loaded fine. Shapes 3 and 4 do occur
inside the 40, next to a client-only base: `TaomSettlementPlateWidget.SetReference<T> where T : Widget`
(`Client/Features/SettlementNameplateRelation/TaomSettlementPlateWidget.cs:102`) and the `OnRender(TwoDimensionContext, ...)`
overrides of `PolygonWidget` (`Client/Features/FactionMap/Widgets/PolygonWidget.cs:383`) and `BannerWidget`
(`Client/Features/FactionMap/Widgets/BannerWidget.cs:208`). No type outside the 40 has any of the extra shapes:
gate 1, widened to cover them on 2026-10-22, still reports exactly the same 40 types.

A type deriving from `Widget`, `MapView` or a view model cannot be rewritten to load headless. It can only
live in an assembly the server never asks the engine to scan.

**The view layer is on the server, but not resolvable when TAOM loads.** The Coop module's server bin carries
SandBox.View, SandBox.GauntletUI, SandBox.ViewModelCollection, TaleWorlds.GauntletUI (and `.Data`,
`.PrefabSystem`, `.ExtraWidgets`), TaleWorlds.Engine.GauntletUI, TaleWorlds.MountAndBlade.View (and
`.GauntletUI`, `.GauntletUI.Widgets`, `.ViewModelCollection`), TaleWorlds.CampaignSystem.ViewModelCollection,
TaleWorlds.Core.ViewModelCollection and TaleWorlds.TwoDimension, staged on purpose by the server repo. TAOM
loads before Coop, and the engine's assembly resolver only returns an already-loaded assembly with a matching
full name. So at TAOM's boot these resolve to nothing. After Coop's `PatchAll` has loaded them a later TAOM
reference may resolve to Coop's copy. Any TAOM UI path that "works" at campaign time on the server may be
working by accident of Coop's load order. Confirmed for game init on 2026-10-23: in the spike boot
(`E:\CoopDS\probe-boot-C2.out` and its TAOM log under `E:\CoopDS\stage-spike\engine\bin\Win64_Shipping_Server\Logs\`),
the `OnSubModuleLoad` calls naming `SandBox.GauntletUI` and `TaleWorlds.MountAndBlade.GauntletUI` failed with
`FileNotFoundException`, yet at game init `ManualPatchApplicator.ApplyAll` patched `SandBox.View` targets (the
`[HarmonyCensus]` lists `contested SandBox.View.Map.MapConversationTableau.SpawnOpponentLeader`). Since B6c a server
logs `[Server] client-only assemblies loaded at game init: ...; not loaded: ...` once per boot
(`ServerAssemblyReport`); "not loaded" means not loaded yet, not unavailable. Do not rely on it.

**Two assemblies are absent even from Coop's bin:** `TaleWorlds.MountAndBlade.CustomBattle` (no CustomBattle
module on the server) and `System.Windows.Forms` (the runtime config names only `Microsoft.NETCore.App` and
`Microsoft.AspNetCore.App`). Five of the 40 types depend on them.

**MCMv5 and UIExtenderEx are load-time dependencies of `TAOM.dll`.** Four settings classes derive from MCMv5
types (`TaomSettings`, `CrashReportSettings`, `BattleLoadDiagnosticsSettings`, `BlowDiagnosticsSettings`);
26 types derive from UIExtenderEx base types, 11 of them among the 40. `MCMv5.dll` and the Harmony build TAOM
binds (0Harmony 2.4.2.0) sit in the staged app bin, which is why those types loaded in run 46908. If a
future staging stops copying non-kept DLLs, the four settings types would fail `GetTypes`. No gate checks
that: gate 2 covers only the TaleWorlds and SandBox references, so MCMv5 staging is a manual check.

## The 40 types

"Dropped" prefix: every feature type below is `TAOM.Features.<row>`. Defining lines were read in the repo on
2026-10-21. The client-only assembly is the one that declares the failing base or interface.

| Type | Defined at | Client-only base or interface (assembly) |
|---|---|---|
| `WarOfTheRingMomentum.UI.MomentumIndicatorMapView` | `Client/Features/WarOfTheRingMomentum/UI/MomentumIndicatorMapView.cs:17` | base MapView (SandBox.View) |
| `TimeAcceleration.UI.TimeAccelerationMixin` | `Client/Features/TimeAcceleration/UI/TimeAccelerationMixin.cs:18` | base arg MapTimeControlVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `TelcharsMark.UI.UpgradeRowMixin` | `Client/Features/TelcharsMark/UI/UpgradeRowMixin.cs:16` | base arg SPItemVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `TelcharsMark.UI.UpgradeScreenMixin` | `Client/Features/TelcharsMark/UI/UpgradeScreenMixin.cs:23` | base arg SPInventoryVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `SpecialResources.UI.EncyclopediaUnitBadgeMixin` | `Client/Features/SpecialResources/UI/EncyclopediaUnitBadgeMixin.cs:29` | base arg EncyclopediaUnitVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `SpecialResources.UI.SpecialResourceMapBarMixin` | `Client/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs:15` | base arg MapInfoVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `SpecialResources.UI.SpecialResourceSpriteWidget` | `Client/Features/SpecialResources/UI/SpecialResourceSpriteWidget.cs:11` | base IconBrushWidget (TaleWorlds.MountAndBlade.GauntletUI.Widgets) |
| `ShaderPrecompilation.TaomShaderGameManager` | `Client/Features/ShaderPrecompilation/TaomShaderGameManager.cs:28` | base CustomGameManager (TaleWorlds.MountAndBlade.CustomBattle) |
| `ShaderCompileNotice.ShaderCompileNoticeWindow` | `Client/Features/ShaderCompileNotice/ShaderCompileNoticeWindow.cs:13` | base Form (System.Windows.Forms) |
| `ShaderCompileNotice.WindowHandleOwner` | `Client/Features/ShaderCompileNotice/ShaderCompileNoticeWindow.cs:85` | interface IWin32Window (System.Windows.Forms) |
| `ShaderCompileNotice.ShaderCompileNoticeContext` (was `ShaderCompileNotice+NoticeContext`) | `Client/Features/ShaderCompileNotice/ShaderCompileNoticeContext.cs:16` | base ApplicationContext (System.Windows.Forms) |
| `SettlementNameplateRelation.TaomSettlementPlateWidget` | `Client/Features/SettlementNameplateRelation/TaomSettlementPlateWidget.cs:19` | base Widget (TaleWorlds.GauntletUI) |
| `ReevesLedger.UI.ReserveStepMixin` | `Client/Features/ReevesLedger/UI/ReserveStepMixin.cs:25` | base arg TownManagementReserveControlVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `ReevesLedger.UI.TownManagementLedgerMixin` | `Client/Features/ReevesLedger/UI/TownManagementLedgerMixin.cs:24` | base arg TownManagementVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `RealmBorders.UI.RealmBordersMapView` | `Client/Features/RealmBorders/UI/RealmBordersMapView.cs:22` | base MapView (SandBox.View) |
| `Namegiving.UI.FiefNamingMixin` | `Client/Features/Namegiving/UI/FiefNamingMixin.cs:24` | base arg KingdomManagementVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `Messengers.UI.MessengerEncyclopediaMixin` | `Client/Features/Messengers/UI/MessengerEncyclopediaMixin.cs:12` | base arg EncyclopediaHeroPageVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `HostReview.Hooks.HostReviewVisualOrderProvider` | `Client/Features/HostReview/Hooks/HostReviewVisualOrderProvider.cs:11` | base DefaultVisualOrderProvider (TaleWorlds.MountAndBlade.View) |
| `HallOfFire.TaomMapConversationDataProvider` | `Client/Features/HallOfFire/TaomMapConversationDataProvider.cs:15` | interface IMapConversationDataProvider (SandBox.View) |
| `FieldCamp.UI.FieldCampMapView` | `Client/Features/FieldCamp/UI/FieldCampMapView.cs:21` | base MapView (SandBox.View) |
| `FiefManagement.UI.TaomFiefsNavigationElement` | `Client/Features/FiefManagement/UI/TaomFiefsNavigationElement.cs:21` | base MapNavigationElementBase (SandBox.View) |
| `FactionUI.UI.Widgets.FilteredPropertyRow` | `Client/Features/FactionUI/UI/Widgets/FilteredPropertyRow.cs:18` | base Widget (TaleWorlds.GauntletUI) |
| `FactionUI.UI.Widgets.FrontEndRichTextWidget` | `Client/Features/FactionUI/UI/Widgets/FrontEndRichTextWidget.cs:9` | base RichTextWidget (TaleWorlds.GauntletUI) |
| `FactionUI.UI.Widgets.FrontEndTextWidget` | `Client/Features/FactionUI/UI/Widgets/FrontEndTextWidget.cs:14` | base TextWidget (TaleWorlds.GauntletUI) |
| `FactionUI.UI.Widgets.NarrativeRandomButton` | `Client/Features/FactionUI/UI/Widgets/NarrativeRandomButton.cs:16` | base ButtonWidget (TaleWorlds.GauntletUI) |
| `FactionUI.UI.Widgets.ScrollShortcutButton` | `Client/Features/FactionUI/UI/Widgets/ScrollShortcutButton.cs:10` | base ButtonWidget (TaleWorlds.GauntletUI) |
| `FactionUI.UI.Widgets.StageJumpButton` | `Client/Features/FactionUI/UI/Widgets/StageJumpButton.cs:12` | base ButtonWidget (TaleWorlds.GauntletUI) |
| `FactionUI.UI.Widgets.ThemeIconWidget` | `Client/Features/FactionUI/UI/Widgets/ThemeIconWidget.cs:10` | base Widget (TaleWorlds.GauntletUI) |
| `FactionMap.Widgets.BannerWidget` | `Client/Features/FactionMap/Widgets/BannerWidget.cs:14` | base ImageWidget (TaleWorlds.GauntletUI) |
| `FactionMap.Widgets.FactionImageWidget` | `Client/Features/FactionMap/Widgets/FactionImageWidget.cs:13` | base ImageWidget (TaleWorlds.GauntletUI) |
| `FactionMap.Widgets.MapContainerWidget` | `Client/Features/FactionMap/Widgets/MapContainerWidget.cs:7` | base Widget (TaleWorlds.GauntletUI) |
| `FactionMap.Widgets.PolygonWidget` | `Client/Features/FactionMap/Widgets/PolygonWidget.cs:23` | base ImageWidget (TaleWorlds.GauntletUI) |
| `FactionMap.Widgets.RuntimeSprite` | `Client/Features/FactionMap/Widgets/RuntimeSprite.cs:6` | base Sprite (TaleWorlds.TwoDimension) |
| `CustomBattles.TaomFactionSelectionVM` | `Client/Features/CustomBattles/TaomFactionSelectionVM.cs:7` | base CustomBattleFactionSelectionVM (TaleWorlds.MountAndBlade.CustomBattle) |
| `CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView` | `Client/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs:17` | base MissionView (TaleWorlds.MountAndBlade.View) |
| `CareerSystem.UI.CharacterDeveloperCareerMixin` | `Client/Features/CareerSystem/UI/CharacterDeveloperCareerMixin.cs:12` | base arg CharacterDeveloperVM (TaleWorlds.CampaignSystem.ViewModelCollection) |
| `CareerSystem.UI.MissionAgentStatusCareerMixin` | `Client/Features/CareerSystem/UI/MissionAgentStatusCareerMixin.cs:18` | base arg MissionAgentStatusVM (TaleWorlds.MountAndBlade.ViewModelCollection) |
| `CareerSystem.UI.TaomCareerDiamondWidget` | `Client/Features/CareerSystem/UI/TaomCareerDiamondWidget.cs:28` | base Widget (TaleWorlds.GauntletUI) |
| `AdvancedCombat.AutonomousMovementPlayerController` | `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:16` | base MissionView (TaleWorlds.MountAndBlade.View) |
| `TAOM.Adapters.FrontEndRuntimeSprite` (not under `Features`) | `Client/Adapters/FrontEndRuntimeSprite.cs:11` | base Sprite (TaleWorlds.TwoDimension) |

By group: front end 14 (7 `FactionUI.UI.Widgets`, 5 `FactionMap.Widgets`, `FrontEndRuntimeSprite`,
`TaomFactionSelectionVM`); campaign map 6, moved in B4b (three MapViews, `TaomFiefsNavigationElement`,
`TaomMapConversationDataProvider`, `TaomSettlementPlateWidget`); UIExtenderEx view-model mixins 11 plus
`SpecialResourceSpriteWidget` and `TaomCareerDiamondWidget` (13); mission, custom-battle engine and WinForms 7
(`AutonomousMovementPlayerController`, `BattleActionBarMissionView`, `HostReviewVisualOrderProvider`,
`TaomShaderGameManager`, `ShaderCompileNoticeWindow`, `WindowHandleOwner`, `NoticeContext`).

**Core methods that name one of the 40**, each moving with it or being inverted behind an adapter (3
methods, after B4a settled the six front-end ones, B4b the seven campaign-map ones and B4c the ten mission,
shader and Host Review ones): the three `Patch109_SPInventoryVM*` postfixes, which B5 settled by moving them to `Client/Features/TelcharsMark/Hooks/` (the patch-category pass now reads TAOM.dll and TAOM.Client). B4b settled
`MomentumUIService.AddMapMeter` and `RemoveMapMeter` (the class moved), `RealmBordersCampaignBehavior.EnsureMapView`
and `FieldCampCampaignBehavior.EnsureOverlay` (their bodies moved into client adapters), the
`Patch36_MapNavigationElements` postfix lambda (a factory), `HallOfFireInstaller.Install` (a wrapper seam) and
`NameplateRelationIoC.InitializeWidgetStatics` (a core context class). B4c settled `SubModule.AddTaomMissionBehaviors`
(two core factory seams), `WargMissionBehavior.Initialize` and `SwitchMainAgentController`,
`WargRiderHandManager.ClearCustomLookDirection` and `UpdateWargRiderHandle`, `FinishRageMode.Reset` and
`PrepareOnFirstCall.Execute` (all through `IAutonomousMovementController`), `ShaderPrecompileRunner.StartCurrentItem`
(a game-manager factory), `ShaderCompileNotice.Run` (a window host) and `HostReviewModule.OnPhase` (an order-provider
registrar).

## TAOM.Dependencies

No type in `TAOM.Dependencies.dll` fails `GetTypes` (76 type definitions, none failed). As of a4b8f7e21 exactly
two method bodies referenced a client-only assembly: the static constructor (`UIConfig`,
TaleWorlds.Engine.GauntletUI, `Dependencies/SubModule.cs:21-30`) and `OnSubModuleLoad` (`UIExtender`,
Bannerlord.UIExtenderEx, forced static init at `:195`). After B2 the only references sit in two NoInlining
helpers, `ConfigureGauntletPrefabs` (`:34-43`) and `ForceUiExtenderStaticInit` (`:287-289`), and
`DependenciesClientIlTests` pins that for every method of every type in the assembly.

Line numbers in the two tables below are in the B2 working tree (after a4b8f7e21).

**Keep on the server** (none of these reference a client-only assembly):

| Step | Where | Why headless needs it |
|---|---|---|
| `InstallAssemblyResolveHandler` | `Dependencies/SubModule.cs:136-155` | Same BUTR-name redirect as a client; harmless when nothing asks. |
| `ApplyHarmonyGuards` (blocks `UnpatchAll(null)`) | `Dependencies/SubModule.cs:336-341` | Coop, the server core and TAOM share one Harmony registry in the process. |
| `CheckForDuplicateHarmony` | `Dependencies/SubModule.cs:353-365` | The staging forces one net6 0Harmony; a second copy is worth one log line. |
| `CoopPresence.Refresh` | `Dependencies/SubModule.cs:242-243` | Feeds the shield policies below. |
| `SaveShield.Install` | `Dependencies/SubModule.cs:251-252` | Under co-op it rethrows save-load faults and keeps swallowing mission-init faults, the same contract as a co-op client ([coop-interop](../features/coop-interop.md), "PatchShield is skipped under co-op" and the SaveShield text near it). |
| `PatchShield.Install` | `Dependencies/SubModule.cs:248-249` | Already a no-op under co-op (`PatchShieldPolicy.ShouldInstall`). |
| `IncompatibleModDetector`, `VersionProbe`, the ProcessExit summary | `Dependencies/SubModule.cs:234-275` | Diagnostics; no UI. |
| `CollectAssemblyTypesShim.Install` | `Dependencies/SubModule.cs:245-246` | Runs after every `GetTypes`; its policy rethrows for Coop's assemblies. Keep for parity with a co-op client, or skip headless so a half-loaded assembly fails loudly. Open question for the maintainer; B2 made it live on the server, since `TAOM.Dependencies` now boots there and installs it. |

**Skip on the server:** the Gauntlet prefab setup in the static constructor (`:29-32`) and the forced
UIExtenderEx static constructor (`:201-220`). With the UIExtenderEx submodule absent headless there is nothing
for it to patch.

**The fix (batch B2):**

1. The server check is the engine's build constant `TaleWorlds.MountAndBlade.GameNetwork.IsDedicatedServer`, not a
   rule of TAOM's own. Its getter in the server DLL is `ldc.i4.1; ret` (Windows and Linux builds) and in the client
   DLL `ldc.i4.0; ret`; the engine reads it in `Module.Initialize` before `LoadSubModules`; the client type's
   initializer only sets one constant and the server type has none, so reading it runs nothing that can fail.
   `Dependencies/SubModule.cs` reads it in both guards, and `DedicatedServerProvider`
   (`IDedicatedServerProvider.IsDedicatedServer`, `Main/Features/CoopInterop/DedicatedServerProvider.cs:13`) reads it
   for the services and stays the test seam (`DedicatedServerProviderTests.Getter_CallsGameNetworkIsDedicatedServer`
   pins the read). On the server a false answer is fatal (the client UI setup runs and ends the process), which a
   build constant cannot give and the bin-folder rule that B2 first used could. That rule (`ServerHost`) was
   replaced after review. The B2 boot ran on the bin-folder build and will be repeated on this one.
2. Static constructor: keep `InstallAssemblyResolveHandler()`, then `if (!GameNetwork.IsDedicatedServer)
   ConfigureGauntletPrefabs();` with `ConfigureGauntletPrefabs` a separate `[MethodImpl(MethodImplOptions.NoInlining)]`
   method holding the `Assembly.Load` and the `UIConfig` write, so the `UIConfig` token is not in the
   constructor's own IL.
3. `OnSubModuleLoad`: the `RunClassConstructor(typeof(UIExtender).TypeHandle)` block moves into a NoInlining
   `ForceUiExtenderStaticInit()` called only off-server. `Bannerlord.UIExtenderEx.dll` itself resolves on the
   current staging (it is promoted into the server app bin); what does not resolve is the Gauntlet layer its
   static init patches, so running that init headless would throw (Likely, not run).
4. Manifest: tag `UIExtenderEx` and `Bannerlord Module Loader` with `DedicatedServerType=none` and
   `IsNoRenderModeElement=false`, as ButterLib and MCM already are. `TAOM.Dependencies` stays untagged.

## Startup and campaign-time problem points

**The rule behind most rows.** On CoreCLR a method whose IL names a type or member in an unresolvable
assembly fails when that method is JIT-compiled, before its first line runs, so an `if` inside the same method
does not protect it. Confirmed on the server's .NET 6.0.36 by the 2026-10-23 spike boot (above): a caller whose
own IL names only TAOM types compiles, and the callee that names the missing assembly fails when it is first
called (`[DevConsole] Patch79 tooltip probes failed to apply: Could not load file or assembly
'TaleWorlds.MountAndBlade.GauntletUI'`, caught by the existing try). That the failure comes before the callee's
first line is still [Likely]. The guard pattern is the server check
in the caller with the UI work in a separate `[MethodImpl(MethodImplOptions.NoInlining)]` method, or an
adapter interface with a server null implementation. Whether a reference fails also depends on whether Coop
has already loaded the assembly, so every row is guarded regardless.

| # | Where | Headless behavior | Guard |
|---|---|---|---|
| 1 | `Main/SubModule.cs:242-244` (`UIExtender.Create`, `RegisterUiExtensions`, `Enable`); `RegisterUiExtensions` `:963-1033` | Unguarded. UIExtenderEx's static constructor patches Gauntlet types; a throw ends `OnSubModuleLoad` and the game does not start (comment `:206-212`). | Done in B5: `ClientSubModule.OnSubModuleLoad` registers once through `ClientUiExtensions.Register` (UIExtenderEx refuses a second Register under one module name, so core registers nothing). Client boot owed. |
| 2 | `Main/SubModule.cs:218-223`; `Main/PatchCategoryIndex.cs:31-57`, `:65-70` | 98 of 317 patch classes reference a client-only assembly: by attribute target (read inside `Build`'s try: skipped, then reported as a failure) or only by patch-method signature (fails in `Apply`, which aborts the rest of that category by design). 11 categories mix UI and core classes: Patch101_MapFrameProfiler, Patch105_HiddenHosts, Patch108_TaomBuildings, Patch17_TroopWeight, Patch19_CustomBattles, Patch23_BannerColorPersistence, Patch26_SpecialResources, Patch29_CCBodyProperties, Patch75_Refuge, Patch94_CreatureBroodCampaign, Patch95_FactionUI. A core class after a signature-only UI class is silently not applied headless. | A core `[ClientOnly]` marker; `PatchCategoryIndex.Build` drops marked classes headless; split the 11 mixed categories; log skipped client-only classes once instead of reporting them as failures (B6a). |
| 3 | `Main/SubModule.cs:320` FactionUI init (helper `InitializeFactionUi`); `_factionUiTicker`, ticked `:2277` | Resolves, then `OnApplicationTick` drives front-end services each frame; the call graph reaches `FrontEndStateAdapter`, `FrontEndResourceAdapter` and `FactionScreenWidgets` from that tick. | Done in B6c (`8960a0c24`): skipped headless, so Patch95 is not applied there (its three core-target classes only theme menu music and video) and the ticker stays null; the tick reads it with `?.`. |
| 4 | `Main/SubModule.cs:259`, `:2276` time acceleration | `TimeAccelerationService.OnTick` reads `_input.IsMapActive` while a campaign runs (`Main/Features/TimeAcceleration/TimeAccelerationService.cs:56`); that is `MapScreen.Instance` (SandBox.View, `Main/Features/TimeAcceleration/MapInputAdapter.cs:19`), a JIT failure out of every application tick. | Done in B6c: the service is not resolved headless; the tick reads the null field with `?.`. |
| 5 | `Main/SubModule.cs:263` hotkey categories (helper `RegisterHotKeyCategories`: the time controls and the career ability key) | InputSystem is on the server; the effect of registering headless was never tested. | Done in B6c: skipped headless. Both consumers (`MapInputAdapter`, `AbilityInputAdapter`) scan `HotKeyManager.GetAllCategories()` and treat a missing key as unbound. |
| 6 | Patch62 (`SubModule.cs` OnSubModuleLoad); `Main/SubModule.cs:481` Patch79 (helper `ApplyTooltipProbes`) | Targets in Engine.GauntletUI, CampaignSystem.ViewModelCollection (`MapInfoItemVM`) and MountAndBlade.GauntletUI. In the spike boot the Patch79 probe's `Initialize` failed with `FileNotFoundException` inside its try. | Patch62: `[ClientOnly]` (B6a). Patch79: done in B6c, skipped headless. |
| 7 | `Main/SubModule.cs:666` `FeatureModuleHooks.RunPhase(ApplyPhase.ProcessLoad, ...)` | Reaches client code in `NameplateCullService.Install` and `MapViewReleaseLayerEvents`. `HallOfFireInstaller.Install` stays on the list: it is runtime-guarded (it returns on a dedicated server before it touches the wrapper) but runs at `ApplyPhase.MainMenu`, not ProcessLoad, and whether it JITs without SandBox.View is UNVERIFIED. `MapConversationProviderSlot` stays in core and is a B7 item. `ModuleRunner.Run` faults the module and skips it in every later step (`Main/Composition/ModuleRunner.cs:62-90`), so its campaign behaviors are lost too; a save-owning module fails closed at campaign start. | `TaomFeatureModule.ClientOnly` (whole module) or a client flag on `PatchCategoryDecl`; `ModuleRunner` skips client work headless without faulting (B6b). |
| 8 | `Main/SubModule.cs:628` `OnBeforeInitialModuleScreenSetAsRoot` | The server engine calls every submodule's hook from `Module.OnApplicationTick` while no game state is active, then returns before it pushes a state (server `Module` decompile, 2026-10-23). Under the Coop host it never fired: zero calls in three logged boots, the spike boot included, so no `phase=MainMenu` line is expected there. Menu customization `:637`, Patch55 `:649`, startup inquiry after it. | No B6c hunk: `CustomizeMenu` already logs one warning per option headless and `ShowInquiry` only raises an event. Patch55 gets `[ClientOnly]` (B6a). |
| 9 | `Main/SubModule.cs:1552` Patch101 installer (`MapFrameProfilerInstaller.OnGameInitialized`); `:1672` Patch80 bindings (helper `ApplyKingdomVoteDeadlockFix`); `:1985` `ManualPatchApplicator.ApplyAll` | `KingdomVoteDeadlockBinding.Initialize` names CampaignSystem.ViewModelCollection types; `ApplyAll` named `OrderOfBattleHeroItemVM`. The plan predicted one JIT failure would skip the rest of `OnGameInitializationFinished`, but the spike boot ran all three at game init without a failure, because Coop had loaded the view assemblies by then (see "The view layer is on the server"). | Done in B6c, as UI skips rather than crash fixes: the installer behind an inline server check; Patch80 (all five classes target view models) in a NoInlining helper; `ApplyAll` keeps the settlement guards and moves the captain tooltip and the four visual patches into two NoInlining helpers. |
| 10 | Campaign behaviors wired in `OnGameStart`: B4b settled three: the realm borders tick (`RealmBordersCampaignBehavior.OnTick`), the field camp tick (`Main/Features/FieldCamp/Hooks/FieldCampCampaignBehavior.cs:139`) and the War of the Ring meter (`Main/Features/WarOfTheRingMomentum/WarOfTheRingMomentumBehavior.cs:215-217`) now call null adapters or a null service headless; `Main/Features/SupplyLines/Hooks/SupplyLinesCampaignBehavior.cs:144` to `SupplyRouteVisualService.Update`; `RefugeService.TryShowVisual` to `RefugeVisualService.Show`; `CampService.TryShowVisual` to `CampVisualService.Show`; `HiddenHostsPlainSightBehavior.Stamp` and `FarWatchService` to `PartyIntelAdapter` (`SandBoxUIHelper`). | These call UI services and adapters directly during the campaign. | Visual services and adapters get a server null implementation, replaced by the client composition (B7). |
| 11 | `Main/SubModule.cs:2178`, `:2245` `AddTaomMissionBehaviors` | Named two of the 40. Whether the dedicated server ever runs a mission is UNVERIFIED. | B4c first used two core factory seams at the same add sites (`IAutonomousMovementControllerFactory`, `IBattleActionBarViewFactory`); #970 replaced both with `[DefaultView]` on the two TAOM.Client views, because a view added there is never registered with the MissionScreen and never ticks. A `ClientSubModule.OnMissionBehaviorInitialize` move was rejected: that hook runs after core's whole `OnMissionBehaviorInitialize` (`Mission.AfterStart`, `Mission.cs:3808-3825`), so the views would sit behind the loading-window closer, outside the co-op add filter and outside the `[BattleLoad]` bracket. ADR-013 decision 3 guard: on a dedicated server `AddTaomMissionBehaviors` logs one error and adds no TAOM behavior. |
| 12 | `Main/SubModule.cs:120` `IoC.Configure` | No direct client reference. `InitializeStatics` is fail-closed for save owners (`Main/Composition/ModuleRunner.cs:38-39`); HiddenHosts owns save data (`Main/Features/HiddenHosts/HiddenHostsModule.cs:41`) but its statics only capture a method group (`Main/Features/HiddenHosts/HiddenHostsCalls.cs:33,46`), which does not JIT it. Likely safe. | None now; the JIT ratchet gate watches it. |
| 13 | `Main/SubModule.cs:130-133` shader-compile notice | Already server-aware (`Main/Features/ShaderCompileNotice/ShaderCompileNoticeEntry.cs:17,31`). Only its WinForms types had to move. | B4c: the window, its owner and the message-loop context moved. The static `ShaderCompileNotice` class stays in TAOM.dll (VanillaTuning may look it up by full type name) and runs the TAOM.Client window through `IShaderCompileNoticeWindowHost`. |
| 14 | `Main/SubModule.cs:672` and the inquiry paths | `InformationManager` is TaleWorlds.Library; nothing renders headless. Likely harmless. | None. |
| 15 | `Main/SubModule.cs:521` `CustomBattlesIoC.InitializeHooks` (helper `InitializeCustomBattleHooks`) | `CommanderSelectorRebuilder.Initialize` and `CustomBattleSideVM_Constructor_Patch.Initialize` name `TaleWorlds.MountAndBlade.CustomBattle`, which no server tree has. Boot-fatal: in the spike boot the wrapped call failed with `FileNotFoundException`, and unwrapped the server's `Module.InitializeSubModuleBases` rethrows (`OnSubModuleLoad failed for TAOM`). | Done in B6c: skipped headless. The two core `BannerlordMissions_*` classes of Patch19 still apply and read their null logger with `?.`. |
| 16 | `Main/SubModule.cs:593` `BannerEditorView_OnTick_Patch.Initialize` (helper `InitializeBannerEditorHook`) | Its body names `SandBox.GauntletUI`. Boot-fatal: failed with `FileNotFoundException` in the spike boot. Missed by the original research. | Done in B6c: skipped headless. |
| 17 | `Main/SubModule.cs:1964` Patch60 (helper `ApplyTournamentExitMovieRelease`) | `Patch60_TournamentExitMovieRelease`'s type initializer names `SandBox.GauntletUI`. Missed by the original research; at game init it ran without a failure in the spike boot. | Done in B6c: skipped headless, a UI skip. |

The call-graph scan from the `SubModule` hooks, `IoC`, every `CampaignBehaviorBase` and `GameModel` subclass
and every core patch class found 97 methods that can reach a client-only method. They are candidates, not
proofs: virtual dispatch was matched by name, so it over-approximates. Each B7 slice reads its candidates
before guarding them. A separate IL scan found 739 `TAOM.dll` methods, in 190 types, whose body references a
client-only assembly or one of the 40.

## Settings headless

- **What a server reads today.** From an MCMv5 decompile: `GlobalSettings<T>.Instance` returns
  `BaseSettingsProvider.Instance?.GetSettings(id) as T`, and `BaseSettingsProvider.Instance` has an
  `internal` setter. Only MCM's own submodules set it and the server loads none of them (they are tagged
  `none`), so `TaomSettings.Instance` is null headless and does not throw (Likely; the first clean boot
  confirms it with one log line).
- **Gameplay reads MCM constantly.** 227 `TaomSettings.Instance` occurrences in 112 files under `Main` (grep
  this turn; the three diagnostics settings classes add more), nearly all through settings providers with
  `private TaomSettings? Settings => _settings ??= TaomSettings.Instance;` (43 files; for example
  `Main/Features/CaravanTrade/CaravanTradeSettingsProvider.cs:21`) and compiled or ModuleData fallbacks (for
  example `Main/Features/AiPartySize/AiPartySizeService.cs:106-108`, which defaults with
  `settings?.EnableAiPartyScaling ?? true`). A grep for `TaomSettings.Instance.` member access without a null
  guard found no code use outside comments. So a server runs on **compiled defaults**. While the instance is
  null, `??=` re-reads MCM's dictionary on every access: cheap, no throw.
- **What that means.** The server's gameplay settings are the defaults whatever the host player chose.
  `SettingsFingerprint` and `SettingsFingerprintLog` already hash the simulation-relevant groups; peers do not
  exchange them ([coop-interop](../features/coop-interop.md), "What is NOT done", the MCM settings parity
  bullet).
- **Decision for the maintainer (B8):** (1) compiled defaults, logged once at boot with the fingerprint (cheap,
  recommended first); or (2) a TAOM-owned settings file the server reads into a `TaomSettings` instance behind
  one `ITaomSettingsSource` seam that every provider's accessor uses (43 files use the `??= TaomSettings.Instance`
  accessor and 81 files declare a `*SettingsProvider` class; the other `TaomSettings.Instance` readers too).
  Where MCM writes TAOM's json2 file on a server user profile, and whether `TaomSettings` can be deserialized
  outside MCM, are UNVERIFIED.

## Options considered

(b) is the smallest form of (a): a type deriving from `Widget`, `MapView` or a view model can only live in a
second assembly, so "move the 40 types" has one possible destination.

| Option | Win | Cost | Verdict |
|---|---|---|---|
| **(a) Full split**: every UI type and UI patch into `TAOM.Client.dll`, a submodule tagged as vanilla tags its UI submodules | The compiler proves core never touches UI, and the server loads nothing it cannot use. | About 190 types with UI bodies, 98 UI patch classes and every core-to-UI call (97 reachable candidates) move or get inverted, in one effort. | Keep as the direction, reject as one change: the cost lands before any boot proves the approach. |
| **(b) Minimal client assembly** (recommended): `TAOM.Client.dll` holds only what cannot load headless, the 40 types plus the 15 UIExtenderEx prefab classes, and the callers that exist only to instantiate them | `GetTypes` passes on the server, and a gate keeps it passing. | One new project and submodule, about 55 type moves, 26 core methods inverted, build-script changes. | **Keep**: the win (TAOM boots headless at all) dominates the cost. |
| **(c) GetTypes shim**: install `CollectAssemblyTypesShim` early enough | Smallest code change: TAOM loads its 4,715 good types. | Swallows `ReflectionTypeLoadException` for every non-Coop assembly in the server process, leaves 40 TAOM types absent with a latent `TypeLoadException` wherever code reaches them, and does not fix the Dependencies static constructor. | **Reject as the fix.** Acceptable only as an uncommitted local spike to see the next failure early. |
| **(d) `TAOM.Server` submodule** | A server entry point with no UI in it. | A third entry point that must repeat TAOM's gameplay wiring (behaviors, models, patches, save definers) and drift from it, or that still needs the core/UI separation of (b) underneath. | **Reject**: more complexity, no gain over (b). |
| **(e) Make the view layer resolvable early** (load Coop's view assemblies before TAOM) | `GetTypes` would fail for only 5 types (CustomBattle, WinForms). | TAOM would depend on Coop's private bin layout and load order, and all TAOM UI code would run half-alive headless. | **Reject.** |

**Why (c) is mechanically viable but still rejected.** In both CoreLibs checked (6.0.36 on the desktop and 6.0.12 on the
server's bundled host as read on 2026-10-21; the appliance stage bundles 6.0.36), `Assembly.GetTypes` is `public virtual` and `RuntimeAssembly` does not override it
(only `GetExportedTypes`), so a Harmony finalizer on it would intercept the engine's call. It would have to be
installed from the `TAOM.Dependencies` submodule constructor, because of the load order above. UNVERIFIED: that
net6 0Harmony patches a CoreLib method there without the JIT inlining or devirtualizing the call site. The
rejection does not rest on that; it rests on a process-wide behavior change that hides type-load faults and
leaves the runtime surface broken.

**Recommendation: (b) now, with the gates below, then grow toward (a) feature by feature where it pays.**
Everything that loads but must not run headless stays in core behind the existing server signal
(`IDedicatedServerProvider`) or a null adapter.

**The client hand-off.** Core must not reference `TAOM.Client`. Core defines a small slot
(`TAOM.Composition.ClientComposition`, a static settable `IClientComposition` with one method,
`RegisterServices(IRegistrator)`); the `TAOM.Client.ClientSubModule` constructor sets it; `IoC.Configure` calls
it after core registrations with replace semantics. This works because the client submodule's constructor
runs before core's `OnSubModuleLoad` (every submodule is constructed before any `OnSubModuleLoad` fans out,
in both builds). On the server the slot stays empty and core's null adapters remain. Rejected alternative:
registering from the client's `OnSubModuleLoad`, because singletons resolved in core's `OnSubModuleLoad` (for
example `_timeAccelerationService`, `Main/SubModule.cs:255`) would already hold the null adapter.

## Staging

**Generic rule for the server repo (mod-agnostic):** for an extra module with no `-KeepSubModules` entry, stage
exactly the submodules the server engine itself would load and leave their tags as written. Concretely, in
`Copy-SubModuleXml` and its docker twin, drop a submodule whose tags include `DedicatedServerType=none` or
`IsNoRenderModeElement=false` (the two client-only markers), keep the rest with their tags. A module named in
`-KeepSubModules` keeps today's behavior (named submodules only, tags stripped), as an explicit override for
mods that tag wrongly. The module author's manifest becomes the contract.

**TAOM's side (B9, after the `GetTypes` milestone):**

- `Main/_Module/SubModule.xml:71-79`: the `TAOM` submodule loses both tags (`:76-77`; it is server-safe after
  B5). The second `TAOM.Client` submodule (`TAOM.Client.dll`, `TAOM.Client.ClientSubModule`, carrying
  `DedicatedServerType=none` and `IsNoRenderModeElement=false`, `:84-92`) already landed in B3. Removing
  `IsNoRenderModeElement=false` from core changes nothing on a client, because an absent tag is never evaluated.
- `Dependencies/_Module/SubModule.xml`: the two tags from B2.
- `Main/TAOM.csproj`, target `MirrorWin64ShippingClientToServer` (`:220-221`): the exclusion of
  `TAOM.Client.*` already landed in B3, so nothing headless carries it. B9 only keeps it and the staging rule
  honest.
- Expected staged submodules: `TAOM.Dependencies`, `TAOM`. Dropped: `UIExtenderEx`, `ButterLib`,
  `ButterLib Implementation Loader`, `MCMv5`, `MCMv5 Basic Implementation`, `Bannerlord Module Loader`,
  `TAOM.Client`.

**Order matters.** The server-repo rule must not ship before TAOM's manifest flip, or the generic rule would
drop today's `none`-tagged TAOM core. Rejected alternative: a separate server manifest shipped by TAOM (two
manifests drift; the engine's tags already say it).

## The gates

All in `TAOM.Tests/Infrastructure/`, plain reflection, no new package. In the test process every client
assembly resolves, so ordinary reflection sees base types, interfaces, fields and constraints, and
`MethodBody.GetILAsByteArray()` plus `Module.ResolveMember` read IL tokens (through `IlCallScanner`).

`ClientAssemblies` (`TAOM.Tests/Infrastructure/ClientAssemblies.cs`) is one shared static list of 16 names, with
a comment naming its source: the `TAOM.dll` references that do not resolve when TAOM loads headless.
SandBox.View, SandBox.GauntletUI, SandBox.ViewModelCollection, TaleWorlds.CampaignSystem.ViewModelCollection,
TaleWorlds.Core.ViewModelCollection, TaleWorlds.Engine.GauntletUI, TaleWorlds.GauntletUI,
TaleWorlds.GauntletUI.Data, TaleWorlds.GauntletUI.PrefabSystem, TaleWorlds.MountAndBlade.GauntletUI,
TaleWorlds.MountAndBlade.GauntletUI.Widgets, TaleWorlds.MountAndBlade.View,
TaleWorlds.MountAndBlade.ViewModelCollection, TaleWorlds.TwoDimension,
TaleWorlds.MountAndBlade.CustomBattle and System.Windows.Forms. `Bannerlord.UIExtenderEx` is not in the list: it resolves on the staged server
and gate 2 reads `Names` (`DependenciesClientIlTests` names it separately). There is no "must be staged" list; gate 2 checks the
TaleWorlds and SandBox references instead.

| Gate | What it checks | Lands in |
|---|---|---|
| `ServerLoadGateTests.EveryCoreType_LoadsWithoutClientAssemblies` | For each type in `TAOM.dll`, four shapes name a `ClientAssemblies` assembly: (1) the base chain and interfaces, generic arguments included recursively; (2) a declared field of a value type (enums, structs, `Nullable<T>`, `KeyValuePair<Client,int>`; reference-typed fields, `List<Client>` and arrays do not fail); (3) a generic parameter constraint of the type or of a declared method; (4) a declared method that overrides a virtual from another assembly and names a client type in its return or parameters. There is no allowlist (the empty `KnownClientTypesInCore` set was deleted after B5), so any core type that needs a client-only assembly fails the test. That is milestone M1, reached in source. The 15 UIExtenderEx prefab classes live in TAOM.Client, and `ClientIsolationGateTests.TaomCore_DeclaresNoUiExtension` pins that no core type carries a UIExtenderEx attribute. The detectors live in `ServerLoadScan`; `ServerLoadFixtureTests` holds one fixture per shape plus the negatives (System.dll as the stand-in client assembly), so a detector that stops firing fails there. `EveryDependenciesType_LoadsWithoutClientAssemblies` runs the same walk over `TAOM.Dependencies.dll` and expects an empty set. | B1 |
| `ServerLoadGateTests.ClientOnlyList_AgainstStagedServer_ListedAbsentAndOtherReferencesPresent` | Reads a staged server root from the `TAOM_DS_STAGE` environment variable (default `E:\repos\BannerlordCoop.DedicatedServer\.stage-win\engine`); `Assert.Inconclusive` when absent. It looks in the app bin (`bin/Win64_Shipping_Server`) plus the bins of the modules that load before TAOM in the stock order (Native, SandBoxCore, SandBox, TAOM.Dependencies, LOTRLOME_Armory, TAOM_Map). Every `ClientAssemblies` name must be absent there (the Coop module loads after TAOM, so its copies do not count), and every TaleWorlds or SandBox assembly `TAOM.dll` references that is not client-only must be present. Tagged `LiveInstall`. | B1 |
| `DependenciesClientIlTests` | Every method and static constructor of every type in `TAOM.Dependencies.dll`, compiler-generated ones included, except the two NoInlining helpers, references no client type (call, field, type and ldtoken operands, a resolved generic method's own type arguments, locals, catch clauses, signatures); the helpers do hold the references and are NoInlining; every submodule of the Dependencies manifest except `TAOM.Dependencies` carries both server tags; and a guard test per site (`StaticConstructor_UiHelperCall_IsGuardedByTheServerCheck`, `OnSubModuleLoad_UiHelperCall_IsGuardedByTheServerCheck`): the helper call is reached only after a read of `GameNetwork.IsDedicatedServer` and a conditional branch, so removing the `if` fails. | B2 |
| `SubModuleServerGuardTests` (`TAOM.Tests/Composition/`) | One source row per B6c site (comments stripped, exactly one match): the step sits behind `if (!ClientComposition.IsDedicatedServer())`, so a removed or inverted check fails; each of the seven helpers is called once and is NoInlining; the server report is the first statement after the once-per-process latch; and (`RequiresGame`) no method of `TAOM.SubModule` or its nested types names a client-only type in IL. That last walk covers only `SubModule`'s own IL, not what a callee names, so a new startup call whose callee names a client type needs its own row. | B6c |
| `ManualPatchApplicatorServerGuardTests` | Every method of `ManualPatchApplicator` and its nested types except `ApplyCaptainTooltip` and `ApplyVisualPatches` (and their generated methods) names no client-only type, which covers lambdas and local functions; each helper has exactly one call instruction, in `ApplyAll`, and is NoInlining; the source guards sit before and after the settlement-guard block. | B6c |
| `IlClientReferencesTests`, `ServerAssemblyReportTests` | The shared IL walk (`IlClientReferences`, also behind `DependenciesClientIlTests`) reports a sentinel for any `calli`, whose stand-alone signature it does not decode, so the gates fail closed on one (an emitted fixture). The report's 16 names equal `ClientAssemblies.Names`, and `Log` returns when the logger throws. | B6c |
| `ServerJitGateTests.ServerReachableMethods_ReferenceNoClientAssembly` | The runtime ratchet. IL-token scan of every `TAOM.dll` method; a method referencing `ClientAssemblies` must be in a `[ClientOnly]` type, carry `[ClientOnly]`, or sit in a checked-in baseline (`ServerJitBaseline.txt`, 739 entries today) that only shrinks. `[ClientOnly]` means "never reached headless"; its guard is the caller's server check, pinned by per-site tests. | B7 |
| `ServerLoadGateTests.PatchClassesReferencingClientAssemblies_AreClientOnly` | Every patch class whose `[HarmonyPatch]` target type or patch-method parameter types come from a client-only assembly carries `[ClientOnly]`. String-resolved targets (`TargetMethod` via `AccessTools`) are not visible to it; the boot test catches those. | B6 |

`ViewCreatorModuleGateTests` (`TAOM.Tests/Infrastructure/ViewCreatorModuleGateTests.cs`) and the
`ConversationTag` gate also extend to scan `TAOM.Client` once it exists.

## Batches

Every batch: TDD (test first, RED, GREEN); `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`
green; `/deep-review` before its commit (C# or XML); its own checkbox under one epic issue. **SO** marks a
batch that touches a single-owner file (`Main/SubModule.cs` or `Main/IoC.cs`): the file's owner applies those
hunks and a builder agent recommends them.

| Batch | Scope | Tier | Single-owner files | Boot test |
|---|---|---|---|---|
| B0 | ADR 013 and the epic with one checkbox per batch (issue creation needs the maintainer's word). No code. | architect | none | none |
| B1 | Gates 1 and 2 with the 40-row allowlist: `ServerLoadGateTests.cs`, shared `ClientAssemblies.cs`. RED first: write gate 1 with an empty allowlist and confirm it lists exactly the 40; a mismatch is a finding, stop and report. | implementer | none | none (no runtime change) |
| B2 | `TAOM.Dependencies` headless: `Dependencies/SubModule.cs`, both guards read `GameNetwork.IsDedicatedServer`, `DedicatedServerProvider` reads it (the test seam), two manifest tags. Tests first: `DedicatedServerProviderTests`, `DependenciesClientIlTests`, a manifest test. | implementer | none | Server with TAOM staged without `TAOM` and with only `TAOM.Dependencies` kept: no `TAOM.Dependencies could not be loaded correctly`, boot reaches Coop. Then a desktop client boot, UI unchanged. The first boot ran the bin-folder build: repeat it on the build constant. |
| B3 | `TAOM.Client` skeleton and hand-off slot: `Client/TAOM.Client.csproj`, empty `ClientSubModule`, core `Composition/ClientComposition.cs`, second submodule in the manifest (core's tags unchanged until B9), the server-mirror exclusion of `TAOM.Client.*`, build-script plan by architect. **Committed as `03d9db8dc` (2026-10-22); the follow-up (stale client deletes, gate and tripwire fixes) is in progress.** | architect plans, implementer executes | `Main/IoC.cs` (one call to the slot) | Client only: boots with both submodules. Server unchanged. |
| B4a | Front-end types to `TAOM.Client` (14 types plus the callers listed in the core-methods paragraph as needed). Research first: how Gauntlet discovers widget types across a second assembly (`pwsh tools/taom-src.ps1 path WidgetFactory`). | implementer | none unless a caller is in `SubModule.cs` | Client: themed main menu, faction screen, custom battle faction list. |
| B4b | Campaign-map types (6) and three helpers. Inversions become core adapter interfaces (ADR-007) with a no-op default, the client implementation registered through the slot. Built 2026-10-22 without a `Main/SubModule.cs` or `Main/IoC.cs` hunk (the field camp overlay arrives through a static set in `FieldCampIoC.InitializePatchStatics`); review, commit and the client boot are owed. | implementer | none | Client: realm borders, camp overlay, momentum meter, fiefs nav button, settlement plates, Hall of Fire conversation. |
| B4c (built, client boot owed) | Mission, custom-battle engine and WinForms types (7). Keep the mission-behavior add order the comments around `Main/SubModule.cs:2084-2092` depend on. `ShaderPrecompilation` is a parked feature: sweep every file its last toggle touched. Sized by whether the dedicated server runs missions at all (problem row 11, still UNVERIFIED: the guard makes a server mission a logged no-op). | implementer, architect reviews the Warg controller swap | `Main/SubModule.cs` | Client: a battle with a warg rider as player, battle action bar, host review, shader notice at cold start. |
| B5 (built, client and server boots owed) | UIExtenderEx moves to the client (13 types, 15 prefab classes, the three Patch109 postfixes, `UIExtender.Create/Register/Enable` and `RegisterUiExtensions`, the attribute-reading half of `CoopUiRegistrationPolicy`). Allowlist reaches zero: M1. | implementer | `Main/SubModule.cs` | Server (the milestone), current staging with only `TAOM` kept: no `Error while getting types` for `TAOM.dll`, no `TAOM could not be loaded correctly`, and the first post-`GetTypes` failure recorded as B6 input. Client: every mixin and prefab extension present. |
| B6 | Lifecycle guards headless (problem rows 2 to 9), one hook per sub-batch: 6a `[ClientOnly]`, `PatchCategoryIndex.Build(assembly, headless)`, the 11 split categories, gate 4; 6b `TaomFeatureModule.ClientOnly` and a client flag on `PatchCategoryDecl` honored by `ModuleRunner` without faulting; 6c the `SubModule.cs` guards, including `CustomBattlesIoC.InitializeHooks`, which throws headless (`SelectorVM<CharacterItemVM>`, `typeof(CustomBattleSideVM)`) and is called at `Main/SubModule.cs:565` with no try. | implementer | `Main/SubModule.cs` (6a call sites, 6c) | After each sub-batch: server reaches `OnGameInitializationFinished` for the staged save with no TAOM exception; record the next failure. |
| B7 | Campaign-time UI paths (row 10 and the remaining 97 candidates), one feature group per slice: map visuals, party intel, map profiler targets. Gate 3 lands in the first slice. | implementer | none unless a caller is in `SubModule.cs` | Server runs the staged campaign for several in-game days with no TAOM exception; a guest connects and plays a day. |
| B8 | Settings headless. Minimum: one boot line "MCM not loaded: TAOM settings at compiled defaults" plus the existing fingerprint log (it can go in `SettingsFingerprintLog`). Option 2 is its own design and issue. Needs the settings decision. | implementer | none | Boot line present. |
| B9 | Manifest flip and the generic staging rule: TAOM first (core tags removed; the second submodule and the server-mirror exclusion of `TAOM.Client` landed in B3), then the server repo's two scripts. | implementer, two repos | none | Stage with no `-KeepSubModules`; the submodule list matches the expected set; the B7 campaign run repeats clean. |
| B10 | Docs and closeout: the "not supported either" bullet in [coop-interop](../features/coop-interop.md), a feature doc, its feature-map row, an orientation trap line, the lessons entry, `python tools/lint_docs.py --fail-on-drift`. | implementer | none | none |

**Order and parallelism.** B0, then B1 and B2 in parallel (disjoint files), B3, then B4a and B4b in parallel.
B4c and B5 both touch `SubModule.cs`: serial, in a worktree. Then the M1 boot, B6a to B6c serial, B7 slices in
parallel up to four wide, B8, B9, B10.

**Boot-test procedure.** The server repo owns port 4200, so these wait for a free machine and the maintainer's
word: deploy the build under test with `./build.ps1` (which also refreshes `bin/Win64_Shipping_Server`
through `MirrorWin64ShippingClientToServer`), restage, boot, then read the newest `rgl_log_*.txt` under
`C:/ProgramData/Mount and Blade II Bannerlord/logs/` for `could not be loaded correctly`,
`Error while getting types`, `Loader Exceptions`, the TAOM lines the batch names, and the last line before
exit (exit 5 with no message is `MBDebug.AbortGame`). TAOM's own log on the server is
`<stage>\engine\bin\Win64_Shipping_Server\Logs\taom_debug_*.log`. Record the first failure in the batch's issue checkbox;
it seeds the next batch.

## UNVERIFIED, with the step that settles each

| Item | Settling step |
|---|---|
| Harmony patching `Assembly.GetTypes` in the server's CoreLib (option c) | An uncommitted spike. |
| The JIT-time failure rule for a method naming an unresolvable assembly | Settled 2026-10-23 by the spike boot on .NET 6.0.36: the callee fails when first called, the caller compiles (see "Startup and campaign-time problem points"). Only "before its first line" stays [Likely]. |
| Whether Coop's view-layer copies make TAOM UI paths resolve after Coop loads | Settled for game init by the spike boot (SandBox.View targets patched there). Which assemblies are loaded at game init on each boot: the B6c `[Server] client-only assemblies loaded at game init` line. Campaign-time paths are still B7. |
| Gauntlet widget discovery across a second assembly | Read from the decompile (`WidgetInfo.CollectWidgetTypes` scans every loaded assembly that references TaleWorlds.GauntletUI; `WidgetFactory` keys by short type name), and `ClientWidgetDiscoveryTests` pins the preconditions. The runtime proof is the client boot's visual checks: on a client Debug.FailedAssert writes nothing (v1.5.5 MBDebugManager.Assert is empty), so the rgl log cannot show a widget Gauntlet failed to find. |
| Whether the server runs missions (row 11, still unproven after B4c: a server mission is a logged no-op). Row 5's hotkey question is moot: B6c skips the registration headless. | The first clean boot. |
| Where MCM's json2 file lives on the server profile. TAOM's log is at `<stage>\engine\bin\Win64_Shipping_Server\Logs\taom_debug_*.log` (found in the 2026-10-23 spike boot). | Find it in the M1 boot. |
| The value-field, generic-constraint and override shapes of a failing `GetTypes` on .NET 6 | The B2 review's experiment ran on .NET Framework 4.8 and CoreCLR 10. Shapes 3 and 4 occur only inside the 40 (see above), and gate 1 flags every shape. |
| The server repo's test harness for its staging scripts | Read that repo before B9. |
| Engine statements taken from the server-build decompile (load order, tag checks, `AbortGame`) | Partly settled 2026-10-23 by the B6c review's server `Module` decompile: `GameNetwork.IsDedicatedServer => true` in both server builds (Win64 and Linux64) and read by the engine before `OnSubModuleLoad`; `InitializeSubModuleBases` rethrows a hook failure; `OnBeforeInitialModuleScreenSetAsRoot` runs only while no game state is active. Load order, tag checks and `AbortGame` are not re-read. |

## Related

- [coop-interop](../features/coop-interop.md): the co-op feature page; its "Dedicated server: no longer unsupported, but not supported either" bullet is rewritten in B10.
- [coop-compat-gap-ledger](coop-compat-gap-ledger.md): the wider co-op gap list.
- Issue #967 (Batch C of the co-op epic #964).
- [ADR-013](../adrs/013-server-safe-core-client-ui-submodule.md) (accepted 2026-10-22): the decision this plan asks for.
- The server repo's `docs/bannerlord-1.5.x-port.md` (BannerlordCoop dedicated-server repo, outside this repo): its v1.5.5 server port notes.

---

