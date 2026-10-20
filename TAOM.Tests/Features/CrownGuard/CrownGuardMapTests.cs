using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrownGuard;

namespace TAOM.Tests.Features.CrownGuard;

[TestClass]
public class CrownGuardMapTests
{
    private CrownGuardMap _sut = null!;

    [TestInitialize]
    public void Setup()
        => _sut = new CrownGuardMap(new Dictionary<string, string> { ["gondor"] = "gondor_mt_fountain_guard" });

    [TestMethod]
    public void GuardTroopId_MappedCulture_ReturnsTheTroop()
        => Assert.AreEqual("gondor_mt_fountain_guard", _sut.GuardTroopId("gondor"));

    [TestMethod]
    public void GuardTroopId_UnmappedCulture_ReturnsNull()
        => Assert.IsNull(_sut.GuardTroopId("sturgia"));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void GuardTroopId_NullOrEmpty_ReturnsNull(string? cultureId)
        => Assert.IsNull(_sut.GuardTroopId(cultureId));

    [DataTestMethod]
    [DataRow("Gondor")]
    [DataRow("  GONDOR ")]
    public void GuardTroopId_CaseAndWhitespace_AreNormalized(string cultureId)
        => Assert.AreEqual("gondor_mt_fountain_guard", _sut.GuardTroopId(cultureId));
}
