using MCM.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CrashReport.Collectors;

namespace TAOM.Tests.Features.CrashReport;

[TestClass]
public class McmSettingsCollectorTests
{
    [TestMethod]
    public void Dropdown_RendersTheSelectedValue_NotTheTypeName()
    {
        var dropdown = new Dropdown<string>(new[] { "Never", "Taken-over lords only", "Always" }, 2);

        Assert.AreEqual("Always", McmSettingsCollector.StringifyValue(dropdown));
    }

    [TestMethod]
    public void Dropdown_WithAnOutOfRangeSelection_DoesNotThrow()
    {
        var dropdown = new Dropdown<string>(new[] { "A", "B" }, 0) { SelectedIndex = 5 };

        StringAssert.StartsWith(McmSettingsCollector.StringifyValue(dropdown), "(");
    }
}
