using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Features.TelcharsMark;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TelcharsMark;

/// <summary>
/// UpgradeStatsReader runs engine getters (EquipmentElement.Item, the GetModified* family), which the reference
/// assemblies stub with <c>throw null</c>, so these need the game assemblies.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class UpgradeStatsReaderTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void Read_EmptyElement_ReturnsNull()
    {
        RequireGame();
        Assert.IsNull(UpgradeStatsReader.Read(default(EquipmentElement)));
    }

    [TestMethod]
    public void Read_HorseTypedItemWithoutHorseComponent_ReturnsEmptyInsteadOfThrowing()
    {
        RequireGame();
        // The engine's mount getters dereference HorseComponent unguarded; one such row used to stop the rescore.
        var item = new ItemObject("taom_test_horse_without_component") { Type = ItemObject.ItemTypeEnum.Horse };

        var stats = UpgradeStatsReader.Read(new EquipmentElement(item));

        Assert.IsNotNull(stats);
        Assert.AreEqual(UpgradeItemKind.None, stats!.Kind);
    }
}
