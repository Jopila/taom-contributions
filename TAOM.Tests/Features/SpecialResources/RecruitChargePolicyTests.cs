using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>Who pays a recruit_cost: singleplayer and a hosting player's own recruits charge MainHero, a guest's charge the scoped leader.</summary>
[TestClass]
public class RecruitChargePolicyTests
{
    [TestMethod]
    public void ChoosePayer_Singleplayer_ChargesMainHero()
        => Assert.AreEqual("main", RecruitChargePolicy.ChoosePayer<string>(true, false, false, null, "main"));

    [TestMethod]
    public void ChoosePayer_NotAuthority_ChargesNobody()
        => Assert.IsNull(RecruitChargePolicy.ChoosePayer<string>(false, false, false, null, "main"));

    [TestMethod]
    public void ChoosePayer_NotAuthorityEvenWithScope_ChargesNobody()
        => Assert.IsNull(RecruitChargePolicy.ChoosePayer<string>(false, false, true, "guest", "main"));

    [TestMethod]
    public void ChoosePayer_ScopedGuestParty_ChargesTheScopedLeader()
        => Assert.AreEqual("guest", RecruitChargePolicy.ChoosePayer<string>(true, false, true, "guest", "main"));

    [TestMethod]
    public void ChoosePayer_DedicatedWithScope_ChargesTheScopedLeader()
        => Assert.AreEqual("guest", RecruitChargePolicy.ChoosePayer<string>(true, true, true, "guest", "placeholder"));

    [TestMethod]
    public void ChoosePayer_DedicatedWithoutScope_ChargesNobody()
        => Assert.IsNull(RecruitChargePolicy.ChoosePayer<string>(true, true, false, null, "placeholder"));

    [TestMethod]
    public void ChoosePayer_ScopedPartyGone_ChargesNobodyNotTheHost()
        => Assert.IsNull(RecruitChargePolicy.ChoosePayer<string>(true, false, true, null, "main"));
}
