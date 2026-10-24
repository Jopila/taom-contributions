using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.KeepWardens;

namespace TAOM.Tests.Features.KeepWardens;

[TestClass]
public class CastlePatrolSettingsProviderTests
{
    [TestMethod]
    public void NullMcm_DefaultsOn()
    {
        Assert.IsTrue(CastlePatrolSettingsProvider.From(null));
        Assert.IsTrue(new CastlePatrolSettingsProvider().Enabled);
        Assert.IsTrue(new TaomSettings().EnableCastlePatrols);
        Assert.IsFalse(CastlePatrolSettingsProvider.From(new TaomSettings { EnableCastlePatrols = false }));
    }

    private static object Attribute(PropertyInfo property, string typeName) =>
        property.GetCustomAttributes(inherit: false).Single(a => a.GetType().Name == typeName);

    private static object Read(object attribute, string name) => attribute.GetType().GetProperty(name)!.GetValue(attribute)!;

    [TestMethod]
    public void TheProperty_SitsInItsFiefManagementGroup_AppliesWithoutARestart_AndDefaultsOn()
    {
        // MCM reads these attributes by name; MCMv5 is runtime-only here, so the test does too.
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.EnableCastlePatrols))!;

        Assert.AreEqual("Fief Management/Keep Wardens", Read(Attribute(property, "SettingPropertyGroupAttribute"), "GroupName"));
        var value = Attribute(property, "SettingPropertyBoolAttribute");
        Assert.AreEqual(false, Read(value, "RequireRestart"), "the patrol queue reads the switch at each visit");
        Assert.IsTrue(new TaomSettings().EnableCastlePatrols);
        StringAssert.StartsWith((string)Read(value, "DisplayName"), "{=taom_keep_wardens_label}");
        StringAssert.StartsWith((string)Read(value, "HintText"), "{=taom_keep_wardens_hint}");
    }
}
