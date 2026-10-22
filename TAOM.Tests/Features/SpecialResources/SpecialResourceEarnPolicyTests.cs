using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.SpecialResources;

[TestClass]
public class SpecialResourceEarnPolicyTests
{
    // --- The regression this policy exists for ---

    [TestMethod]
    public void IsPlayerVictory_PlayerFightsInsideAnAiLedArmyAndThatSideWins_Earns()
    {
        // THE single-player bug the multiplayer report surfaced without noticing. The old gate asked
        // whether the player IS the winning side's LeaderParty.LeaderHero; join any lord's army and
        // you are not, so every victory paid zero. Participation is the correct question.
        Assert.IsTrue(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Attacker, winningSide: BattleSideEnum.Attacker));
    }

    [TestMethod]
    public void IsPlayerVictory_PlayerLedTheWinningSide_StillEarns()
    {
        // The case that already worked must keep working — the fix widens the gate, never narrows it.
        Assert.IsTrue(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Defender, winningSide: BattleSideEnum.Defender));
    }

    [TestMethod]
    public void IsPlayerVictory_PlayerOnTheLosingSide_DoesNotEarn()
    {
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Defender, winningSide: BattleSideEnum.Attacker));
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Attacker, winningSide: BattleSideEnum.Defender));
    }

    [TestMethod]
    public void IsPlayerVictory_BattleUnresolved_DoesNotEarn()
    {
        // Under co-op a client routinely observes state=None because the server is authoritative and
        // never re-broadcasts BattleState. Treating that as a win would pay out for lost battles.
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.Attacker, winningSide: BattleSideEnum.None));
    }

    [TestMethod]
    public void IsPlayerVictory_PlayerOnNoSide_DoesNotEarn()
    {
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.None, winningSide: BattleSideEnum.Attacker));
    }

    [TestMethod]
    public void IsPlayerVictory_NeitherSideResolved_DoesNotEarn()
    {
        Assert.IsFalse(SpecialResourceEarnPolicy.IsPlayerVictory(
            playerSide: BattleSideEnum.None, winningSide: BattleSideEnum.None));
    }

    // --- #770: a raid or extortion event pays the battle credit only when a field party was beaten ---

    private static readonly MapEvent.BattleTypes[] KnownBattleTypes =
    {
        MapEvent.BattleTypes.None,
        MapEvent.BattleTypes.FieldBattle,
        MapEvent.BattleTypes.Raid,
        MapEvent.BattleTypes.IsForcingVolunteers,
        MapEvent.BattleTypes.IsForcingSupplies,
        MapEvent.BattleTypes.Siege,
        MapEvent.BattleTypes.Hideout,
        MapEvent.BattleTypes.SallyOut,
        MapEvent.BattleTypes.SiegeOutside,
        MapEvent.BattleTypes.BlockadeBattle,
        MapEvent.BattleTypes.BlockadeSallyOutBattle,
        MapEvent.BattleTypes.SiegeAmbush,
    };

    [TestMethod]
    public void PaysBattleCredit_EveryEventTypeWithBothFlagValues_MatchesTheExpectation()
    {
        foreach (MapEvent.BattleTypes type in System.Enum.GetValues(typeof(MapEvent.BattleTypes)))
        {
            var villageHostile = type == MapEvent.BattleTypes.Raid
                || type == MapEvent.BattleTypes.IsForcingSupplies
                || type == MapEvent.BattleTypes.IsForcingVolunteers;

            Assert.AreEqual(true, SpecialResourceEarnPolicy.PaysBattleCredit(type, beatAFieldParty: true),
                $"{type} with a beaten field party");
            Assert.AreEqual(!villageHostile, SpecialResourceEarnPolicy.PaysBattleCredit(type, beatAFieldParty: false),
                $"{type} without a beaten field party");
        }
    }

    [TestMethod]
    public void PaysBattleCredit_EnumStillHoldsExactlyTheValuesTheTestKnows()
    {
        // A new engine event type must be a conscious decision here, not a silent default.
        var actual = (MapEvent.BattleTypes[])System.Enum.GetValues(typeof(MapEvent.BattleTypes));
        CollectionAssert.AreEquivalent(KnownBattleTypes, actual);
    }

    // --- Who may earn on this process (#967 C1) ---

    private static readonly PlayerControl[] AllControls =
        { PlayerControl.NotPlayer, PlayerControl.Player, PlayerControl.Unknown };

    // Written out independently of the policy's branching. The control already answers "who": without a
    // session the registry query reduces to the local MainHero, in a session it is the player registry.
    private static bool ExpectedMayEarn(bool session, bool authority, bool dedicated, PlayerControl control)
    {
        // A client never writes: the server's balance is the only one.
        if (!authority) return false;
        // Only a hero the control query calls a player earns.
        if (control != PlayerControl.Player) return false;
        // A dedicated server's single-player-style MainHero is the idle world-gen hero, but in a session
        // its registered guests are real players.
        return session || !dedicated;
    }

    [TestMethod]
    public void MayEarn_EveryCombination_MatchesTheTable()
    {
        var bools = new[] { false, true };
        foreach (var session in bools)
        foreach (var authority in bools)
        foreach (var dedicated in bools)
        foreach (var control in AllControls)
        {
            Assert.AreEqual(
                ExpectedMayEarn(session, authority, dedicated, control),
                SpecialResourceEarnPolicy.MayEarn(session, authority, dedicated, control),
                $"session={session} authority={authority} dedicated={dedicated} control={control}");
        }
    }

    [TestMethod]
    public void MayEarn_SinglePlayerMainHero_Earns()
        => Assert.IsTrue(SpecialResourceEarnPolicy.MayEarn(false, true, false, PlayerControl.Player));

    [TestMethod]
    public void MayEarn_DedicatedServerWithoutSession_RefusesMainHero()
        => Assert.IsFalse(SpecialResourceEarnPolicy.MayEarn(false, true, true, PlayerControl.Player));

    [TestMethod]
    public void MayEarn_SessionRegisteredPlayer_EarnsEvenOnADedicatedServer()
        => Assert.IsTrue(SpecialResourceEarnPolicy.MayEarn(true, true, true, PlayerControl.Player));

    [TestMethod]
    public void MayEarn_SessionServerHero_EarnsNothing()
        => Assert.IsFalse(SpecialResourceEarnPolicy.MayEarn(true, true, false, PlayerControl.NotPlayer));

    [TestMethod]
    public void MayEarn_SessionUnknownControl_EarnsNothing()
        => Assert.IsFalse(SpecialResourceEarnPolicy.MayEarn(true, true, false, PlayerControl.Unknown));
}
