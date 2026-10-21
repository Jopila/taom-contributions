using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.ObjectSystem;
using TAOM.Adapters;

namespace TAOM.Tests.Adapters;

/// <summary>The one roster-removal routine both desertion paths share.</summary>
[TestClass]
[TestCategory("RequiresGame")] // a real TroopRoster and a real object manager
public class TroopRosterRemovalTests
{
    private MBObjectManager? _objects;

    [TestInitialize]
    public void Setup()
    {
        // MBObjectManager.Instance is process-wide and null in every other test; Cleanup puts it back.
        _objects = MBObjectManager.Init();
        _objects.RegisterType<CharacterObject>("NPCCharacter", "NPCCharacters", 43u);
    }

    [TestCleanup]
    public void Cleanup() => _objects?.Destroy();

    [TestMethod]
    public void RemoveUpTo_CapsAtTheStackAndReturnsTheCount()
    {
        var character = _objects!.RegisterObject(new CharacterObject { StringId = "gondor_soldier" });
        var roster = TroopRoster.CreateDummyTroopRoster();
        roster.AddToCounts(character, 5);

        Assert.AreEqual(2, TroopRosterRemoval.RemoveUpTo(roster, "gondor_soldier", 2));
        Assert.AreEqual(3, roster.GetTroopCount(character));

        Assert.AreEqual(3, TroopRosterRemoval.RemoveUpTo(roster, "gondor_soldier", 9));
        Assert.AreEqual(0, roster.GetTroopCount(character));
    }

    [TestMethod]
    public void RemoveUpTo_UnknownTroopOrNothingToRemove_ReturnsZero()
    {
        var character = _objects!.RegisterObject(new CharacterObject { StringId = "gondor_soldier" });
        var roster = TroopRoster.CreateDummyTroopRoster();
        roster.AddToCounts(character, 5);

        Assert.AreEqual(0, TroopRosterRemoval.RemoveUpTo(roster, "no_such_troop", 3));
        Assert.AreEqual(0, TroopRosterRemoval.RemoveUpTo(roster, "gondor_soldier", 0));
        Assert.AreEqual(5, roster.GetTroopCount(character));
    }
}
