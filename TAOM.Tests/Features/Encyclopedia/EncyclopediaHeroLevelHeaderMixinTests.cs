using System.Reflection;
using System.Text.RegularExpressions;
using Bannerlord.UIExtenderEx.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TAOM.Features.Encyclopedia.UI;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.Encyclopedia;

/// <summary>Wiring pins for the "Lvl: N, Skills:" mixin on the hero page (#907). The text on screen is the in-game check.</summary>
[TestClass]
public class EncyclopediaHeroLevelHeaderMixinTests
{
    private const string Source = "Client/Features/Encyclopedia/UI/EncyclopediaHeroLevelHeaderMixin.cs";

    [TestMethod]
    public void TheMixin_HooksRefresh_WhichRefreshValuesCallsAfterSettingSkillsText()
    {
        Assert.AreEqual("Refresh", typeof(EncyclopediaHeroLevelHeaderMixin).GetCustomAttribute<ViewModelMixinAttribute>()!.RefreshMethodName);
        Assert.IsNotNull(typeof(EncyclopediaHeroPageVM).GetMethod("Refresh", System.Type.EmptyTypes));
    }

    [TestMethod]
    public void SkillsText_IsAPublicStringWithAPublicSetter()
    {
        var property = typeof(EncyclopediaHeroPageVM).GetProperty("SkillsText", BindingFlags.Public | BindingFlags.Instance)!;
        Assert.IsNotNull(property);
        Assert.AreEqual(typeof(string), property.PropertyType);
        Assert.IsNotNull(property.SetMethod);
        Assert.IsTrue(property.SetMethod!.IsPublic);
    }

    [TestMethod]
    public void TheConstructor_DoesNotCallOnRefresh()
    {
        var source = ReadSource(Source, stripComments: true);
        var constructor = Regex.Match(source, @"public \w+\([^)]*\)\s*:\s*base\([^)]*\)\s*\{(?<body>.*?)\n    \}", RegexOptions.Singleline);
        Assert.IsTrue(constructor.Success);
        StringAssert.DoesNotMatch(constructor.Groups["body"].Value, new Regex(@"OnRefresh\s*\("));
    }

    [TestMethod]
    public void TheMixin_UsesTheCoreRule_TheEnginesComingOfAge_AndTheHiddenFlag()
    {
        var source = ReadSource(Source, stripComments: true);
        StringAssert.Contains(source, "HeroLevelHeader.ShouldShowLevel(");
        StringAssert.Contains(source, "AgeModel.HeroComesOfAge");
        StringAssert.Contains(source, "IsInformationHidden");
        StringAssert.Contains(source, "{=taom_ency_hero_level_skills}Lvl: {LEVEL}, Skills:");
    }

    [TestMethod]
    public void OnRefresh_DoesNotResolveTheLogger_ItIsCapturedByTheConstructor()
    {
        var source = ReadSource(Source, stripComments: true);
        var refresh = source.Substring(source.IndexOf("public override void OnRefresh()"));
        StringAssert.DoesNotMatch(refresh, new Regex(@"IoC\.Resolve"));
        var constructor = Regex.Match(source, @"public \w+\([^)]*\)\s*:\s*base\([^)]*\)\s*\{(?<body>.*?)
    \}", RegexOptions.Singleline);
        StringAssert.Contains(constructor.Groups["body"].Value, "IoC.Resolve<IModLogger>()");
    }
}
