using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TaomBuildings;

/// <summary>
/// A source pin: the adapter needs the engine's town and castle lists, which a no-game test cannot build.
/// </summary>
[TestClass]
public class TownBuildingsAdapterSourceTests
{
    [TestMethod]
    public void Fortifications_WalksTownAllFiefs_NotTownAllTowns()
    {
        // Town.AllTowns holds towns only; castles live in a second list that Town.AllFiefs appends (v1.5.5 Town.cs).
        var source = RepoPaths.ReadSource("Main/Adapters/TownBuildingsAdapter.cs", stripComments: true);

        StringAssert.Contains(source, "Town.AllFiefs");
        Assert.IsFalse(source.Contains("Town.AllTowns"), "a castle would never be seeded");
    }
}
