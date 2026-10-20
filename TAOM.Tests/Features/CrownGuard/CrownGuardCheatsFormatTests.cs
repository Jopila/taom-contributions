using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrownGuard.Cheats;

namespace TAOM.Tests.Features.CrownGuard;

/// <summary>
/// Pins the `taom.play_coronation` report. The kingdom resolution and the scene are checked in game, and the console
/// shape is covered for every command by <c>ConsoleCommandBindingTests</c>.
/// </summary>
[TestClass]
public class CrownGuardCheatsFormatTests
{
    [TestMethod]
    public void FormatShown_TroopDrawn_NamesKingdomCultureAndGuardTroop()
    {
        var report = CrownGuardCheats.FormatShown("Rohan", "vlandia", "rohan_edoras_golden_hall_kings_own_rider");

        StringAssert.Contains(report, "for Rohan:");
        StringAssert.Contains(report, "culture vlandia");
        StringAssert.Contains(report, "guard troop rohan_edoras_golden_hall_kings_own_rider");
    }

    [TestMethod]
    public void FormatShown_NoTroop_SaysNone()
    {
        var report = CrownGuardCheats.FormatShown("Rohan", "vlandia", null);

        StringAssert.Contains(report, "guard troop none");
    }
}
