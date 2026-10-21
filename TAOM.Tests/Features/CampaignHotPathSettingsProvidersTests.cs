using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.FieldCamp;
using TAOM.Features.SupplyLines;
using TAOM.Features.CaravanTrade;
using TAOM.Features.CastleRecruitment;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.FieldCommission;
using TAOM.Features.FiefManagement;
using TAOM.Features.HiddenHosts;
using TAOM.Features.FromTheAshes;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.FieldCommission.Domain;
using TAOM.Features.NameplateCull;
using TAOM.Features.Namegiving;
using TAOM.Features.PartyIconScale;
using TAOM.Features.PosternGate;
using TAOM.Features.QuickActions;
using TAOM.Features.Relations;
using TAOM.Features.RealmBorders;
using TAOM.Features.ReevesLedger;
using TAOM.Features.TimeAcceleration;
using TAOM.Features.WarChronicle;
using TAOM.Features.WatchfulHolds;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features;

/// <summary>
/// Campaign settings providers are read per party per hour, per caravan score, per party per day and
/// every map frame. Each takes the MCM settings reference once, in a private lazy <c>Settings</c>
/// accessor, and reads through it; no other member may resolve MCM's <c>Instance</c>, which walks MCM's
/// settings containers on every call. The accessor caches only a non-null instance, so a resolve before
/// MCM is up cannot pin the defaults. Pattern: BattleBalanceSettingsProvider (02157b18).
/// </summary>
[TestClass]
public class CampaignHotPathSettingsProvidersTests
{
    private const BindingFlags Declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic
                                          | BindingFlags.Instance | BindingFlags.Static;

    // MCM declares Instance on a generic base (GlobalSettings<T>), so match any declaring type the settings class derives from.
    private static bool IsInstanceGetter(MethodBase m) =>
        m.Name == "get_Instance" && m.DeclaringType != null
        && (m.DeclaringType.IsAssignableFrom(typeof(TaomSettings))
            || m.DeclaringType.IsAssignableFrom(typeof(BattleLoadDiagnosticsSettings)));

    private static bool CallsInstance(MethodBase m)
    {
        var il = m.GetMethodBody()?.GetILAsByteArray();
        return il != null && IlCallScanner.ExtractCalledMethods(m, il).Any(IsInstanceGetter);
    }

    [DataTestMethod]
    [DataRow(typeof(CaravanTradeSettingsProvider))]
    [DataRow(typeof(CastleRecruitmentSettingsProvider))]
    [DataRow(typeof(AlignmentDesertionSettingsProvider))]
    [DataRow(typeof(RealmBordersSettingsProvider))]
    [DataRow(typeof(FieldCommissionSettingsProvider))]
    [DataRow(typeof(QuickActionsSettingsProvider))]
    [DataRow(typeof(PartyIconScaleConfig))]
    [DataRow(typeof(TimeAccelerationSettingsProvider))]
    // #746: read every campaign frame (supply lines always, the field camp while it stands).
    [DataRow(typeof(SupplyLinesSettingsProvider))]
    [DataRow(typeof(CampSettingsProvider))]
    // #765: the war-effect strength is re-read by the registry's daily re-bake, the chronicle switch and army focus by each chronicle step.
    [DataRow(typeof(WarChronicleSettingsProvider))]
    // Read every campaign-map frame, from the Battle Load Diagnostics page.
    [DataRow(typeof(NameplateCullSettingsProvider))]
    // #789: the Fiefs map-bar button polls the fief hub opener every frame, which reads these.
    [DataRow(typeof(FiefManagementSettingsProvider))]
    // #765 M8: the hunter's speed multiplier is read every campaign frame while a hunt runs.
    [DataRow(typeof(EyeOfSauronSettingsProvider))]
    // #822: both switches are read for every party on every hourly tick.
    [DataRow(typeof(WatchfulHoldsSettingsProvider))]
    // #846: the nameplate toggle is read for every visible party nameplate every map frame.
    [DataRow(typeof(HiddenHostsSettingsProvider))]
    // #862: the master switch is read on every relation read (main thread).
    [DataRow(typeof(PersonalRelationSettingsProvider))]
    // The defender's siege wait menu re-checks the break-out options every frame, and each check asks the Postern Gate model.
    [DataRow(typeof(PosternGateSettingsProvider))]
    // The host menus are plain menus, so the start conditions run when a menu opens or refreshes; the accessor still takes the settings once.
    [DataRow(typeof(FromTheAshesSettingsProvider))]
    // Tokens of Esteem: the gate and the list read the group at every gift line a conversation shows.
    [DataRow(typeof(GiftSettingsProvider))]
    // Not a hot path: both switches are read once, when the town management window opens. The accessor still takes the settings once.
    [DataRow(typeof(ReevesLedgerSettingsProvider))]
    public void OnlyTheLazySettingsAccessor_ReadsTheMcmInstance(Type provider)
    {
        var accessor = provider.GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            ?.GetGetMethod(true);
        Assert.IsNotNull(accessor, provider.Name + ": the private lazy Settings accessor");
        Assert.IsTrue(CallsInstance(accessor), provider.Name + ": the lazy accessor takes the settings reference");

        var offenders = provider.GetMethods(Declared).Cast<MethodBase>()
            .Concat(provider.GetConstructors(Declared))
            .Where(m => m.Name != "get_Settings" && CallsInstance(m))
            .Select(m => m.Name)
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            provider.Name + " resolves the MCM instance outside the lazy accessor: " + string.Join(", ", offenders));
    }

    // A second (internal, test-only) constructor must leave exactly one public constructor, or DryIoc
    // cannot select one at registration. FieldCommission registers through a delegate and
    // PartyIconScaleConfig is static, so neither is a row here.
    [DataTestMethod]
    [DataRow(typeof(ICaravanTradeSettingsProvider), typeof(CaravanTradeSettingsProvider))]
    [DataRow(typeof(ICastleRecruitmentSettingsProvider), typeof(CastleRecruitmentSettingsProvider))]
    [DataRow(typeof(IAlignmentDesertionSettingsProvider), typeof(AlignmentDesertionSettingsProvider))]
    [DataRow(typeof(IRealmBordersSettings), typeof(RealmBordersSettingsProvider))]
    [DataRow(typeof(IQuickActionsSettingsProvider), typeof(QuickActionsSettingsProvider))]
    [DataRow(typeof(ITimeAccelerationSettingsProvider), typeof(TimeAccelerationSettingsProvider))]
    [DataRow(typeof(ISupplyLinesSettingsProvider), typeof(SupplyLinesSettingsProvider))]
    [DataRow(typeof(ICampSettingsProvider), typeof(CampSettingsProvider))]
    [DataRow(typeof(IWarChronicleSettingsProvider), typeof(WarChronicleSettingsProvider))]
    [DataRow(typeof(INameplateCullSettingsProvider), typeof(NameplateCullSettingsProvider))]
    [DataRow(typeof(IFiefManagementSettingsProvider), typeof(FiefManagementSettingsProvider))]
    [DataRow(typeof(IEyeOfSauronSettings), typeof(EyeOfSauronSettingsProvider))]
    [DataRow(typeof(IWatchfulHoldsSettings), typeof(WatchfulHoldsSettingsProvider))]
    [DataRow(typeof(IHiddenHostsSettings), typeof(HiddenHostsSettingsProvider))]
    [DataRow(typeof(IPersonalRelationSettingsProvider), typeof(PersonalRelationSettingsProvider))]
    [DataRow(typeof(IPosternGateSettingsProvider), typeof(PosternGateSettingsProvider))]
    [DataRow(typeof(IFromTheAshesSettingsProvider), typeof(FromTheAshesSettingsProvider))]
    [DataRow(typeof(IGiftSettingsProvider), typeof(GiftSettingsProvider))]
    [DataRow(typeof(ReevesLedgerSettingsProvider), typeof(ReevesLedgerSettingsProvider))]
    public void Provider_ResolvesFromARealContainer(Type service, Type implementation)
    {
        using var container = new Container();
        foreach (var parameter in implementation.GetConstructors().Single().GetParameters())
            container.RegisterInstance(parameter.ParameterType,
                Substitute.For(new[] { parameter.ParameterType }, new object[0]));
        container.Register(service, implementation, Reuse.Singleton);

        Assert.IsInstanceOfType(container.Resolve(service), implementation);
    }

    // Read THROUGH the cached object, never snapshotted: MCM edits its one registered TaomSettings in
    // place, so an edit after the provider is built and read must reach the getter. Each "before" value
    // is the compiled MCM default, so a getter wired to the wrong setting fails too.
    [TestMethod]
    public void Gifts_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new GiftSettingsProvider(mcm);
        Assert.IsTrue(sut.Get().Enabled);
        Assert.AreEqual(7, sut.Get().CooldownDays);

        mcm.GiftsEnabled = false;
        mcm.GiftCooldownDays = 21;

        Assert.IsFalse(sut.Get().Enabled);
        Assert.AreEqual(21, sut.Get().CooldownDays);
    }

    [TestMethod]
    public void PersonalRelations_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new PersonalRelationSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);

        mcm.PersonalRelationsEnabled = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void CaravanTrade_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<ICaravanTradeConfigProvider>();
        cfg.GetConfig().Returns(new CaravanTradeConfig());
        var sut = new CaravanTradeSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.AreEqual(1.6f, sut.RangeMultiplier, 0.0001f);

        mcm.EnableCaravanTrade = false;
        mcm.CaravanRangeMultiplier = 2.5f;

        Assert.IsFalse(sut.Enabled);
        Assert.AreEqual(2.5f, sut.RangeMultiplier, 0.0001f);
    }

    [TestMethod]
    public void CastleRecruitment_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<ICastleRecruitmentConfigProvider>();
        cfg.GetConfig().Returns(new CastleRecruitmentConfig());
        var sut = new CastleRecruitmentSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.IsAiEnabled);
        Assert.AreEqual(3, sut.NotablesPerCastle);

        mcm.EnableCastleRecruitmentAi = false;
        mcm.CastleNotablesPerCastle = 5;

        Assert.IsFalse(sut.IsAiEnabled);
        Assert.AreEqual(5, sut.NotablesPerCastle);
    }

    [TestMethod]
    public void AlignmentDesertion_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var cfg = Substitute.For<IAlignmentDesertionConfigProvider>();
        cfg.GetConfig().Returns(new AlignmentDesertionConfig());
        var sut = new AlignmentDesertionSettingsProvider(cfg, mcm);
        Assert.IsTrue(sut.IsEnabled);
        Assert.AreEqual(0.5f, sut.Rate, 0.0001f);

        mcm.EnableAlignmentDesertion = false;
        mcm.AlignmentDesertionRate = 0.25f;

        Assert.IsFalse(sut.IsEnabled);
        Assert.AreEqual(0.25f, sut.Rate, 0.0001f);
    }

    [TestMethod]
    public void WatchfulHolds_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new WatchfulHoldsSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsTrue(sut.HarassAiLords);

        mcm.EnableWatchfulHolds = false;
        mcm.WatchfulHoldsHarassAiLords = false;

        Assert.IsFalse(sut.Enabled);
        Assert.IsFalse(sut.HarassAiLords);
    }

    [TestMethod]
    public void RealmBorders_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new RealmBordersSettingsProvider(Substitute.For<IModLogger>(), mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsFalse(sut.HeraldicBands);

        mcm.EnableRealmBorders = false;
        mcm.RealmBordersHeraldicBands = true;

        Assert.IsFalse(sut.Enabled);
        Assert.IsTrue(sut.HeraldicBands);
    }

    [TestMethod]
    public void FieldCommission_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var inner = Substitute.For<IFieldCommissionConfigProvider>();
        inner.GetConfig().Returns(new FieldCommissionConfig());
        var sut = new FieldCommissionSettingsProvider(inner, mcm);
        Assert.IsTrue(sut.GetConfig().Enabled);

        mcm.EnableFieldCommission = false;

        Assert.IsFalse(sut.GetConfig().Enabled);
    }

    [TestMethod]
    public void QuickActions_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new QuickActionsSettingsProvider(mcm);
        Assert.IsTrue(sut.EnableInventorySearch);

        mcm.EnableInventorySearch = false;

        Assert.IsFalse(sut.EnableInventorySearch);
    }

    [TestMethod]
    public void SupplyLines_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new SupplyLinesSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.IsTrue(sut.ShowRouteVisual);

        mcm.EnableSupplyLines = false;
        mcm.SupplyShowRouteVisual = false;
        mcm.SupplyGoodsMarkupFactor = 2f;

        Assert.IsFalse(sut.Enabled);
        Assert.IsFalse(sut.ShowRouteVisual);
        Assert.AreEqual(2f, sut.GoodsMarkupFactor);
    }

    [TestMethod]
    public void FieldCamp_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new CampSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);
        Assert.AreEqual(4f, sut.CampSetupHours);

        mcm.EnableFieldCamps = false;
        mcm.CampSetupHours = 8f;

        Assert.IsFalse(sut.Enabled);
        Assert.AreEqual(8f, sut.CampSetupHours);
    }

    [TestMethod]
    public void FiefManagement_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new FiefManagementSettingsProvider(mcm);
        Assert.IsTrue(sut.EnableFiefManagement);
        Assert.IsTrue(sut.AllowRemoteBuildingQueue);
        Assert.IsFalse(sut.IsDebugMode);

        mcm.EnableFiefManagement = false;
        mcm.AllowRemoteBuildingQueue = false;
        mcm.FiefManagementDebug = true;

        Assert.IsFalse(sut.EnableFiefManagement);
        Assert.IsFalse(sut.AllowRemoteBuildingQueue);
        Assert.IsTrue(sut.IsDebugMode);
    }

    [TestMethod]
    public void FiefManagement_NoMcm_FallsBackToTheDefaults()
    {
        var sut = new FiefManagementSettingsProvider();

        Assert.IsTrue(sut.EnableFiefManagement);
        Assert.IsTrue(sut.AllowRemoteBuildingQueue);
        Assert.IsFalse(sut.IsDebugMode);
    }

    [TestMethod]
    public void TimeAcceleration_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new TimeAccelerationSettingsProvider(mcm);
        Assert.AreEqual(4, sut.FastForwardMultiplier);
        Assert.AreEqual(8, sut.ExtraFastForwardMultiplier);
        Assert.AreEqual(16, sut.CtrlSpaceMultiplier);

        mcm.FastForwardMultiplier = 10;
        mcm.CtrlSpaceMultiplier = 32;

        Assert.AreEqual(10, sut.FastForwardMultiplier);
        Assert.AreEqual(10, sut.ExtraFastForwardMultiplier, "extra is floored at fast");
        Assert.AreEqual(32, sut.CtrlSpaceMultiplier);
    }

    [TestMethod]
    public void NameplateCull_ReadsThroughTheCachedSettings()
    {
        var settings = new BattleLoadDiagnosticsSettings();
        var sut = new NameplateCullSettingsProvider(settings);
        Assert.IsTrue(sut.CullEnabled);

        settings.CullHiddenNameplates = false;

        Assert.IsFalse(sut.CullEnabled);
    }

    [TestMethod]
    public void HiddenHosts_ReadsThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new HiddenHostsSettingsProvider(mcm);
        Assert.IsTrue(sut.Enabled);

        mcm.HiddenHostsEnabled = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void PosternGate_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new PosternGateSettingsProvider(settings);
        Assert.IsTrue(sut.CutEnabled);

        settings.EnablePosternGate = false;

        Assert.IsFalse(sut.CutEnabled);
    }

    [TestMethod]
    public void FromTheAshes_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new FromTheAshesSettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);

        settings.EnableFromTheAshes = false;

        Assert.IsFalse(sut.Enabled);
    }

    [TestMethod]
    public void ReevesLedger_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new ReevesLedgerSettingsProvider(settings);
        Assert.IsTrue(sut.LedgerEnabled);
        Assert.IsTrue(sut.StepsEnabled);

        settings.EnableReevesLedger = false;
        settings.EnableReserveSteps = false;

        Assert.IsFalse(sut.LedgerEnabled);
        Assert.IsFalse(sut.StepsEnabled);
    }

    [TestMethod]
    public void Namegiving_ReadsThroughTheCachedSettings()
    {
        var settings = new TaomSettings();
        var sut = new NamegivingSettingsProvider(settings);
        Assert.IsTrue(sut.Enabled);

        settings.EnableNamegiving = false;

        Assert.IsFalse(sut.Enabled);
    }

    // No-MCM pins: TaomSettings.Instance is null in the test host (MCM is never initialised), so these
    // hold today's fallbacks through the caching change.
    [TestMethod]
    public void QuickActions_NoMcm_InventorySearchDefaultsOn()
        => Assert.IsTrue(new QuickActionsSettingsProvider().EnableInventorySearch);

    [TestMethod]
    public void PartyIconScale_NoMcm_GetScaleIsTheDefault()
        => Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.GetScale(), 0.0001f);

    [TestMethod]
    public void NameplateCull_NoMcm_CullDefaultsOn()
        => Assert.IsTrue(new NameplateCullSettingsProvider().CullEnabled);

    [TestMethod]
    public void CastleRecruitment_NoMcm_TogglesFallBackToJson()
    {
        var cfg = Substitute.For<ICastleRecruitmentConfigProvider>();
        cfg.GetConfig().Returns(new CastleRecruitmentConfig { Enabled = false, AiEnabled = false });
        var sut = new CastleRecruitmentSettingsProvider(cfg);

        Assert.IsFalse(sut.IsEnabled);
        Assert.IsFalse(sut.IsAiEnabled);
    }
}
