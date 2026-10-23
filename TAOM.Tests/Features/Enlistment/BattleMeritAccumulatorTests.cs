using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class BattleMeritAccumulatorTests
{
    [TestMethod]
    public void ResetForNewSession_DropsThePendingSample()
    {
        var sut = new BattleMeritAccumulator();
        sut.Submit(new MeritSample { Kills = 4, KilledCharacterIds = new[] { "orc" } });

        sut.ResetForNewSession();

        Assert.IsNull(sut.Consume());
    }

    [TestMethod]
    public void NewSample_HasNoKilledCharacters()
    {
        Assert.AreEqual(0, new MeritSample().KilledCharacterIds.Count);
    }
}
