using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8, E02: <c>ReadSight</c> runs every campaign frame of a hunt. It must find the hunter with the locator search
/// around the player, never the linear <c>CampaignObjectManager.Find</c> walk over every party, and its reading must
/// be a value type so the frame allocates nothing. The adapter is engine-bound, so its IL is the testable surface.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HunterPartyAdapterIlTests
{
    private static MethodBase[] CallsOf(string methodName)
    {
        var method = typeof(HunterPartyAdapter).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
        return IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()).ToArray();
    }

    [TestMethod]
    public void ReadSight_SearchesAroundThePlayer_NeverTheLinearFind()
    {
        var calls = CallsOf(nameof(HunterPartyAdapter.ReadSight));

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(MobileParty) && m.Name == nameof(MobileParty.StartFindingLocatablesAroundPosition)),
            "ReadSight must search the locator grid around the player");
        Assert.IsFalse(calls.Any(m => m.DeclaringType == typeof(CampaignObjectManager) && m.Name.StartsWith("Find")),
            "ReadSight must not walk every party with CampaignObjectManager.Find each frame");
        Assert.IsFalse(calls.Any(m => m.DeclaringType == typeof(HunterPartyAdapter) && m.Name == "FindParty"),
            "ReadSight must not call FindParty (the linear walk) each frame");
    }

    [TestMethod]
    public void HuntSightReading_IsAValueType_SoAFrameAllocatesNothing() =>
        Assert.IsTrue(typeof(HuntSightReading).IsValueType);
}
