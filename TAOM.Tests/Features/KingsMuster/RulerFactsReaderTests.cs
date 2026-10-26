using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.KingsMuster;

namespace TAOM.Tests.Features.KingsMuster;

[TestClass]
public class RulerFactsReaderTests
{
    [TestMethod]
    [TestCategory("RequiresGame")]
    public void ANullParty_IsNotARuler_AndNotLedByTheMainHero()
    {
        Assert.IsFalse(RulerFactsReader.IsKingdomRuler(null));
        Assert.IsFalse(RulerFactsReader.IsLedByMainHero(null));
    }
}
