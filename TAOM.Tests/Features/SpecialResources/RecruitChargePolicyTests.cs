using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Who pays a recruit_cost: single-player charges MainHero, a guest's recruit charges the scoped leader,
/// and during a live session an unscoped recruit charges nobody (the authority's MainHero pays nothing).
/// </summary>
[TestClass]
public class RecruitChargePolicyTests
{
    private static string? Payer(bool authority, bool dedicated, bool session, bool scope, string? leader, string? main)
        => RecruitChargePolicy.ChoosePayer<string>(
            isAuthority: authority, isDedicated: dedicated, sessionActive: session,
            hasScope: scope, scopedLeader: leader, mainHero: main);

    [TestMethod]
    public void ChoosePayer_Singleplayer_ChargesMainHero()
        => Assert.AreEqual("main", Payer(true, false, false, false, null, "main"));

    [TestMethod]
    public void ChoosePayer_NotAuthority_ChargesNobody()
        => Assert.IsNull(Payer(false, false, false, false, null, "main"));

    [TestMethod]
    public void ChoosePayer_NotAuthorityEvenWithScope_ChargesNobody()
        => Assert.IsNull(Payer(false, false, false, true, "guest", "main"));

    [TestMethod]
    public void ChoosePayer_ScopedGuestParty_ChargesTheScopedLeader()
        => Assert.AreEqual("guest", Payer(true, false, false, true, "guest", "main"));

    [TestMethod]
    public void ChoosePayer_DedicatedWithScope_ChargesTheScopedLeader()
        => Assert.AreEqual("guest", Payer(true, true, false, true, "guest", "placeholder"));

    [TestMethod]
    public void ChoosePayer_DedicatedWithoutScope_ChargesNobody()
        => Assert.IsNull(Payer(true, true, false, false, null, "placeholder"));

    [TestMethod]
    public void ChoosePayer_ScopedPartyGone_ChargesNobodyNotTheHost()
        => Assert.IsNull(Payer(true, false, false, true, null, "main"));

    [TestMethod]
    public void ChoosePayer_SessionAuthorityWithoutScope_ChargesNobody()
        => Assert.IsNull(Payer(true, false, true, false, null, "main"));

    [TestMethod]
    public void ChoosePayer_SessionAuthorityWithScope_ChargesTheScopedLeader()
        => Assert.AreEqual("guest", Payer(true, false, true, true, "guest", "main"));

    [TestMethod]
    public void ChoosePayer_SessionScopedPartyGone_ChargesNobodyNotTheHost()
        => Assert.IsNull(Payer(true, false, true, true, null, "main"));

    [TestMethod]
    public void ChoosePayer_SessionClient_ChargesNobody()
        => Assert.IsNull(Payer(false, false, true, true, "guest", "main"));
}
