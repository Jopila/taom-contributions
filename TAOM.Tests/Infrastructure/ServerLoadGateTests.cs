using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Headless gates. On the dedicated server the engine runs <c>Assembly.GetTypes()</c> on TAOM.dll, and a
/// type that needs a client-only assembly to be laid out makes that call throw, so TAOM does not load.
/// Reference-typed fields and method bodies do not fail GetTypes (26 field-only and 190 body-only types
/// loaded in run 46908), so the walk covers base chain, interfaces, value-typed fields, generic
/// constraints and cross-assembly override signatures
/// (see <c>ServerLoadScan</c>; its fixtures are <c>ServerLoadFixtureTests</c>). The allowlist is the set known to fail today and can only shrink.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ServerLoadGateTests
{
    private const string StageEnvVar = "TAOM_DS_STAGE";
    private const string DefaultStageRoot = @"E:\repos\BannerlordCoop.DedicatedServer\.stage-win\engine";

    // Modules ahead of TAOM in the stock order; the Coop module is after it.
    private static readonly string[] ModulesLoadedBeforeTaom =
        { "Native", "SandBoxCore", "SandBox", "TAOM.Dependencies", "LOTRLOME_Armory", "TAOM_Map" };

    // The 40 types that failed GetTypes in rgl_log_46908.txt. Remove an entry when its type moves out of
    // TAOM.dll; the test fails if an entry stops failing, so this list never grows back.
    private static readonly HashSet<string> KnownClientTypesInCore = new HashSet<string>(StringComparer.Ordinal)
    {
        "TAOM.Features.WarOfTheRingMomentum.UI.MomentumIndicatorMapView",
        "TAOM.Features.TimeAcceleration.UI.TimeAccelerationMixin",
        "TAOM.Features.TelcharsMark.UI.UpgradeRowMixin",
        "TAOM.Features.TelcharsMark.UI.UpgradeScreenMixin",
        "TAOM.Features.SpecialResources.UI.EncyclopediaUnitBadgeMixin",
        "TAOM.Features.SpecialResources.UI.SpecialResourceMapBarMixin",
        "TAOM.Features.SpecialResources.UI.SpecialResourceSpriteWidget",
        "TAOM.Features.ShaderPrecompilation.TaomShaderGameManager",
        "TAOM.Features.ShaderCompileNotice.ShaderCompileNoticeWindow",
        "TAOM.Features.ShaderCompileNotice.WindowHandleOwner",
        "TAOM.Features.SettlementNameplateRelation.TaomSettlementPlateWidget",
        "TAOM.Features.ReevesLedger.UI.ReserveStepMixin",
        "TAOM.Features.ReevesLedger.UI.TownManagementLedgerMixin",
        "TAOM.Features.RealmBorders.UI.RealmBordersMapView",
        "TAOM.Features.Namegiving.UI.FiefNamingMixin",
        "TAOM.Features.Messengers.UI.MessengerEncyclopediaMixin",
        "TAOM.Features.HostReview.Hooks.HostReviewVisualOrderProvider",
        "TAOM.Features.HallOfFire.TaomMapConversationDataProvider",
        "TAOM.Features.FieldCamp.UI.FieldCampMapView",
        "TAOM.Features.FiefManagement.UI.TaomFiefsNavigationElement",
        "TAOM.Features.FactionUI.UI.Widgets.FilteredPropertyRow",
        "TAOM.Features.FactionUI.UI.Widgets.FrontEndRichTextWidget",
        "TAOM.Features.FactionUI.UI.Widgets.FrontEndTextWidget",
        "TAOM.Features.FactionUI.UI.Widgets.NarrativeRandomButton",
        "TAOM.Features.FactionUI.UI.Widgets.ScrollShortcutButton",
        "TAOM.Features.FactionUI.UI.Widgets.StageJumpButton",
        "TAOM.Features.FactionUI.UI.Widgets.ThemeIconWidget",
        "TAOM.Features.FactionMap.Widgets.BannerWidget",
        "TAOM.Features.FactionMap.Widgets.FactionImageWidget",
        "TAOM.Features.FactionMap.Widgets.MapContainerWidget",
        "TAOM.Features.FactionMap.Widgets.PolygonWidget",
        "TAOM.Features.FactionMap.Widgets.RuntimeSprite",
        "TAOM.Features.CustomBattles.TaomFactionSelectionVM",
        "TAOM.Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView",
        "TAOM.Features.CareerSystem.UI.CharacterDeveloperCareerMixin",
        "TAOM.Features.CareerSystem.UI.MissionAgentStatusCareerMixin",
        "TAOM.Features.CareerSystem.UI.TaomCareerDiamondWidget",
        "TAOM.Features.AdvancedCombat.AutonomousMovementPlayerController",
        "TAOM.Adapters.FrontEndRuntimeSprite",
        "TAOM.Features.ShaderCompileNotice.ShaderCompileNotice+NoticeContext",
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) =>
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void EveryCoreType_LoadsWithoutClientAssemblies()
    {
        var failing = ServerLoadScan.FindClientDependentTypes(typeof(SubModule).Assembly.GetTypes(), ClientAssemblies.IsClientOnly)
            .ToDictionary(kv => kv.Key.FullName!, kv => kv.Value, StringComparer.Ordinal);

        var added = failing.Where(kv => !KnownClientTypesInCore.Contains(kv.Key))
            .Select(kv => $"{kv.Key} (from {kv.Value.Assembly}, shape {(int)kv.Value.Shape} {kv.Value.Shape})")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        var healed = KnownClientTypesInCore.Where(n => !failing.ContainsKey(n))
            .OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.IsTrue(added.Count == 0,
            "New TAOM.dll types would fail to load on the dedicated server (they need a client-only assembly to be "
            + "laid out: shape 1 base type or interface, 2 value-typed field, 3 generic constraint, 4 cross-assembly "
            + "override). Put them in a client assembly or break the dependency: " + string.Join("; ", added));
        Assert.IsTrue(healed.Count == 0,
            "These allowlisted types no longer depend on a client-only assembly; remove them from "
            + "KnownClientTypesInCore: " + string.Join("; ", healed));
    }

    [TestMethod]
    public void EveryDependenciesType_LoadsWithoutClientAssemblies()
    {
        var failing = ServerLoadScan.FindClientDependentTypes(
                typeof(TAOM.Dependencies.SubModule).Assembly.GetTypes(), ClientAssemblies.IsClientOnly)
            .Select(kv => $"{kv.Key.FullName} (from {kv.Value.Assembly}, shape {(int)kv.Value.Shape} {kv.Value.Shape})")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();

        CollectionAssert.AreEqual(new List<string>(), failing,
            "TAOM.Dependencies.dll loads first on the dedicated server and must have no type that needs a client-only assembly");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void ClientOnlyList_AgainstStagedServer_ListedAbsentAndOtherReferencesPresent()
    {
        var root = Environment.GetEnvironmentVariable(StageEnvVar);
        if (string.IsNullOrEmpty(root))
            root = DefaultStageRoot;
        var appBin = Path.Combine(root, "bin", "Win64_Shipping_Server");
        if (!Directory.Exists(appBin))
            Assert.Inconclusive("No staged server at " + root + " (set " + StageEnvVar + ")");

        // What TAOM.dll can see when it loads: the app bin plus the bins of the modules ahead of TAOM in the
        // stock order. The Coop module loads after TAOM, so its copies of the client assemblies do not count.
        var dirs = new List<string> { appBin };
        dirs.AddRange(ModulesLoadedBeforeTaom
            .Select(m => Path.Combine(root, "Modules", m, "bin", "Win64_Shipping_Server")).Where(Directory.Exists));

        bool Visible(string name) =>
            dirs.Any(d => File.Exists(Path.Combine(d, name + ".dll")));

        var listedButPresent = ClientAssemblies.Names.Where(Visible).ToList();
        Assert.IsTrue(listedButPresent.Count == 0,
            "Listed as client-only but visible to TAOM.dll on the staged server (remove from ClientAssemblies): "
            + string.Join(", ", listedButPresent));

        var missing = typeof(SubModule).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("TaleWorlds.", StringComparison.Ordinal)
                        || n.StartsWith("SandBox", StringComparison.Ordinal))
            .Where(n => !ClientAssemblies.IsClientOnly(n))
            .Where(n => !Visible(n))
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.IsTrue(missing.Count == 0,
            "TAOM.dll references these TaleWorlds/SandBox assemblies, not visible on the staged server and not in "
            + "ClientAssemblies: " + string.Join(", ", missing));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TaomClient_AbsentFromTheStagedServer()
    {
        var root = Environment.GetEnvironmentVariable(StageEnvVar);
        if (string.IsNullOrEmpty(root))
            root = DefaultStageRoot;
        var appBin = Path.Combine(root, "bin", "Win64_Shipping_Server");
        var moduleBin = Path.Combine(root, "Modules", "TAOM", "bin", "Win64_Shipping_Server");
        if (!Directory.Exists(appBin) && !Directory.Exists(moduleBin))
            Assert.Inconclusive("No staged server at " + root + " (set " + StageEnvVar + ")");

        var found = new[] { appBin, moduleBin }.Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "TAOM.Client.*")).ToList();
        CollectionAssert.AreEqual(new List<string>(), found,
            "TAOM.Client is client-only (ADR-013); the server staging promotes a stray DLL into its app bin.");

        var manifest = Path.Combine(root, "Modules", "TAOM", "SubModule.xml");
        if (File.Exists(manifest))
        {
            var names = System.Xml.Linq.XDocument.Load(manifest).Descendants("SubModule")
                .Select(s => (string)s.Element("Name")?.Attribute("value")).ToList();
            CollectionAssert.DoesNotContain(names, "TAOM.Client", "The staged TAOM manifest must not list the client submodule.");
        }
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TaomClient_AbsentFromTheInstallServerMirror()
    {
        if (!TAOM.Tests.Migration.GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("No game install resolved.");
        var serverBin = Path.Combine(TAOM.Tests.Migration.GameAssemblies.GameDir, "Modules", "TAOM", "bin", "Win64_Shipping_Server");
        if (!Directory.Exists(serverBin))
            Assert.Inconclusive("No TAOM server bin at " + serverBin);

        var found = Directory.GetFiles(serverBin, "TAOM.Client.*").ToList();
        CollectionAssert.AreEqual(new List<string>(), found,
            "Main/TAOM.csproj's server mirror must never write TAOM.Client (ADR-013).");
    }
}
