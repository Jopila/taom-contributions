using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.HiddenHosts;
using TAOM.Features.HiddenHosts.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846: the two prefixes and the finalizer, run for real against bare engine objects. A nameplate view model and a map
/// visual cannot be constructed in a test host, but <c>FormatterServices.GetUninitializedObject</c> gives an instance whose
/// bind fields the hooks read and write by reflection, which is all they touch; the party is another bare object that the
/// faked adapter never opens. The SandBox types are resolved by name: the test project references the TaleWorlds assemblies only.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsHookTests
{
    private const string NameplateTypeName = "SandBox.ViewModelCollection.Nameplate.PartyNameplateVM";
    private const string VisualTypeName = "SandBox.View.Map.Visuals.MobilePartyVisual";
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static bool _gameLoaded;
    private IHiddenHostsSettings _settings = null!;
    private IPartyIntelAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private HiddenHostsService _service = null!;

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
        _service = new HiddenHostsService(_settings);
        HiddenHostsCalls.Initialize(_service, _adapter, _logger);
    }

    [TestCleanup]
    public void Cleanup() => HiddenHostsCalls.Initialize(null, null);

    private static PartyIntelSnapshot Snap(float ratio, int count = 123, float spotting = 1f) => new()
    {
        HasData = true, StringId = "p1", HealthyCount = count, DistanceToMain = ratio * 100f, MainSeeingRange = 100f,
        SpottingRatio = spotting,
    };

    private void Sees(PartyIntelSnapshot snapshot) => _adapter.Read(Arg.Any<MobileParty>()).Returns(snapshot);

    private static Type NameplateType => AccessTools.TypeByName(NameplateTypeName);

    private static object NewPlate(bool visible = true, bool withParty = true, string count = "123", string extra = "5 wounded",
        string name = "Lord Aldric")
    {
        var plate = FormatterServices.GetUninitializedObject(NameplateType);
        Set(plate, "_countBind", count);
        Set(plate, "_extraInfoTextBind", extra);
        Set(plate, "_fullNameBind", name);
        Set(plate, "_isVisibleOnMapBind", visible);
        if (withParty)
            NameplateType.GetProperty("Party", AnyInstance)!.SetValue(plate, FormatterServices.GetUninitializedObject(typeof(MobileParty)));
        return plate;
    }

    private static void Set(object plate, string field, object? value) => AccessTools.Field(NameplateType, field).SetValue(plate, value);

    private static string? Get(object plate, string field) => (string?)AccessTools.Field(NameplateType, field).GetValue(plate);

    private static Type Hook(string name) => typeof(HiddenHostsModule).Assembly.GetType("TAOM.Features.HiddenHosts.Hooks." + name)!;

    private static VanillaBinds RunPrefix(object? plate)
    {
        var args = new object?[] { plate, null };
        Hook(nameof(PartyNameplateVM_RefreshBinding_HiddenHosts_Patch)).GetMethod("Prefix")!.Invoke(null, args);
        return (VanillaBinds)args[1]!;
    }

    private static void RunFinalizer(object? plate, VanillaBinds state) =>
        Hook(nameof(PartyNameplateVM_RefreshBinding_HiddenHosts_Patch)).GetMethod("Finalizer")!.Invoke(null, new object?[] { plate, state });

    private static bool RunEncyclopediaPrefix(object? visual) =>
        (bool)Hook(nameof(MobilePartyVisual_OnOpenEncyclopedia_HiddenHosts_Patch)).GetMethod("Prefix")!.Invoke(null, new[] { visual })!;

    private static object NewVisual() => FormatterServices.GetUninitializedObject(AccessTools.TypeByName(VisualTypeName));

    // --- RefreshBinding prefix and finalizer

    [TestMethod]
    public void Prefix_FarParty_LaysTheMask_AndTheFinalizerRestoresVanillasFields()
    {
        Sees(Snap(0.7f));
        var plate = NewPlate();

        var state = RunPrefix(plate);

        Assert.IsTrue(state.IsSet);
        Assert.AreEqual("???", Get(plate, "_countBind"));
        Assert.AreEqual(string.Empty, Get(plate, "_extraInfoTextBind"));
        Assert.AreEqual("Lord Aldric", Get(plate, "_fullNameBind"), "the name changes only at the horizon");

        RunFinalizer(plate, state);

        Assert.AreEqual("123", Get(plate, "_countBind"));
        Assert.AreEqual("5 wounded", Get(plate, "_extraInfoTextBind"));
        Assert.AreEqual("Lord Aldric", Get(plate, "_fullNameBind"));
    }

    [TestMethod]
    public void Prefix_HorizonParty_AlsoRenamesThePlate_AndTheFinalizerRestoresIt()
    {
        Sees(Snap(0.95f));
        var plate = NewPlate();

        var state = RunPrefix(plate);

        Assert.AreEqual("...", Get(plate, "_countBind"));
        Assert.IsNotNull(Get(plate, "_fullNameBind"));
        Assert.AreNotEqual("Lord Aldric", Get(plate, "_fullNameBind"));

        RunFinalizer(plate, state);

        Assert.AreEqual("Lord Aldric", Get(plate, "_fullNameBind"));
        Assert.AreEqual("123", Get(plate, "_countBind"));
    }

    [TestMethod]
    public void Prefix_TwoFramesInARow_MasksAgainAfterTheFinalizerRestored()
    {
        Sees(Snap(0.7f));
        var plate = NewPlate();

        RunFinalizer(plate, RunPrefix(plate));
        var second = RunPrefix(plate);

        Assert.AreEqual("???", Get(plate, "_countBind"));
        RunFinalizer(plate, second);
        Assert.AreEqual("123", Get(plate, "_countBind"));
    }

    [TestMethod]
    public void Prefix_CloseParty_LeavesTheFieldsAlone()
    {
        Sees(Snap(0.1f));
        var plate = NewPlate();

        var state = RunPrefix(plate);

        Assert.IsFalse(state.IsSet);
        Assert.AreEqual("123", Get(plate, "_countBind"));
        Assert.AreEqual("5 wounded", Get(plate, "_extraInfoTextBind"));
    }

    [TestMethod]
    public void Prefix_SettingOff_LeavesTheFields_AndNeverReadsTheAdapter()
    {
        _settings.Enabled.Returns(false);
        Sees(Snap(0.7f));
        var plate = NewPlate();

        var state = RunPrefix(plate);

        Assert.IsFalse(state.IsSet);
        Assert.AreEqual("123", Get(plate, "_countBind"));
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void Prefix_PlateNotOnTheMap_IsSkipped()
    {
        Sees(Snap(0.7f));
        var plate = NewPlate(visible: false);

        var state = RunPrefix(plate);

        Assert.IsFalse(state.IsSet);
        Assert.AreEqual("123", Get(plate, "_countBind"));
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void Prefix_PlateWithoutAParty_IsSkipped()
    {
        Sees(Snap(0.7f));

        var state = RunPrefix(NewPlate(withParty: false));

        Assert.IsFalse(state.IsSet);
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void Prefix_NullInstanceOrNoServices_DoesNothing()
    {
        Assert.IsFalse(RunPrefix(null).IsSet);

        HiddenHostsCalls.Initialize(null, null);
        Assert.IsFalse(RunPrefix(NewPlate()).IsSet);
    }

    [TestMethod]
    public void Prefix_TheAdapterThrows_LeavesTheFields_AndLogsOnce()
    {
        _adapter.Read(Arg.Any<MobileParty>()).Throws(new InvalidOperationException("no campaign"));
        var plate = NewPlate();

        var first = RunPrefix(plate);
        RunPrefix(plate);

        Assert.IsFalse(first.IsSet);
        Assert.AreEqual("123", Get(plate, "_countBind"));
        Assert.AreEqual("5 wounded", Get(plate, "_extraInfoTextBind"));
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Prefix_AfterTheFaultBudgetIsSpent_DoesNoWorkForTheRestOfTheProcess()
    {
        _adapter.Read(Arg.Any<MobileParty>()).Throws(new InvalidOperationException("no campaign"));
        var plate = NewPlate();
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            RunPrefix(plate);
        Assert.IsTrue(HiddenHostsCalls.Stopped);
        _adapter.ClearReceivedCalls();
        Sees(Snap(0.7f));

        var state = RunPrefix(plate);

        Assert.IsFalse(state.IsSet);
        Assert.AreEqual("123", Get(plate, "_countBind"));
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void Finalizer_NullInstanceAndUnsetState_DoesNotThrow() => RunFinalizer(null, default);

    [TestMethod]
    public void Finalizer_UnsetState_LeavesTheFieldsAsTheyAre()
    {
        var plate = NewPlate();

        RunFinalizer(plate, default);

        Assert.AreEqual("123", Get(plate, "_countBind"));
        Assert.AreEqual("5 wounded", Get(plate, "_extraInfoTextBind"));
        Assert.AreEqual("Lord Aldric", Get(plate, "_fullNameBind"));
    }

    // --- OnOpenEncyclopedia prefix

    [TestMethod]
    public void EncyclopediaPrefix_PartyAtTheHorizon_SkipsTheOriginal()
    {
        Sees(Snap(0.95f));

        Assert.IsFalse(RunEncyclopediaPrefix(NewVisual()));
    }

    [TestMethod]
    public void EncyclopediaPrefix_PartyNotAtTheHorizon_RunsVanilla()
    {
        Sees(Snap(0.5f));

        Assert.IsTrue(RunEncyclopediaPrefix(NewVisual()));
    }

    [TestMethod]
    public void EncyclopediaPrefix_SettingOff_RunsVanilla_AndNeverReadsTheAdapter()
    {
        _settings.Enabled.Returns(false);
        Sees(Snap(0.95f));

        Assert.IsTrue(RunEncyclopediaPrefix(NewVisual()));
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void EncyclopediaPrefix_TheAdapterThrows_RunsVanilla_AndLogsOnce()
    {
        _adapter.Read(Arg.Any<MobileParty>()).Throws(new InvalidOperationException("no campaign"));

        Assert.IsTrue(RunEncyclopediaPrefix(NewVisual()));
        Assert.IsTrue(RunEncyclopediaPrefix(NewVisual()));
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void EncyclopediaPrefix_AfterTheFaultBudgetIsSpent_RunsVanillaWithoutReadingTheAdapter()
    {
        _adapter.Read(Arg.Any<MobileParty>()).Throws(new InvalidOperationException("no campaign"));
        for (int i = 0; i < HiddenHostsCalls.FaultBudget; i++)
            RunEncyclopediaPrefix(NewVisual());
        _adapter.ClearReceivedCalls();
        Sees(Snap(0.95f));

        Assert.IsTrue(RunEncyclopediaPrefix(NewVisual()));
        _adapter.DidNotReceiveWithAnyArgs().Read(null);
    }

    [TestMethod]
    public void EncyclopediaPrefix_NullInstanceOrNoServices_RunsVanilla()
    {
        Assert.IsTrue(RunEncyclopediaPrefix(null));

        HiddenHostsCalls.Initialize(null, null);
        Assert.IsTrue(RunEncyclopediaPrefix(NewVisual()));
    }

    // --- The engine

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void NameplateFields_ResolveOnTheInstalledEngine()
    {
        foreach (var field in new[] { "_countBind", "_extraInfoTextBind", "_fullNameBind" })
            Assert.AreEqual(typeof(string), AccessTools.Field(NameplateType, field)?.FieldType, field);
        Assert.AreEqual(typeof(bool), AccessTools.Field(NameplateType, "_isVisibleOnMapBind")?.FieldType, "_isVisibleOnMapBind");
    }

    // The real patch classes applied to the real methods, so Harmony builds the replacements and the JIT compiles them (a
    // member a replacement cannot resolve would surface here), then takes them off again. The methods are never run.
    [DataTestMethod]
    [DataRow(NameplateTypeName, "RefreshBinding", "PartyNameplateVM_RefreshBinding_HiddenHosts_Patch", true)]
    [DataRow(VisualTypeName, "OnOpenEncyclopedia", "MobilePartyVisual_OnOpenEncyclopedia_HiddenHosts_Patch", false)]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchClass_AppliedToTheInstalledEngine_BuildsAReplacementTheJitAccepts(string typeName, string method, string hook,
        bool hasFinalizer)
    {
        string id = "taom.tests.hiddenhosts.jit." + hook;
        var harmony = new Harmony(id);
        var target = AccessTools.Method(AccessTools.TypeByName(typeName), method, Type.EmptyTypes);
        try
        {
            harmony.CreateClassProcessor(Hook(hook)).Patch();

            var info = Harmony.GetPatchInfo(target);
            Assert.IsTrue(info.Prefixes.Any(p => p.owner == id), "the prefix is attached");
            Assert.AreEqual(hasFinalizer, info.Finalizers.Any(p => p.owner == id), "the finalizer");
            RuntimeHelpers.PrepareMethod(target.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
