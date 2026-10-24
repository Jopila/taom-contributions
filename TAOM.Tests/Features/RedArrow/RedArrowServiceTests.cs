using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.RedArrow;

namespace TAOM.Tests.Features.RedArrow;

/// <summary>
/// The Red Arrow decision matrix (#873). The service is pure: snapshots in, verdict out. A null verdict means "use vanilla's
/// answer"; the checks reproduce DefaultArmyManagementCalculationModel.CheckPartyEligibility branches 2 to 12 in vanilla's
/// order (v1.5.5 :405-479), and the map-action check is CampaignUIHelper.GetMapScreenActionIsEnabledWithReason (:2440-2497)
/// with the two siege blocks loosened for the player's own besieged settlement.
/// </summary>
[TestClass]
public class RedArrowServiceTests
{
    private const float Min = 0.4f;
    private const float Max = 100f;

    private static readonly ArmyCallReason[] VanillaOrder =
    {
        ArmyCallReason.NoParty, ArmyCallReason.RulersParty, ArmyCallReason.InAnotherArmy, ArmyCallReason.InThisArmy,
        ArmyCallReason.Fighting, ArmyCallReason.TooSmall, ArmyCallReason.Disbanding, ArmyCallReason.NoShips,
        ArmyCallReason.AtSea, ArmyCallReason.LostAtSea, ArmyCallReason.TooFar,
    };

    private static readonly MapActionReason[] MapOrder =
    {
        MapActionReason.Prisoner, MapActionReason.RaftState, MapActionReason.FerryState, MapActionReason.InMission,
        MapActionReason.Encounter, MapActionReason.Raid, MapActionReason.Siege, MapActionReason.MapEvent,
    };

    private IRedArrowSettingsProvider _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private RedArrowService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IRedArrowSettingsProvider>();
        _settings.Enabled.Returns(true);
        _coop = Substitute.For<ICoopSessionProvider>();
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _sut = new RedArrowService(_settings, _coop, _dedicated);
    }

    private static PlayerSiegeState Besieging => new PlayerSiegeState(isBesieger: true, leadsArmy: true);

    // A candidate that fails exactly the listed checks and passes the rest.
    private static ArmyCallCandidate Candidate(IEnumerable<ArmyCallReason> failing)
    {
        var fail = new HashSet<ArmyCallReason>(failing);
        var atSea = fail.Contains(ArmyCallReason.NoShips) || fail.Contains(ArmyCallReason.AtSea);
        return new ArmyCallCandidate(
            hasParty: !fail.Contains(ArmyCallReason.NoParty),
            isFactionLeaderParty: fail.Contains(ArmyCallReason.RulersParty),
            army: fail.Contains(ArmyCallReason.InAnotherArmy) ? CandidateArmy.OtherArmy
                : fail.Contains(ArmyCallReason.InThisArmy) ? CandidateArmy.PlayersArmy
                : CandidateArmy.None,
            isFightingOrBesieged: fail.Contains(ArmyCallReason.Fighting),
            sizeScore: fail.Contains(ArmyCallReason.TooSmall) ? Min : 0.9f,
            minSizeScore: Min,
            isDisbanding: fail.Contains(ArmyCallReason.Disbanding),
            mainPartyAtSea: atSea,
            hasNavalCapability: !fail.Contains(ArmyCallReason.NoShips),
            isInRaftState: fail.Contains(ArmyCallReason.LostAtSea),
            distance: fail.Contains(ArmyCallReason.TooFar) ? Max + 1f : 10f,
            maxDistance: Max);
    }

    private static ArmyCallCandidate Candidate(params ArmyCallReason[] failing) => Candidate((IEnumerable<ArmyCallReason>)failing);

    private static ArmyCallCandidate WithSize(float score, float min = Min) => new ArmyCallCandidate(
        true, false, CandidateArmy.None, false, score, min, false, false, true, false, 10f, Max);

    private static ArmyCallCandidate WithDistance(float distance, float max = Max) => new ArmyCallCandidate(
        true, false, CandidateArmy.None, false, 0.9f, Min, false, false, true, false, distance, max);

    // ---- inactive: every case keeps vanilla (null)

    [TestMethod]
    public void Applies_BesiegingAndLeadingTheArmy_SingleplayerWithTheSwitchOn()
    {
        Assert.IsTrue(_sut.Applies(Besieging));
    }

    [TestMethod]
    public void ToggleOff_UsesBase_ForEveryQuestion()
    {
        _settings.Enabled.Returns(false);

        AssertAllVanilla();
    }

    [TestMethod]
    public void ACoopSession_UsesBase()
    {
        _coop.IsSessionActive.Returns(true);

        AssertAllVanilla();
    }

    [TestMethod]
    public void AClientDeferringToTheHost_UsesBase()
    {
        _coop.ShouldDeferToHost.Returns(true);

        AssertAllVanilla();
    }

    [TestMethod]
    public void ADedicatedServer_UsesBase()
    {
        _dedicated.IsDedicatedServer.Returns(true);

        AssertAllVanilla();
    }

    [TestMethod]
    public void NotBesieging_AllClearOtherwise_UsesBase()
    {
        AssertAllVanilla(new PlayerSiegeState(isBesieger: false, leadsArmy: true));
    }

    [TestMethod]
    public void BesiegingWithoutLeadingTheArmy_UsesBase()
    {
        AssertAllVanilla(new PlayerSiegeState(isBesieger: true, leadsArmy: false));
    }

    [TestMethod]
    public void NoArmyAtAll_UsesBase_SoNoArmyIsCreatedMidSiege()
    {
        AssertAllVanilla(new PlayerSiegeState(isBesieger: false, leadsArmy: false));
        AssertAllVanilla(new PlayerSiegeState(isBesieger: true, leadsArmy: false));
    }

    private void AssertAllVanilla(PlayerSiegeState? state = null)
    {
        var player = state ?? Besieging;
        Assert.IsFalse(_sut.Applies(player));
        Assert.IsNull(_sut.CheckParty(player, Candidate()));
        Assert.IsNull(_sut.CheckParty(player, Candidate(ArmyCallReason.TooFar)));
        Assert.IsNull(_sut.CheckMapAction(player, MapState()));
        Assert.IsNull(_sut.TransferDisabled(player, isMainHero: false));
        Assert.IsNull(_sut.TransferDisabled(player, isMainHero: true));
    }

    // ---- the reproduced party checks

    [TestMethod]
    public void AnEligibleParty_IsEligible()
    {
        Assert.AreEqual(ArmyCallReason.None, _sut.CheckParty(Besieging, Candidate()));
    }

    [DataTestMethod]
    [DataRow(ArmyCallReason.NoParty)]
    [DataRow(ArmyCallReason.RulersParty)]
    [DataRow(ArmyCallReason.InAnotherArmy)]
    [DataRow(ArmyCallReason.InThisArmy)]
    [DataRow(ArmyCallReason.Fighting)]
    [DataRow(ArmyCallReason.TooSmall)]
    [DataRow(ArmyCallReason.Disbanding)]
    [DataRow(ArmyCallReason.NoShips)]
    [DataRow(ArmyCallReason.AtSea)]
    [DataRow(ArmyCallReason.LostAtSea)]
    [DataRow(ArmyCallReason.TooFar)]
    public void EachCheckAlone_GivesItsOwnReason(ArmyCallReason reason)
    {
        Assert.AreEqual(reason, _sut.CheckParty(Besieging, Candidate(reason)));
    }

    [TestMethod]
    public void TheVanillaOrder_IsElevenChecks_InVanillasSequence()
    {
        // The enum is declared in vanilla order after None, so a reordering of the enum is a reordering of the checks.
        var declared = Enum.GetValues(typeof(ArmyCallReason)).Cast<ArmyCallReason>().Where(r => r != ArmyCallReason.None).ToArray();

        CollectionAssert.AreEqual(VanillaOrder, declared);
    }

    [TestMethod]
    public void EveryPairOfFailingChecks_GivesTheEarlierOne()
    {
        for (var i = 0; i < VanillaOrder.Length; i++)
        for (var j = i + 1; j < VanillaOrder.Length; j++)
        {
            // Two army reasons cannot both hold (a party is in one army state), and the two sea reasons are one branch.
            if (IsExclusive(VanillaOrder[i], VanillaOrder[j]))
                continue;

            var verdict = _sut.CheckParty(Besieging, Candidate(VanillaOrder[i], VanillaOrder[j]));

            Assert.AreEqual(VanillaOrder[i], verdict, VanillaOrder[i] + " must beat " + VanillaOrder[j]);
        }
    }

    private static bool IsExclusive(ArmyCallReason a, ArmyCallReason b) =>
        (a == ArmyCallReason.InAnotherArmy && b == ArmyCallReason.InThisArmy)
        || (a == ArmyCallReason.NoShips && b == ArmyCallReason.AtSea);

    [TestMethod]
    public void EveryCheckFailing_GivesTheFirst_AndEachRemovalPromotesTheNext()
    {
        var failing = new List<ArmyCallReason>(VanillaOrder.Where(r => r != ArmyCallReason.InThisArmy && r != ArmyCallReason.AtSea));

        while (failing.Count > 0)
        {
            Assert.AreEqual(failing[0], _sut.CheckParty(Besieging, Candidate(failing)));
            failing.RemoveAt(0);
        }

        Assert.AreEqual(ArmyCallReason.None, _sut.CheckParty(Besieging, Candidate(failing)));
    }

    [TestMethod]
    public void ThePartySize_AtTheLimitIsRefused_JustAboveIsAllowed()
    {
        Assert.AreEqual(ArmyCallReason.TooSmall, _sut.CheckParty(Besieging, WithSize(0.4f)), "vanilla refuses a score <= the ratio");
        Assert.AreEqual(ArmyCallReason.TooSmall, _sut.CheckParty(Besieging, WithSize(0.39f)));
        Assert.AreEqual(ArmyCallReason.None, _sut.CheckParty(Besieging, WithSize(0.401f)));
    }

    [TestMethod]
    public void ThePartySize_UsesTheRatioTheSnapshotCarries()
    {
        Assert.AreEqual(ArmyCallReason.TooSmall, _sut.CheckParty(Besieging, WithSize(0.5f, min: 0.5f)));
        Assert.AreEqual(ArmyCallReason.None, _sut.CheckParty(Besieging, WithSize(0.5f, min: 0.25f)));
    }

    [TestMethod]
    public void ANotANumberSize_FailsTheGate_WhereVanillaWouldPassIt()
    {
        // Vanilla's `score <= ratio` is false for NaN, so it lets the party in. A positive requirement refuses it.
        Assert.AreEqual(ArmyCallReason.TooSmall, _sut.CheckParty(Besieging, WithSize(float.NaN)));
        Assert.AreEqual(ArmyCallReason.TooSmall, _sut.CheckParty(Besieging, WithSize(0.9f, min: float.NaN)));
    }

    [TestMethod]
    public void TheDistance_AtTheLimitIsAllowed_JustBeyondIsRefused()
    {
        Assert.AreEqual(ArmyCallReason.None, _sut.CheckParty(Besieging, WithDistance(Max)), "vanilla refuses only distance > limit");
        Assert.AreEqual(ArmyCallReason.TooFar, _sut.CheckParty(Besieging, WithDistance(Max + 0.01f)));
        Assert.AreEqual(ArmyCallReason.None, _sut.CheckParty(Besieging, WithDistance(0f)));
    }

    [TestMethod]
    public void ANotANumberDistance_FailsTheGate_WhereVanillaWouldPassIt()
    {
        Assert.AreEqual(ArmyCallReason.TooFar, _sut.CheckParty(Besieging, WithDistance(float.NaN)));
        Assert.AreEqual(ArmyCallReason.TooFar, _sut.CheckParty(Besieging, WithDistance(10f, max: float.NaN)));
        Assert.AreEqual(ArmyCallReason.TooFar, _sut.CheckParty(Besieging, WithDistance(float.PositiveInfinity)));
    }

    [TestMethod]
    public void AtSea_NamesTheMissingShips_BeforeTheSea()
    {
        var noShips = new ArmyCallCandidate(true, false, CandidateArmy.None, false, 0.9f, Min, false, true, false, false, float.NaN, Max);
        var ships = new ArmyCallCandidate(true, false, CandidateArmy.None, false, 0.9f, Min, false, true, true, false, float.NaN, Max);

        Assert.AreEqual(ArmyCallReason.NoShips, _sut.CheckParty(Besieging, noShips));
        Assert.AreEqual(ArmyCallReason.AtSea, _sut.CheckParty(Besieging, ships));
    }

    [TestMethod]
    public void AnAllyAlreadyAtTheSiegeOutsideTheArmy_KeepsVanillasBlock()
    {
        // Such a party is in the besieger camp, so its SiegeEvent is set: the "Currently fighting an enemy" branch refuses it.
        Assert.AreEqual(ArmyCallReason.Fighting, _sut.CheckParty(Besieging, Candidate(ArmyCallReason.Fighting)));
    }

    // ---- the map-action check

    private static MapActionState MapState(
        bool prisoner = false, bool raft = false, bool ferry = false, bool mission = false,
        bool encounter = false, bool encounterSettlement = true, bool raid = false,
        bool underSiege = false, bool ownBesieged = false, bool mapEvent = false) =>
        new MapActionState(prisoner, raft, ferry, mission, encounter, encounterSettlement, raid, underSiege, ownBesieged, mapEvent);

    [TestMethod]
    public void TheMapAction_NothingInTheWay_IsAllowed()
    {
        Assert.AreEqual(MapActionReason.None, _sut.CheckMapAction(Besieging, MapState()));
    }

    [DataTestMethod]
    [DataRow(MapActionReason.Prisoner)]
    [DataRow(MapActionReason.RaftState)]
    [DataRow(MapActionReason.FerryState)]
    [DataRow(MapActionReason.InMission)]
    [DataRow(MapActionReason.Encounter)]
    [DataRow(MapActionReason.Raid)]
    [DataRow(MapActionReason.Siege)]
    [DataRow(MapActionReason.MapEvent)]
    public void EachMapBlockAlone_GivesItsOwnReason(MapActionReason reason)
    {
        Assert.AreEqual(reason, _sut.CheckMapAction(Besieging, MapFailing(reason)));
    }

    private static MapActionState MapFailing(params MapActionReason[] failing)
    {
        var f = new HashSet<MapActionReason>(failing);
        var needsEncounter = f.Contains(MapActionReason.Encounter) || f.Contains(MapActionReason.Raid) || f.Contains(MapActionReason.Siege);
        return MapState(
            prisoner: f.Contains(MapActionReason.Prisoner),
            raft: f.Contains(MapActionReason.RaftState),
            ferry: f.Contains(MapActionReason.FerryState),
            mission: f.Contains(MapActionReason.InMission),
            encounter: needsEncounter,
            encounterSettlement: !f.Contains(MapActionReason.Encounter),
            raid: f.Contains(MapActionReason.Raid),
            underSiege: f.Contains(MapActionReason.Siege),
            ownBesieged: false,
            mapEvent: f.Contains(MapActionReason.MapEvent));
    }

    [TestMethod]
    public void EveryPairOfMapBlocks_GivesTheEarlierOne()
    {
        for (var i = 0; i < MapOrder.Length; i++)
        for (var j = i + 1; j < MapOrder.Length; j++)
        {
            // An encounter without a settlement cannot also be at a raided or besieged one.
            if (MapOrder[i] == MapActionReason.Encounter && (MapOrder[j] == MapActionReason.Raid || MapOrder[j] == MapActionReason.Siege))
                continue;

            var verdict = _sut.CheckMapAction(Besieging, MapFailing(MapOrder[i], MapOrder[j]));

            Assert.AreEqual(MapOrder[i], verdict, MapOrder[i] + " must beat " + MapOrder[j]);
        }
    }

    [TestMethod]
    public void AForeignBesiegedSettlement_StillBlocks()
    {
        var foreign = MapState(encounter: true, underSiege: true, ownBesieged: false);

        Assert.AreEqual(MapActionReason.Siege, _sut.CheckMapAction(Besieging, foreign));
    }

    [TestMethod]
    public void ThePlayersOwnBesiegedSettlement_IsAllowed_BothSiegeBlocksLoosened()
    {
        var own = MapState(encounter: true, underSiege: true, ownBesieged: true);

        Assert.AreEqual(MapActionReason.None, _sut.CheckMapAction(Besieging, own));
    }

    [TestMethod]
    public void ThePlayersOwnBesiegedSettlement_DoesNotLiftTheOtherBlocks()
    {
        Assert.AreEqual(MapActionReason.Raid, _sut.CheckMapAction(Besieging, MapState(encounter: true, underSiege: true, ownBesieged: true, raid: true)));
        Assert.AreEqual(MapActionReason.MapEvent, _sut.CheckMapAction(Besieging, MapState(encounter: true, underSiege: true, ownBesieged: true, mapEvent: true)));
        Assert.AreEqual(MapActionReason.Prisoner, _sut.CheckMapAction(Besieging, MapState(prisoner: true, encounter: true, underSiege: true, ownBesieged: true)));
    }

    [TestMethod]
    public void AnEncounterAtAnUnbesiegedSettlement_IsAllowed()
    {
        Assert.AreEqual(MapActionReason.None, _sut.CheckMapAction(Besieging, MapState(encounter: true)));
    }

    [TestMethod]
    public void AnEncounterWithNoSettlement_Blocks_EvenWhenTheFlagsSayOwnSiege()
    {
        var odd = MapState(encounter: true, encounterSettlement: false, underSiege: true, ownBesieged: true);

        Assert.AreEqual(MapActionReason.Encounter, _sut.CheckMapAction(Besieging, odd));
    }

    [TestMethod]
    public void ASettlementFlagWithoutAnEncounter_IsIgnored()
    {
        // Vanilla reads the settlement facts only inside `PlayerEncounter.Current != null`.
        Assert.AreEqual(MapActionReason.None, _sut.CheckMapAction(Besieging, MapState(encounter: false, underSiege: true, raid: true)));
    }

    // ---- transfer

    [TestMethod]
    public void Transfer_IsEnabledForEveryPartyButTheMainHero()
    {
        Assert.AreEqual(false, _sut.TransferDisabled(Besieging, isMainHero: false), "add and remove both work during the siege");
        Assert.AreEqual(true, _sut.TransferDisabled(Besieging, isMainHero: true), "the player's own party stays fixed");
    }
}
