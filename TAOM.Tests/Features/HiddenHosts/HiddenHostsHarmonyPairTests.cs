using System;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HiddenHosts;
using TAOM.Features.HiddenHosts.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// The nameplate prefix and finalizer as a pair, through real Harmony on the real <c>PartyNameplateVM.RefreshBinding</c>
/// (harmony-patches.md, "Prefix and finalizer pair"). The link between them is Harmony's <c>__state</c>, and what a hand
/// call cannot reach is exactly what these cases do: the prefix stripped while the finalizer stays, a later finalizer that
/// throws (Harmony reruns the finalizers), and an earlier prefix that skips the original. The plate is a bare
/// <c>GetUninitializedObject</c> instance: its property setters are inert because the view model's handler lists are null,
/// and the quest block returns early because <c>_previousQuestsBind == _questsBind</c>. The SandBox types are resolved by
/// name: the test project references the TaleWorlds assemblies only.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsHarmonyPairTests
{
    private const string HarmonyId = "taom.tests.hiddenhosts.pair";
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags OwnStatic = BindingFlags.Static | BindingFlags.NonPublic;

    private static bool _gameLoaded;
    private Harmony _harmony = null!;
    private IModLogger _logger = null!;
    private MethodBase _target = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type PlateType => AccessTools.TypeByName("SandBox.ViewModelCollection.Nameplate.PartyNameplateVM");

    private static void ThrowingFinalizer() => throw new InvalidOperationException("a finalizer after ours");

    private static bool SkipOriginalPrefix() => false;

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var settings = Substitute.For<IHiddenHostsSettings>();
        settings.Enabled.Returns(true);
        var adapter = Substitute.For<IPartyIntelAdapter>();
        // A party 70 percent of the seeing range away, 123 healthy troops: the mask is "???".
        adapter.Read(Arg.Any<MobileParty>()).Returns(new PartyIntelSnapshot
        {
            HasData = true, StringId = "p1", HealthyCount = 123, DistanceToMain = 70f, MainSeeingRange = 100f, SpottingRatio = 1f,
        });
        _logger = Substitute.For<IModLogger>();
        HiddenHostsCalls.Initialize(new HiddenHostsService(settings), adapter, _logger);

        _harmony = new Harmony(HarmonyId);
        _target = AccessTools.Method(PlateType, "RefreshBinding", Type.EmptyTypes);
        _harmony.CreateClassProcessor(typeof(PartyNameplateVM_RefreshBinding_HiddenHosts_Patch)).Patch();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony?.UnpatchAll(HarmonyId);
        HiddenHostsCalls.Initialize(null, null);
    }

    private static object NewPlate()
    {
        var plate = FormatterServices.GetUninitializedObject(PlateType);
        SetField(plate, "_countBind", "123");
        SetField(plate, "_extraInfoTextBind", "5 wounded");
        SetField(plate, "_fullNameBind", "Lord Aldric");
        SetField(plate, "_isVisibleOnMapBind", true);
        PlateType.GetProperty("Party", AnyInstance)!.SetValue(plate, FormatterServices.GetUninitializedObject(typeof(MobileParty)));
        return plate;
    }

    private static void SetField(object plate, string name, object value) => AccessTools.Field(PlateType, name).SetValue(plate, value);

    private static string? Field(object plate, string name) => (string?)AccessTools.Field(PlateType, name).GetValue(plate);

    private static string? Property(object plate, string name) => (string?)PlateType.GetProperty(name, AnyInstance)!.GetValue(plate);

    private void CallRefreshBinding(object plate) => _target.Invoke(plate, null);

    private void AssertVanillaFields(object plate)
    {
        Assert.AreEqual("123", Field(plate, "_countBind"));
        Assert.AreEqual("5 wounded", Field(plate, "_extraInfoTextBind"));
        Assert.AreEqual("Lord Aldric", Field(plate, "_fullNameBind"));
    }

    [TestMethod]
    public void Masked_ThePropertiesTakeTheMask_AndTheFieldsAreVanillasAgain()
    {
        var plate = NewPlate();

        CallRefreshBinding(plate);

        Assert.AreEqual("???", Property(plate, "Count"));
        Assert.AreEqual(string.Empty, Property(plate, "ExtraInfoText"));
        AssertVanillaFields(plate);
        _logger.DidNotReceiveWithAnyArgs().LogError(null!);
    }

    [TestMethod]
    public void PrefixStripped_TheFinalizerStays_AndTheCallRunsVanilla()
    {
        // PatchShield removes prefixes but never finalizers: the finalizer then runs with an unset __state.
        _harmony.Unpatch(_target, HarmonyPatchType.Prefix, HarmonyId);
        var plate = NewPlate();

        CallRefreshBinding(plate);

        AssertVanillaFields(plate);
        Assert.AreEqual("123", Property(plate, "Count"));
        Assert.AreEqual("5 wounded", Property(plate, "ExtraInfoText"));
        Assert.IsFalse(HiddenHostsCalls.Stopped);
        _logger.DidNotReceiveWithAnyArgs().LogError(null!);
    }

    [TestMethod]
    public void ALaterFinalizerThrows_TheExceptionPropagates_AndTheFieldsAreStillRestored()
    {
        _harmony.Patch(_target, finalizer: new HarmonyMethod(typeof(HiddenHostsHarmonyPairTests).GetMethod(nameof(ThrowingFinalizer), OwnStatic))
        {
            priority = Priority.Last,
        });
        var plate = NewPlate();

        var thrown = Assert.ThrowsException<TargetInvocationException>(() => CallRefreshBinding(plate));

        Assert.IsInstanceOfType(thrown.InnerException, typeof(InvalidOperationException));
        Assert.AreEqual("a finalizer after ours", thrown.InnerException!.Message);
        AssertVanillaFields(plate);
    }

    [TestMethod]
    public void AnEarlierPrefixSkipsTheOriginal_TheFieldsAreVanillas_AndTheCountIsUntouched()
    {
        _harmony.Patch(_target, prefix: new HarmonyMethod(typeof(HiddenHostsHarmonyPairTests).GetMethod(nameof(SkipOriginalPrefix), OwnStatic))
        {
            priority = Priority.First,
        });
        var plate = NewPlate();

        CallRefreshBinding(plate);

        AssertVanillaFields(plate);
        Assert.IsNull(Property(plate, "Count"), "the original never ran, so nothing was copied to the property");
    }
}
