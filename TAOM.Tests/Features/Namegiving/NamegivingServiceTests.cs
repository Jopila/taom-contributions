using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.Namegiving;

namespace TAOM.Tests.Features.Namegiving;

[TestClass]
public class NamegivingServiceTests
{
    private IFiefNamingAdapter _fiefs = null!;
    private IAlignmentService _alignment = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IModLogger _logger = null!;
    private TaomSettings _settings = null!;
    private NamegivingService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _fiefs = Substitute.For<IFiefNamingAdapter>();
        _alignment = Substitute.For<IAlignmentService>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _settings = new TaomSettings();
        _fiefs.IsRuler.Returns(true);
        _fiefs.Influence.Returns(10000f);
        _fiefs.MapActionBlockedReason().Returns((string?)null);
        _fiefs.AllNames().Returns(new[]
        {
            new NamedSettlement("town_A", "Aldburg"),
            new NamedSettlement("town_B", "Bree"),
            new NamedSettlement("village_B1", "Staddle"),
        });
        Build();
    }

    private void Build() =>
        _sut = new NamegivingService(_fiefs, _alignment, _coop, _dedicated, new NamegivingSettingsProvider(_settings), _logger);

    private static FiefNameSnapshot Snap(string id, FiefKind kind, string name, string custom = "", bool realm = true,
        string faction = "kingdom_x", string culture = "culture_x") =>
        new(id, kind, name, custom, realm, faction, culture);

    private void Has(FiefNameSnapshot snap) => _fiefs.Snapshot(snap.Id).Returns(snap);

    private static string Long(int n) => new string('a', n);

    // --- ValidateName ----------------------------------------------------------------------------------------------

    private static readonly string[] None = Array.Empty<string>();

    [TestMethod]
    public void ValidateName_AValidName_Passes()
        => Assert.AreEqual(NameProblem.None, NamegivingService.ValidateName("Barad Nûrn", "Aldburg", None));

    [TestMethod]
    public void ValidateName_AccentedLetterDigitsAndPunctuation_Pass()
        => Assert.AreEqual(NameProblem.None, NamegivingService.ValidateName("St. Mary's Ford 2-b", "Aldburg", None));

    [TestMethod]
    public void ValidateName_Null_IsLength()
        => Assert.AreEqual(NameProblem.Length, NamegivingService.ValidateName(null, "Aldburg", None));

    [TestMethod]
    public void ValidateName_Empty_IsLength()
        => Assert.AreEqual(NameProblem.Length, NamegivingService.ValidateName("", "Aldburg", None));

    [TestMethod]
    public void ValidateName_FiftyCharacters_Pass()
        => Assert.AreEqual(NameProblem.None, NamegivingService.ValidateName(Long(50), "Aldburg", None));

    [TestMethod]
    public void ValidateName_FiftyOneCharacters_IsLength()
        => Assert.AreEqual(NameProblem.Length, NamegivingService.ValidateName(Long(51), "Aldburg", None));

    [TestMethod]
    public void ValidateName_CurlyBrace_IsBrackets()
    {
        Assert.AreEqual(NameProblem.Brackets, NamegivingService.ValidateName("Ald{burg", "Bree", None));
        Assert.AreEqual(NameProblem.Brackets, NamegivingService.ValidateName("Ald}burg", "Bree", None));
        Assert.AreEqual(NameProblem.Brackets, NamegivingService.ValidateName("{=x}Ald", "Bree", None));
    }

    [TestMethod]
    public void ValidateName_SquareBracket_IsBrackets()
    {
        Assert.AreEqual(NameProblem.Brackets, NamegivingService.ValidateName("Ald[burg", "Bree", None));
        Assert.AreEqual(NameProblem.Brackets, NamegivingService.ValidateName("Ald]burg", "Bree", None));
    }

    [TestMethod]
    public void ValidateName_Tab_IsCharacters()
        => Assert.AreEqual(NameProblem.Characters, NamegivingService.ValidateName("Ald\tburg", "Bree", None));

    [TestMethod]
    public void ValidateName_Newline_IsCharacters()
        => Assert.AreEqual(NameProblem.Characters, NamegivingService.ValidateName("Ald\nburg", "Bree", None));

    [TestMethod]
    public void ValidateName_NoBreakSpace_IsCharacters()
        => Assert.AreEqual(NameProblem.Characters, NamegivingService.ValidateName("Ald\u00A0burg", "Bree", None));

    [TestMethod]
    public void ValidateName_AngleBracketAndSymbol_AreCharacters()
    {
        Assert.AreEqual(NameProblem.Characters, NamegivingService.ValidateName("Ald<burg", "Bree", None));
        Assert.AreEqual(NameProblem.Characters, NamegivingService.ValidateName("Ald$burg", "Bree", None));
    }

    [TestMethod]
    public void ValidateName_Emoji_IsCharacters()
        => Assert.AreEqual(NameProblem.Characters, NamegivingService.ValidateName("Ald😀", "Bree", None));

    [TestMethod]
    public void ValidateName_LeadingOrTrailingSpace_IsEdge()
    {
        Assert.AreEqual(NameProblem.Edge, NamegivingService.ValidateName(" Aldburg", "Bree", None));
        Assert.AreEqual(NameProblem.Edge, NamegivingService.ValidateName("Aldburg ", "Bree", None));
        Assert.AreEqual(NameProblem.Edge, NamegivingService.ValidateName(" ", "Bree", None));
    }

    [TestMethod]
    public void ValidateName_TwoSpacesInARow_IsDouble()
        => Assert.AreEqual(NameProblem.Double, NamegivingService.ValidateName("Ald  burg", "Bree", None));

    [TestMethod]
    public void ValidateName_TheCurrentName_IsSame()
        => Assert.AreEqual(NameProblem.Same, NamegivingService.ValidateName("Aldburg", "Aldburg", None));

    [TestMethod]
    public void ValidateName_TheCurrentNameInOtherCase_IsAllowed()
        => Assert.AreEqual(NameProblem.None, NamegivingService.ValidateName("ALDBURG", "Aldburg", None));

    [TestMethod]
    public void ValidateName_AnotherPlacesName_IsTaken()
        => Assert.AreEqual(NameProblem.Taken, NamegivingService.ValidateName("Bree", "Aldburg", new[] { "Bree" }));

    [TestMethod]
    public void ValidateName_AnotherPlacesNameInOtherCase_IsTaken()
    {
        Assert.AreEqual(NameProblem.Taken, NamegivingService.ValidateName("bREE", "Aldburg", new[] { "Bree" }));
        Assert.AreEqual(NameProblem.Taken, NamegivingService.ValidateName("BRÉE", "Aldburg", new[] { "Brée" }));
    }

    [TestMethod]
    public void ValidateName_RulesRunInOrder_LengthFirst()
        => Assert.AreEqual(NameProblem.Length, NamegivingService.ValidateName(Long(51) + "{", "Bree", None));

    // --- Costs ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CostFor_EachKind_IsTheConstant()
    {
        Assert.AreEqual(2000, NamegivingService.CostFor(FiefKind.Town));
        Assert.AreEqual(1000, NamegivingService.CostFor(FiefKind.Castle));
        Assert.AreEqual(500, NamegivingService.CostFor(FiefKind.Village));
    }

    // --- Gates ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Check_EveryGateOpen_IsNone()
        => Assert.AreEqual(RenameBlock.None, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));

    [TestMethod]
    public void Check_SwitchOff_IsDisabled()
    {
        _settings.EnableNamegiving = false;
        Assert.AreEqual(RenameBlock.Disabled, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_CoopSessionActive_IsNotSinglePlayer()
    {
        _coop.IsSessionActive.Returns(true);
        Assert.AreEqual(RenameBlock.NotSinglePlayer, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_CoopModToDefer_IsNotSinglePlayer()
    {
        _coop.ShouldDeferToHost.Returns(true);
        Assert.AreEqual(RenameBlock.NotSinglePlayer, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_DedicatedServer_IsNotSinglePlayer()
    {
        _dedicated.IsDedicatedServer.Returns(true);
        Assert.AreEqual(RenameBlock.NotSinglePlayer, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_NoAuthority_IsNotSinglePlayer()
    {
        _coop.IsAuthority.Returns(false);
        Assert.AreEqual(RenameBlock.NotSinglePlayer, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_NotTheRuler_IsNotRuler()
    {
        _fiefs.IsRuler.Returns(false);
        Assert.AreEqual(RenameBlock.NotRuler, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_MapActionBlocked_IsMapAction()
    {
        _fiefs.MapActionBlockedReason().Returns("This action is disabled while in a mission");
        Assert.AreEqual(RenameBlock.MapAction, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_FiefOutsideTheRealm_IsNotRealm()
        => Assert.AreEqual(RenameBlock.NotRealm, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg", realm: false)));

    [TestMethod]
    public void Check_ShortOfInfluence_IsShortOfInfluence()
    {
        _fiefs.Influence.Returns(1999f);
        Assert.AreEqual(RenameBlock.ShortOfInfluence, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_InfluenceExactlyTheCost_Passes()
    {
        _fiefs.Influence.Returns(2000f);
        Assert.AreEqual(RenameBlock.None, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_NaNInfluence_IsShortOfInfluence()
    {
        _fiefs.Influence.Returns(float.NaN);
        Assert.AreEqual(RenameBlock.ShortOfInfluence, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
    }

    [TestMethod]
    public void Check_TheCostFollowsTheKind()
    {
        _fiefs.Influence.Returns(1000f);
        Assert.AreEqual(RenameBlock.ShortOfInfluence, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")));
        Assert.AreEqual(RenameBlock.None, _sut.Check(Snap("castle_A", FiefKind.Castle, "Caer")));
        _fiefs.Influence.Returns(499f);
        Assert.AreEqual(RenameBlock.ShortOfInfluence, _sut.Check(Snap("v", FiefKind.Village, "Staddle")));
    }

    [TestMethod]
    public void Check_TheGatesRunInOrder_SwitchBeforeEverythingElse()
    {
        _settings.EnableNamegiving = false;
        _fiefs.IsRuler.Returns(false);
        _fiefs.Influence.Returns(0f);
        Assert.AreEqual(RenameBlock.Disabled, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg", realm: false)));
    }

    [TestMethod]
    public void Check_FiefOutsideTheRealmWhileTheMapActionIsBlocked_ReportsTheMapAction()
    {
        _fiefs.MapActionBlockedReason().Returns("In a mission");
        Assert.AreEqual(RenameBlock.MapAction, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg", realm: false)),
            "the target-independent gates run first: switch, single-player, ruler, map action; then the realm, then the influence");
    }

    [TestMethod]
    public void MayWriteWorld_IsTheSinglePlayerPredicate()
    {
        Assert.IsTrue(_sut.MayWriteWorld);
        _coop.IsSessionActive.Returns(true);
        Assert.IsFalse(_sut.MayWriteWorld);
    }

    // --- Button and picker ------------------------------------------------------------------------------------------

    [TestMethod]
    public void ButtonState_NoFiefSelected_IsHidden()
        => Assert.IsFalse(_sut.ButtonState(null).Visible);

    [TestMethod]
    public void ButtonState_SwitchOff_IsHidden()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _settings.EnableNamegiving = false;
        Assert.IsFalse(_sut.ButtonState("town_A").Visible);
    }

    [TestMethod]
    public void ButtonState_NotTheRuler_IsHidden()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.IsRuler.Returns(false);
        Assert.IsFalse(_sut.ButtonState("town_A").Visible);
    }

    [TestMethod]
    public void ButtonState_CoopSession_IsHidden()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _coop.IsSessionActive.Returns(true);
        Assert.IsFalse(_sut.ButtonState("town_A").Visible);
    }

    [TestMethod]
    public void ButtonState_AnOpenFief_IsVisibleEnabledWithTheFiefsCost()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        var state = _sut.ButtonState("town_A");
        Assert.IsTrue(state.Visible);
        Assert.IsTrue(state.Enabled);
        Assert.AreEqual(2000, state.Cost);
        Assert.AreEqual(RenameBlock.None, state.Block);
    }

    [TestMethod]
    public void ButtonState_MapActionBlocked_IsVisibleDisabledWithTheEnginesReason()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.MapActionBlockedReason().Returns("In a mission");
        var state = _sut.ButtonState("town_A");
        Assert.IsTrue(state.Visible);
        Assert.IsFalse(state.Enabled);
        Assert.AreEqual(RenameBlock.MapAction, state.Block);
        Assert.AreEqual("In a mission", state.Detail);
    }

    [TestMethod]
    public void ButtonState_ShortOfEveryPrice_IsDisabled_ButAVillageThatIsAffordableEnablesIt()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.VillageIds("town_A").Returns(new[] { "village_A1" });
        Has(Snap("village_A1", FiefKind.Village, "Staddle"));
        _fiefs.Influence.Returns(600f);

        var state = _sut.ButtonState("town_A");

        Assert.IsTrue(state.Enabled, "the village costs 500");
        _fiefs.Influence.Returns(100f);
        state = _sut.ButtonState("town_A");
        Assert.IsFalse(state.Enabled);
        Assert.AreEqual(RenameBlock.ShortOfInfluence, state.Block);
    }

    [TestMethod]
    public void ButtonState_UnknownFief_IsHidden()
    {
        _fiefs.Snapshot("nope").Returns((FiefNameSnapshot?)null);
        Assert.IsFalse(_sut.ButtonState("nope").Visible);
    }

    [TestMethod]
    public void Targets_ListTheFiefThenItsVillages_EachWithItsOwnCostAndReason()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.VillageIds("town_A").Returns(new[] { "village_A1", "village_A2" });
        Has(Snap("village_A1", FiefKind.Village, "Staddle"));
        Has(Snap("village_A2", FiefKind.Village, "Archet", realm: false));
        _fiefs.Influence.Returns(1000f);

        var targets = _sut.Targets("town_A");

        CollectionAssert.AreEqual(new[] { "town_A", "village_A1", "village_A2" }, targets.Select(t => t.Id).ToArray());
        CollectionAssert.AreEqual(new[] { 2000, 500, 500 }, targets.Select(t => t.Cost).ToArray());
        CollectionAssert.AreEqual(new[] { RenameBlock.ShortOfInfluence, RenameBlock.None, RenameBlock.NotRealm }, targets.Select(t => t.Block).ToArray());
        Assert.AreEqual("Staddle", targets[1].Name);
    }

    [TestMethod]
    public void Targets_AVillageWithoutASnapshot_IsLeftOut()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.VillageIds("town_A").Returns(new[] { "ghost" });
        _fiefs.Snapshot("ghost").Returns((FiefNameSnapshot?)null);

        CollectionAssert.AreEqual(new[] { "town_A" }, _sut.Targets("town_A").Select(t => t.Id).ToArray());
    }

    [TestMethod]
    public void Targets_MapActionBlocked_CarriesTheReasonOnEveryRow()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.VillageIds("town_A").Returns(new[] { "village_A1" });
        Has(Snap("village_A1", FiefKind.Village, "Staddle"));
        _fiefs.MapActionBlockedReason().Returns("In a mission");

        var targets = _sut.Targets("town_A");

        Assert.AreEqual(2, targets.Count);
        foreach (var row in targets)
        {
            Assert.AreEqual(RenameBlock.MapAction, row.Block, row.Id);
            Assert.AreEqual("In a mission", row.Detail, row.Id);
        }
    }

    [TestMethod]
    public void Targets_ReadTheMapActionGateOnce()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.VillageIds("town_A").Returns(new[] { "village_A1", "village_A2" });
        Has(Snap("village_A1", FiefKind.Village, "Staddle"));
        Has(Snap("village_A2", FiefKind.Village, "Archet"));
        _fiefs.ClearReceivedCalls();

        _sut.Targets("town_A");

        _fiefs.Received(1).MapActionBlockedReason();
    }

    [TestMethod]
    public void ButtonState_ReadsTheMapActionGateOnce()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.VillageIds("town_A").Returns(new[] { "village_A1", "village_A2" });
        Has(Snap("village_A1", FiefKind.Village, "Staddle"));
        Has(Snap("village_A2", FiefKind.Village, "Archet"));
        _fiefs.ClearReceivedCalls();

        _sut.ButtonState("town_A");

        _fiefs.Received(1).MapActionBlockedReason();
    }

    // --- Other names ------------------------------------------------------------------------------------------------

    [TestMethod]
    public void OtherNames_ExcludeTheTargetItself_AndKeepDuplicates()
    {
        _fiefs.AllNames().Returns(new[]
        {
            new NamedSettlement("town_A", "Aldburg"),
            new NamedSettlement("town_B", "Bree"),
            new NamedSettlement("hideout_1", "Bree"),
        });

        CollectionAssert.AreEquivalent(new[] { "Bree", "Bree" }, _sut.OtherNames("town_A").ToArray());
        CollectionAssert.AreEquivalent(new[] { "Aldburg", "Bree" }, _sut.OtherNames("town_B").ToArray());
    }

    // --- Apply ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Apply_AValidName_WritesItWithTheCost()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));

        var result = _sut.Apply("town_A", "Meduseld");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(RenameBlock.None, result.Block);
        Assert.AreEqual(NameProblem.None, result.Problem);
        _fiefs.Received(1).ApplyPlayerName("town_A", "Meduseld", 2000);
    }

    [TestMethod]
    public void TheResultAndTheRow_CarryNothingTheCallersIgnore()
    {
        // The prompts already hold the old name (the picked row) and the new one (what was typed), and the price (the row).
        CollectionAssert.AreEquivalent(new[] { "Success", "Block", "Problem", "Detail" },
            typeof(RenameResult).GetProperties().Select(p => p.Name).ToArray());
        Assert.IsNull(typeof(RenameTarget).GetProperty("Kind"));
    }

    [TestMethod]
    public void Apply_ACastleAndAVillage_ChargeTheirOwnPrice()
    {
        Has(Snap("castle_A", FiefKind.Castle, "Caer"));
        Has(Snap("village_A1", FiefKind.Village, "Staddle"));

        _sut.Apply("castle_A", "Caer Dhu");
        _sut.Apply("village_A1", "Little Staddle");

        _fiefs.Received(1).ApplyPlayerName("castle_A", "Caer Dhu", 1000);
        _fiefs.Received(1).ApplyPlayerName("village_A1", "Little Staddle", 500);
    }

    [TestMethod]
    public void Apply_TheFiefLeftTheRealmSinceThePrompt_WritesNothing()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg", realm: false));

        var result = _sut.Apply("town_A", "Meduseld");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(RenameBlock.NotRealm, result.Block);
        _fiefs.DidNotReceiveWithAnyArgs().ApplyPlayerName(default!, default!, default);
    }

    [TestMethod]
    public void Apply_InfluenceSpentSinceThePrompt_WritesNothing()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.Influence.Returns(5f);

        var result = _sut.Apply("town_A", "Meduseld");

        Assert.AreEqual(RenameBlock.ShortOfInfluence, result.Block);
        _fiefs.DidNotReceiveWithAnyArgs().ApplyPlayerName(default!, default!, default);
    }

    [TestMethod]
    public void Apply_NoLongerTheRuler_WritesNothing()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.IsRuler.Returns(false);

        Assert.AreEqual(RenameBlock.NotRuler, _sut.Apply("town_A", "Meduseld").Block);
        _fiefs.DidNotReceiveWithAnyArgs().ApplyPlayerName(default!, default!, default);
    }

    [TestMethod]
    public void Apply_ACoopSessionStartedSinceThePrompt_WritesNothing()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        Assert.AreEqual(RenameBlock.None, _sut.Check(Snap("town_A", FiefKind.Town, "Aldburg")), "the prompt opened in single-player");
        _coop.IsSessionActive.Returns(true);

        var result = _sut.Apply("town_A", "Meduseld");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(RenameBlock.NotSinglePlayer, result.Block);
        _fiefs.DidNotReceiveWithAnyArgs().ApplyPlayerName(default!, default!, default);
    }

    [DataTestMethod]
    [DataRow(true, false, false, true, DisplayName = "a live co-op session")]
    [DataRow(false, true, false, true, DisplayName = "a co-op mod to defer to")]
    [DataRow(false, false, true, true, DisplayName = "a dedicated server")]
    [DataRow(false, false, false, false, DisplayName = "a peer without authority")]
    public void Apply_EveryNonSinglePlayerState_WritesNothing(bool session, bool defer, bool dedicated, bool authority)
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        InState(session, defer, dedicated, authority);

        var result = _sut.Apply("town_A", "Meduseld");

        Assert.AreEqual(RenameBlock.NotSinglePlayer, result.Block);
        _fiefs.DidNotReceiveWithAnyArgs().ApplyPlayerName(default!, default!, default);
    }

    [TestMethod]
    public void Apply_MapActionBlockedSinceThePrompt_CarriesTheReason()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));
        _fiefs.MapActionBlockedReason().Returns("In a mission");

        var result = _sut.Apply("town_A", "Meduseld");

        Assert.AreEqual(RenameBlock.MapAction, result.Block);
        Assert.AreEqual("In a mission", result.Detail);
        _fiefs.DidNotReceiveWithAnyArgs().ApplyPlayerName(default!, default!, default);
    }

    [TestMethod]
    public void Apply_UnknownTarget_IsNotRealm()
    {
        _fiefs.Snapshot("nope").Returns((FiefNameSnapshot?)null);

        Assert.AreEqual(RenameBlock.NotRealm, _sut.Apply("nope", "Meduseld").Block);
    }

    [TestMethod]
    public void Apply_ABadName_WritesNothing_AndNamesTheProblem()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));

        var result = _sut.Apply("town_A", "Med{useld");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(NameProblem.Brackets, result.Problem);
        _fiefs.DidNotReceiveWithAnyArgs().ApplyPlayerName(default!, default!, default);
    }

    [TestMethod]
    public void Apply_ANameAnotherPlaceTookSinceThePrompt_IsTaken()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));

        Assert.AreEqual(NameProblem.Taken, _sut.Apply("town_A", "bree").Problem);
    }

    [TestMethod]
    public void Apply_ItsOwnCurrentNameInTheNamesList_IsSame_NotTaken()
    {
        Has(Snap("town_A", FiefKind.Town, "Aldburg"));

        Assert.AreEqual(NameProblem.Same, _sut.Apply("town_A", "Aldburg").Problem);
    }

    // --- Classify and DecideLore ------------------------------------------------------------------------------------

    [TestMethod]
    public void Classify_NullOrEmpty_IsNone()
    {
        Assert.AreEqual(NameOrigin.None, NamegivingService.Classify(null));
        Assert.AreEqual(NameOrigin.None, NamegivingService.Classify(""));
    }

    [TestMethod]
    public void Classify_ALoreKey_IsLore()
        => Assert.AreEqual(NameOrigin.Lore, NamegivingService.Classify("{=taom_namegiving_lore_minas_ithil}Minas Ithil"));

    [TestMethod]
    public void Classify_APlayerName_IsPlayer()
        => Assert.AreEqual(NameOrigin.Player, NamegivingService.Classify("{=!}Meduseld"));

    [TestMethod]
    public void Classify_AnythingElse_IsPlayer_SoItIsNeverTouched()
    {
        Assert.AreEqual(NameOrigin.Player, NamegivingService.Classify("Some Other Name"));
        Assert.AreEqual(NameOrigin.Player, NamegivingService.Classify("{=other_mod_key}Foreign"));
        Assert.AreEqual(NameOrigin.Player, NamegivingService.Classify("{=!}{=taom_namegiving_lore_x}Fake"));
    }

    private static readonly LoreRow Row = new("town_ES2", "{=taom_namegiving_lore_minas_ithil}Minas Ithil", FactionSide.Free);

    [TestMethod]
    public void DecideLore_OwnSideHoldsIt_NoCustomName_Sets()
        => Assert.AreEqual(LoreAction.Set, NamegivingService.DecideLore(Row, FactionSide.Free, NameOrigin.None, true));

    [TestMethod]
    public void DecideLore_OwnSideHoldsIt_AlreadyLore_Keeps()
        => Assert.AreEqual(LoreAction.Keep, NamegivingService.DecideLore(Row, FactionSide.Free, NameOrigin.Lore, true));

    [TestMethod]
    public void DecideLore_OwnSideHoldsIt_PlayerName_Keeps()
        => Assert.AreEqual(LoreAction.Keep, NamegivingService.DecideLore(Row, FactionSide.Free, NameOrigin.Player, true));

    [TestMethod]
    public void DecideLore_OtherSideHoldsIt_LoreName_Restores()
        => Assert.AreEqual(LoreAction.Restore, NamegivingService.DecideLore(Row, FactionSide.Evil, NameOrigin.Lore, true));

    [TestMethod]
    public void DecideLore_OtherSideHoldsIt_NoCustomName_Keeps()
        => Assert.AreEqual(LoreAction.Keep, NamegivingService.DecideLore(Row, FactionSide.Evil, NameOrigin.None, true));

    [TestMethod]
    public void DecideLore_OtherSideHoldsIt_PlayerName_Keeps()
        => Assert.AreEqual(LoreAction.Keep, NamegivingService.DecideLore(Row, FactionSide.Evil, NameOrigin.Player, true));

    [TestMethod]
    public void DecideLore_Neutral_KeepsWhateverIsThere()
    {
        foreach (var origin in new[] { NameOrigin.None, NameOrigin.Lore, NameOrigin.Player })
            Assert.AreEqual(LoreAction.Keep, NamegivingService.DecideLore(Row, FactionSide.Neutral, origin, true), origin.ToString());
    }

    [TestMethod]
    public void DecideLore_SwitchOff_RestoresALoreName_KeepsEverythingElse()
    {
        foreach (var side in new[] { FactionSide.Free, FactionSide.Evil, FactionSide.Neutral })
            foreach (var origin in new[] { NameOrigin.None, NameOrigin.Lore, NameOrigin.Player })
            {
                var expected = origin == NameOrigin.Lore ? LoreAction.Restore : LoreAction.Keep;
                Assert.AreEqual(expected, NamegivingService.DecideLore(Row, side, origin, false), side + " " + origin);
            }
    }

    [TestMethod]
    public void DecideLore_EvilRow_FollowsTheEvilSide()
    {
        var dike = new LoreRow("town_AN2", "{=taom_namegiving_lore_deadmens_dike}Deadmen's Dike", FactionSide.Evil);

        Assert.AreEqual(LoreAction.Set, NamegivingService.DecideLore(dike, FactionSide.Evil, NameOrigin.None, true));
        Assert.AreEqual(LoreAction.Restore, NamegivingService.DecideLore(dike, FactionSide.Free, NameOrigin.Lore, true));
    }

    // --- ReconcileLore ----------------------------------------------------------------------------------------------

    private void Side(string faction, string culture, FactionSide side) => _alignment.ResolveSide(faction, culture).Returns(side);

    [TestMethod]
    public void ReconcileLore_FreeSideHoldsMinasMorgul_SetsMinasIthil()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Morgul", faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);

        _sut.ReconcileLore("town_ES2");

        _fiefs.Received(1).ApplyLore("town_ES2", "{=taom_namegiving_lore_minas_ithil}Minas Ithil");
    }

    [TestMethod]
    public void ReconcileLore_EvilSideRetakesIt_Restores()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Ithil", custom: "{=taom_namegiving_lore_minas_ithil}Minas Ithil", faction: "mordor_k", culture: "mordor"));
        Side("mordor_k", "mordor", FactionSide.Evil);

        _sut.ReconcileLore("town_ES2");

        _fiefs.Received(1).Restore("town_ES2");
        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
    }

    [TestMethod]
    public void ReconcileLore_APlayerNamedFief_IsNeverTouched()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Meduseld", custom: "{=!}Meduseld", faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);

        _sut.ReconcileLore("town_ES2");
        Side("gondor_k", "gondor", FactionSide.Evil);
        _sut.ReconcileLore("town_ES2");

        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
        _fiefs.DidNotReceiveWithAnyArgs().Restore(default!);
    }

    [TestMethod]
    public void ReconcileLore_NeutralHolder_ChangesNothing()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Morgul", faction: "bandits", culture: "none"));
        Side("bandits", "none", FactionSide.Neutral);

        _sut.ReconcileLore("town_ES2");

        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
        _fiefs.DidNotReceiveWithAnyArgs().Restore(default!);
    }

    [TestMethod]
    public void ReconcileLore_ASettlementWithoutALoreRow_ReadsNothing()
    {
        _sut.ReconcileLore("town_A");

        _fiefs.DidNotReceiveWithAnyArgs().Snapshot(default!);
        _ = _coop.DidNotReceive().IsSessionActive;
        _ = _coop.DidNotReceive().ShouldDeferToHost;
        _ = _coop.DidNotReceive().IsAuthority;
        _ = _dedicated.DidNotReceive().IsDedicatedServer;
    }

    [TestMethod]
    public void ReconcileLore_TheNameIsInUse_IsSkippedAndLogged()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Morgul", faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);
        _fiefs.AllNames().Returns(new[]
        {
            new NamedSettlement("town_ES2", "Minas Morgul"),
            new NamedSettlement("town_A", "minas ithil"),
        });

        _sut.ReconcileLore("town_ES2");

        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Minas Ithil") && m.Contains("town_ES2")));
    }

    private const string MinasIthilLore = "{=taom_namegiving_lore_minas_ithil}Minas Ithil";

    [TestMethod]
    public void ReconcileLore_SwitchOff_WithNoCustomName_ChangesNothing()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Morgul", faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);
        _settings.EnableNamegiving = false;

        _sut.ReconcileLore("town_ES2");

        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
        _fiefs.DidNotReceiveWithAnyArgs().Restore(default!);
    }

    [TestMethod]
    public void ReconcileLore_SwitchOff_RestoresALoreName()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Ithil", custom: MinasIthilLore, faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);
        _settings.EnableNamegiving = false;

        _sut.ReconcileLore("town_ES2");

        _fiefs.Received(1).Restore("town_ES2");
        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
    }

    [TestMethod]
    public void ReconcileLore_SwitchOff_LeavesAPlayerName()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Meduseld", custom: "{=!}Meduseld", faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);
        _settings.EnableNamegiving = false;

        _sut.ReconcileLore("town_ES2");

        _fiefs.DidNotReceiveWithAnyArgs().Restore(default!);
        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
    }

    [TestMethod]
    public void ReconcileLore_SwitchOffInACoopSession_RestoresNothing()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Ithil", custom: MinasIthilLore, faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);
        _settings.EnableNamegiving = false;
        _coop.IsSessionActive.Returns(true);

        _sut.ReconcileLore("town_ES2");

        _fiefs.DidNotReceiveWithAnyArgs().Restore(default!);
    }

    [DataTestMethod]
    [DataRow(true, false, false, true, DisplayName = "a live co-op session")]
    [DataRow(false, true, false, true, DisplayName = "a co-op mod to defer to")]
    [DataRow(false, false, true, true, DisplayName = "a dedicated server")]
    [DataRow(false, false, false, false, DisplayName = "a peer without authority")]
    public void ReconcileLore_EveryNonSinglePlayerState_WritesNothing(bool session, bool defer, bool dedicated, bool authority)
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Morgul", faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);
        InState(session, defer, dedicated, authority);

        _sut.ReconcileLore("town_ES2");

        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
        _fiefs.DidNotReceiveWithAnyArgs().Restore(default!);
    }

    private void InState(bool session, bool defer, bool dedicated, bool authority)
    {
        _coop.IsSessionActive.Returns(session);
        _coop.ShouldDeferToHost.Returns(defer);
        _dedicated.IsDedicatedServer.Returns(dedicated);
        _coop.IsAuthority.Returns(authority);
    }

    [TestMethod]
    public void ReconcileLore_CoopSession_ChangesNothing()
    {
        Has(Snap("town_ES2", FiefKind.Town, "Minas Morgul", faction: "gondor_k", culture: "gondor"));
        Side("gondor_k", "gondor", FactionSide.Free);
        _coop.IsSessionActive.Returns(true);

        _sut.ReconcileLore("town_ES2");

        _fiefs.DidNotReceiveWithAnyArgs().ApplyLore(default!, default!);
    }

    [TestMethod]
    public void ReconcileLore_AdapterThrows_IsSwallowedAndLogged()
    {
        _fiefs.Snapshot("town_ES2").Returns(_ => throw new InvalidOperationException("boom"));

        _sut.ReconcileLore("town_ES2");

        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("town_ES2") && m.Contains("boom")));
    }

    [TestMethod]
    public void ReconcileAllLore_OneFiefThrows_TheOthersStillRun_AndOneLineIsLogged()
    {
        _fiefs.Snapshot("town_ES2").Returns(_ => throw new InvalidOperationException("boom"));
        Has(Snap("town_DG1", FiefKind.Town, "Dol Guldur", faction: "free_k", culture: "free"));
        Has(Snap("town_AN2", FiefKind.Town, "Fornost Erain", faction: "free_k", culture: "free"));
        Has(Snap("town_isengard", FiefKind.Town, "Orthanc", faction: "free_k", culture: "free"));
        Side("free_k", "free", FactionSide.Free);

        _sut.ReconcileAllLore();

        _fiefs.Received(1).ApplyLore("town_DG1", "{=taom_namegiving_lore_amon_lanc}Amon Lanc");
        _fiefs.Received(1).ApplyLore("town_isengard", "{=taom_namegiving_lore_treegarth}Treegarth of Orthanc");
        _fiefs.DidNotReceive().ApplyLore("town_AN2", Arg.Any<string>());
        _logger.Received(1).LogError(Arg.Any<string>());
    }
}
