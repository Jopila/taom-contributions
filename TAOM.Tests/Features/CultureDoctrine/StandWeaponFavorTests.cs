using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CultureDoctrine;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Features.CultureDoctrine.Hooks;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// The polearm favour of a standing wall: the configured multiplier for every AI soldier off the
/// player's team in a formation holding a Dismount order (the whole wall of a dismount:true team,
/// not the riders alone); 1 (no effect) for everyone else, and for a favour that is not a finite
/// number above 1. The human check lives in <c>Apply</c>, not in the predicate tested here.
/// </summary>
[TestClass]
public class StandWeaponFavorTests
{
    [TestMethod]
    public void Multiplier_AiHumanInADismountedFormation_IsTheFavour()
        => Assert.AreEqual(2f, StandWeaponFavor.Multiplier(onPlayerTeam: false, dismountOrdered: true, favor: 2f));

    [TestMethod]
    public void Multiplier_PlayerTeam_IsOne()
        => Assert.AreEqual(1f, StandWeaponFavor.Multiplier(onPlayerTeam: true, dismountOrdered: true, favor: 2f));

    [TestMethod]
    public void Multiplier_FormationNotOrderedToDismount_IsOne()
        => Assert.AreEqual(1f, StandWeaponFavor.Multiplier(onPlayerTeam: false, dismountOrdered: false, favor: 2f));

    [TestMethod]
    public void Multiplier_FavourOne_IsOne()
        => Assert.AreEqual(1f, StandWeaponFavor.Multiplier(onPlayerTeam: false, dismountOrdered: true, favor: 1f));

    [TestMethod]
    public void Multiplier_NaNOrInfiniteFavour_IsOne()
    {
        Assert.AreEqual(1f, StandWeaponFavor.Multiplier(false, true, float.NaN));
        Assert.AreEqual(1f, StandWeaponFavor.Multiplier(false, true, float.PositiveInfinity));
    }

    [TestMethod]
    public void Multiplier_FavourBelowOne_IsOne()
        => Assert.AreEqual(1f, StandWeaponFavor.Multiplier(false, true, 0.5f));
}

/// <summary>The favour the stat models read: the catalog's value only while the horse-stand switch
/// (which already includes the doctrine switch) is on, 1 otherwise.</summary>
[TestClass]
public class StandPolearmFavorServiceTests
{
    private static CultureAggressionService Build(bool horseStandOn, float favor)
    {
        var catalog = new DoctrineCatalog(enabled: true, DoctrineCatalog.VanillaDefault(), new Doctrine[0],
            EngagementTunables.Default, new HorseStandTunables(0.5f, 1f, favor));
        var config = Substitute.For<ICultureDoctrineConfigProvider>();
        config.GetCatalog().Returns(catalog);
        var settings = Substitute.For<ICultureDoctrineSettingsProvider>();
        settings.IsHorseStandEnabled.Returns(horseStandOn);
        return new CultureAggressionService(config, settings);
    }

    [TestMethod]
    public void StandPolearmFavor_SwitchOn_IsTheCatalogsValue()
        => Assert.AreEqual(3f, Build(horseStandOn: true, favor: 3f).StandPolearmFavor);

    [TestMethod]
    public void StandPolearmFavor_SwitchOff_IsOne()
        => Assert.AreEqual(1f, Build(horseStandOn: false, favor: 3f).StandPolearmFavor);
}
