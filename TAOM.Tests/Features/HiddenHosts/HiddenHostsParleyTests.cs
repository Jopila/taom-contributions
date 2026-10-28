using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.HiddenHosts;
using TAOM.Features.HiddenHosts.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #1037: a field parley must not undo Hidden Hosts' mask. The service answers from a plain snapshot: the name verdict of a
/// party drawn on the map (the plate's verdict, and the tooltip title's, which also needs the party to be visible); the postfix
/// on <c>DefaultEncounterModel.CanMainHeroDoParleyWithParty</c> asks it and, for a masked host, refuses the parley and drops
/// vanilla's explanation (which names the party), so the screen falls back to its own generic refusal line.
/// </summary>
[TestClass]
public class HiddenHostsParleyTests
{
    private IHiddenHostsSettings _settings = null!;
    private IScoutIntelQuery _scouting = null!;
    private HiddenHostsService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _scouting = Substitute.For<IScoutIntelQuery>();
        _sut = new HiddenHostsService(_settings, _scouting);
    }

    private static PartyIntelSnapshot Party(float ratio, string id = "p1", int hour = 100) => new()
    {
        HasData = true, StringId = id, HealthyCount = 123, OwnHealthyCount = 123, DistanceToMain = ratio * 100f,
        MainSeeingRange = 100f, SpottingRatio = 1f, NowHour = hour, IsVisible = true,
    };

    [TestMethod]
    public void MasksParley_HostAtTheHorizon_IsTrue() => Assert.IsTrue(_sut.MasksParley(Party(0.95f)));

    [TestMethod]
    public void MasksParley_HostInsideTheHorizon_IsFalse() => Assert.IsFalse(_sut.MasksParley(Party(0.5f)));

    [TestMethod]
    public void MasksParley_SettingOff_IsFalse()
    {
        _settings.Enabled.Returns(false);

        Assert.IsFalse(_sut.MasksParley(Party(0.95f)));
    }

    [TestMethod]
    public void MasksParley_HostTheScoutNamed_IsFalse()
    {
        _scouting.IsNamedByScout("p1", 100).Returns(true);

        Assert.IsFalse(_sut.MasksParley(Party(0.95f)));
    }

    [TestMethod]
    public void MasksParley_NoDataOrOwnSideOrPlainSight_IsFalse()
    {
        Assert.IsFalse(_sut.MasksParley(PartyIntelSnapshot.NoData));
        Assert.IsFalse(_sut.MasksParley(Party(0.95f) with { InPlayerClan = true }));
        Assert.IsFalse(_sut.MasksParley(Party(0.95f) with { InPlainSight = true }));
        Assert.IsFalse(_sut.MasksParley(Party(0.95f) with { TrueSight = true }));
    }

    [TestMethod]
    public void MasksParley_HostOutOfSight_IsFalse() =>
        Assert.IsFalse(_sut.MasksParley(Party(0.95f) with { IsVisible = false }));

    private static System.Collections.Generic.IEnumerable<PartyIntelSnapshot> Variants()
    {
        for (float ratio = 0f; ratio <= 1.2f; ratio += 0.05f)
        {
            yield return Party(ratio);
            yield return Party(ratio, "scout");
            yield return Party(ratio) with { InPlayerClan = true };
            yield return Party(ratio) with { InPlainSight = true };
            yield return Party(ratio) with { IsVisible = false };
        }
        yield return PartyIntelSnapshot.NoData;
    }

    private HiddenHostsService Fresh()
    {
        var scouting = Substitute.For<IScoutIntelQuery>();
        scouting.IsNamedByScout("scout", Arg.Any<int>()).Returns(true);
        return new HiddenHostsService(_settings, scouting);
    }

    [TestMethod]
    public void MasksParley_AgreesWithThePlate_ForVisibleAndNoDataSnapshots()
    {
        foreach (var s in Variants().Where(v => v.IsVisible || !v.HasData))
            Assert.AreEqual(Fresh().Evaluate(s).HidesName, Fresh().MasksParley(s), "ratio " + s.DistanceToMain);
    }

    [TestMethod]
    public void MasksParley_AgreesWithTheTooltipTitle_VisibleOrNot()
    {
        foreach (var s in Variants())
        {
            var verdict = Fresh().EvaluateTooltipParty(s);
            bool titleHidden = verdict.Mode == TooltipMode.Horizon && !verdict.Named;
            Assert.AreEqual(titleHidden, Fresh().MasksParley(s), "ratio " + s.DistanceToMain + " visible " + s.IsVisible);
        }
    }
}

/// <summary>#1037: the postfix itself, run for real against bare engine objects (the adapter is faked and never opens them).</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsParleyHookTests
{
    private static bool _gameLoaded;
    private IHiddenHostsSettings _settings = null!;
    private IPartyIntelAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private IScoutIntelQuery _scouting = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<IPartyIntelAdapter>();
        _logger = Substitute.For<IModLogger>();
        _scouting = Substitute.For<IScoutIntelQuery>();
        HiddenHostsCalls.Initialize(new HiddenHostsService(_settings, _scouting), _adapter, _logger);
    }

    [TestCleanup]
    public void Cleanup() => HiddenHostsCalls.Initialize(null, null);

    private static PartyIntelSnapshot Snap(float ratio) => new()
    {
        HasData = true, StringId = "p1", HealthyCount = 123, DistanceToMain = ratio * 100f, MainSeeingRange = 100f,
        SpottingRatio = 1f, NowHour = 100, IsVisible = true,
    };

    private void Sees(PartyIntelSnapshot snapshot) => _adapter.Read(Arg.Any<MobileParty>()).Returns(snapshot);

    /// <summary>A mobile party base (its <c>MobileParty</c> set) or, with <paramref name="mobile"/> false, a settlement-like one with none.</summary>
    private static PartyBase NewParty(bool mobile = true)
    {
        var party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        if (mobile)
        {
            var field = typeof(PartyBase).GetField("<MobileParty>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(party, FormatterServices.GetUninitializedObject(typeof(MobileParty)));
        }
        return party;
    }

    private static TextObject TooFar() => new("{=*}You are too far away from Lord Aldric to start parley.");

    private static void Run(PartyBase? party, ref bool result, ref TextObject explanation) =>
        DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch.Postfix(party!, ref result, ref explanation);

    [TestMethod]
    public void Postfix_MaskedHostVanillaAllowed_RefusesTheParley_WithVanillasFallbackLine()
    {
        Sees(Snap(0.95f));
        bool result = true;
        TextObject explanation = null!;

        Run(NewParty(), ref result, ref explanation);

        Assert.IsFalse(result);
        Assert.IsNull(explanation, "no text: MapScreen.TryParleyWith prints its generic refusal line");
    }

    [TestMethod]
    public void Postfix_MaskedHostVanillaRefusedWithAName_DropsTheExplanation()
    {
        Sees(Snap(0.95f));
        bool result = false;
        TextObject explanation = TooFar();

        Run(NewParty(), ref result, ref explanation);

        Assert.IsFalse(result);
        Assert.IsNull(explanation, "the too-far text names the party, so vanilla's nameless fallback applies");
    }

    [TestMethod]
    public void Postfix_MaskedHostVanillaRefusedWithNoText_StaysUntouched()
    {
        Sees(Snap(0.95f));
        bool result = false;
        TextObject explanation = null!;

        Run(NewParty(), ref result, ref explanation);

        Assert.IsFalse(result);
        Assert.IsNull(explanation, "no text to hide, and vanilla's own fallback line applies");
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void Postfix_UnmaskedHost_KeepsVanillasAnswer()
    {
        Sees(Snap(0.5f));
        bool result = true;
        TextObject explanation = null!;

        Run(NewParty(), ref result, ref explanation);

        Assert.IsTrue(result);
        Assert.IsNull(explanation);
    }

    [TestMethod]
    public void Postfix_UnmaskedHostRefused_KeepsVanillasExplanation()
    {
        Sees(Snap(0.5f));
        bool result = false;
        var vanilla = TooFar();
        TextObject explanation = vanilla;

        Run(NewParty(), ref result, ref explanation);

        Assert.IsFalse(result);
        Assert.AreSame(vanilla, explanation);
    }

    [TestMethod]
    public void Postfix_SettingOff_KeepsVanillasAnswer()
    {
        _settings.Enabled.Returns(false);
        Sees(Snap(0.95f));
        bool result = true;
        TextObject explanation = null!;

        Run(NewParty(), ref result, ref explanation);

        Assert.IsTrue(result);
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void Postfix_SettlementParty_KeepsVanillasAnswer()
    {
        Sees(Snap(0.95f));
        bool result = true;
        TextObject explanation = null!;

        Run(NewParty(mobile: false), ref result, ref explanation);

        Assert.IsTrue(result);
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void Postfix_HostTheScoutNamed_KeepsVanillasAnswer()
    {
        _scouting.IsNamedByScout("p1", 100).Returns(true);
        Sees(Snap(0.95f));
        bool result = true;
        TextObject explanation = null!;

        Run(NewParty(), ref result, ref explanation);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Postfix_NullParty_IsHarmless()
    {
        bool result = true;
        TextObject explanation = null!;

        Run(null, ref result, ref explanation);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Postfix_AdapterThrows_KeepsVanillasAnswer_AndLogsOnce()
    {
        _adapter.Read(Arg.Any<MobileParty>()).Throws(new InvalidOperationException("boom"));
        bool result = true;
        TextObject explanation = null!;

        Run(NewParty(), ref result, ref explanation);
        Run(NewParty(), ref result, ref explanation);

        Assert.IsTrue(result);
        Assert.IsNull(explanation);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains(nameof(DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch))));
    }

    [TestMethod]
    public void Postfix_FaultBudgetSpent_KeepsVanillasAnswer()
    {
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++) HiddenHostsCalls.ReportFault("x", new InvalidOperationException());
        Sees(Snap(0.95f));
        bool result = true;
        TextObject explanation = null!;

        Run(NewParty(), ref result, ref explanation);

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Postfix_IsBoundToTheEncounterModelOverride()
    {
        var attr = typeof(DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch)
            .GetCustomAttribute<HarmonyPatch>()!;
        Assert.AreEqual("TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterModel", attr.info.declaringType.FullName);
        Assert.AreEqual("CanMainHeroDoParleyWithParty", attr.info.methodName);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_AttachesAPostfixTheJitAccepts()
    {
        const string id = "taom.tests.hiddenhosts.jit.parley";
        var harmony = new Harmony(id);
        var target = AccessTools.Method(AccessTools.TypeByName("TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterModel"),
            "CanMainHeroDoParleyWithParty");
        Assert.IsNotNull(target, "the v1.5.6 override resolves");
        try
        {
            harmony.CreateClassProcessor(typeof(DefaultEncounterModel_CanMainHeroDoParleyWithParty_HiddenHosts_Patch)).Patch();

            var info = Harmony.GetPatchInfo(target);
            Assert.IsTrue(info.Postfixes.Any(p => p.owner == id), "the postfix is attached");
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id));
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
