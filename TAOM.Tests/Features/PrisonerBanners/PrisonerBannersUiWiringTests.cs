using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.PrisonerBanners;
using TAOM.Features.PrisonerBanners.UI;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.PrisonerBanners;

/// <summary>
/// Wiring pins for the prisoner banners on the party screen (#902). The banners live in one shared sub-prefab that both TAOM tuple
/// clones include. The look (corner offsets, the gold of the icon) is the in-game check.
/// </summary>
[TestClass]
public class PrisonerBannersUiWiringTests
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private const string Marker = "<!-- TAOM #902 prisoner banners (TaomPrisonerBanners.xml): reapply after a re-sync to vanilla -->";

    private static readonly string[] Tuples = { "PartyTroopTuple.xml", "PartyTroopTupleLeft.xml" };

    private static string PartyPath(string file) => RepoPath("Main", "_Module", "GUI", "Prefabs", "Party", file);

    private static XmlDocument Load(string file)
    {
        var doc = new XmlDocument { PreserveWhitespace = false };
        doc.Load(PartyPath(file));
        return doc;
    }

    private static XmlElement Banners() => (XmlElement)Load("TaomPrisonerBanners.xml").SelectSingleNode("/Prefab/Window/Widget")!;

    [TestMethod]
    public void TheMixin_ReadsOnceInItsConstructor_WithNoRefreshHook()
    {
        var attribute = typeof(PrisonerBannerMixin).GetCustomAttribute<ViewModelMixinAttribute>()!;

        Assert.IsNull(attribute.RefreshMethodName);
        Assert.IsNull(typeof(PrisonerBannerMixin).GetMethod("OnRefresh", Members), "the row is read in the constructor, never refreshed");
        Assert.AreSame(typeof(PartyCharacterVM), typeof(PrisonerBannerMixin).BaseType!.GetGenericArguments().Single());
    }

    [TestMethod]
    public void EveryBannerViewModel_IsNineGrid()
    {
        var source = ReadSource("Client/Features/PrisonerBanners/UI/PrisonerBannerMixin.cs", stripComments: true);
        var all = Regex.Matches(source, @"new BannerImageIdentifierVM\(").Count;
        var nine = Regex.Matches(source, @"new BannerImageIdentifierVM\([^)]*nineGrid: true\)").Count;

        Assert.IsTrue(all >= 1);
        Assert.AreEqual(all, nine, "a banner built without nineGrid breaks the vanilla brush");
    }

    [TestMethod]
    public void EachTuple_IncludesTheSubPrefabOnce_InsideTheTroopBack_AfterTheTypeIcon()
    {
        foreach (var file in Tuples)
        {
            var doc = Load(file);
            var includes = doc.SelectNodes("//TaomPrisonerBanners")!.Cast<XmlElement>().ToList();
            Assert.AreEqual(1, includes.Count, file);

            var children = (XmlElement)includes[0].ParentNode!;
            Assert.AreEqual("Children", children.Name, file);
            var back = (XmlElement)children.ParentNode!;
            Assert.AreEqual("BrushWidget", back.Name, file);
            Assert.AreEqual("Party.TroopBack", back.GetAttribute("Brush"), file);

            var siblings = children.ChildNodes.OfType<XmlElement>().ToList();
            var typeIcon = siblings.FindIndex(e => e.GetAttribute("DataSource") == "{TypeIconData}");
            Assert.IsTrue(typeIcon >= 0 && siblings.IndexOf(includes[0]) > typeIcon, file + ": the banners must draw after the type icon");
            StringAssert.Contains(File.ReadAllText(PartyPath(file)), Marker, file);
            Assert.AreEqual(1, Regex.Matches(File.ReadAllText(PartyPath(file)), Regex.Escape(Marker)).Count, file);
        }
    }

    [TestMethod]
    public void EveryBinding_ResolvesToAMixinOrVanillaMember()
    {
        var (failures, count) = PrefabBindingWalker.Walk(Banners(), typeof(PartyCharacterVM), typeof(PrisonerBannerMixin));

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
        Assert.IsTrue(count >= 5, "the walk found only " + count + " bindings");
    }

    [TestMethod]
    public void TheFiveProperties_AreDeclaredOnTheMixin_AndBoundWithExactTokens()
    {
        var declared = typeof(PrisonerBannerMixin).GetProperties(Members)
            .Where(p => p.CustomAttributes.Any(a => a.AttributeType.Name.StartsWith("DataSourceProperty", StringComparison.Ordinal)))
            .Select(p => p.Name)
            .ToArray();
        CollectionAssert.AreEquivalent(
            new[] { "HasPrisonerClanBanner", "PrisonerClanBanner", "HasPrisonerKingdomBanner", "PrisonerKingdomBanner", "IsPrisonerClanLeader" },
            declared);

        var root = Banners();
        var sources = root.SelectNodes(".//MaskedTextureWidget")!.Cast<XmlElement>().Select(e => e.GetAttribute("DataSource")).ToArray();
        CollectionAssert.AreEquivalent(new[] { "{PrisonerClanBanner}", "{PrisonerKingdomBanner}" }, sources);
        var visible = root.SelectNodes(".//*[@IsVisible]")!.Cast<XmlElement>().Select(e => e.GetAttribute("IsVisible")).ToArray();
        CollectionAssert.AreEquivalent(new[] { "@HasPrisonerClanBanner", "@HasPrisonerKingdomBanner", "@IsPrisonerClanLeader" }, visible);
    }

    [TestMethod]
    public void EachBanner_IsShownByItsOwnFlag()
    {
        foreach (var banner in Banners().SelectNodes(".//MaskedTextureWidget")!.Cast<XmlElement>())
        {
            var name = banner.GetAttribute("DataSource").Trim('{', '}');
            var wrapper = (XmlElement)banner.ParentNode!.ParentNode!;

            Assert.AreEqual("Widget", wrapper.Name);
            Assert.AreEqual("@Has" + name, wrapper.GetAttribute("IsVisible"), name);
        }
    }

    [TestMethod]
    public void TheBlock_UsesTheVanillaBannerBrush_TheNobleSprite_AndIsInert()
    {
        var root = Banners();
        Assert.AreEqual("true", root.GetAttribute("DoNotAcceptEvents"), "the outer wrapper");

        var banners = root.SelectNodes(".//MaskedTextureWidget")!.Cast<XmlElement>().ToList();
        Assert.AreEqual(2, banners.Count);
        foreach (var banner in banners)
        {
            Assert.AreEqual("Flat.Tuple.Banner.Small.Hero", banner.GetAttribute("Brush"));
            Assert.AreEqual("true", banner.GetAttribute("IsDisabled"));
            Assert.AreEqual("true", ((XmlElement)banner.ParentNode!.ParentNode!).GetAttribute("DoNotAcceptEvents"), "the banner wrapper");
        }

        var icon = root.SelectNodes(".//Widget[@Sprite]")!.Cast<XmlElement>().Single();
        Assert.AreEqual(@"General\Mission\noble", icon.GetAttribute("Sprite"));
        Assert.AreEqual("true", icon.GetAttribute("DoNotAcceptEvents"));
        Assert.AreEqual("29", icon.GetAttribute("SuggestedWidth"), "the sprite is 58x40");
        Assert.AreEqual("20", icon.GetAttribute("SuggestedHeight"));
    }

    [TestMethod]
    public void TheTalkIcon_IsUnchanged()
    {
        var text = File.ReadAllText(PartyPath("PartyTroopTuple.xml"));

        StringAssert.Contains(text, "Sprite=\"PartyScreen\\talk_icon\" HueFactor=\"10\" SaturationFactor=\"60\" ValueFactor=\"20\"");
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void TheNobleSprite_ExistsInTheInstalledGame()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));
        var sprites = Path.Combine(GameAssemblies.GameDir, "Modules", "Native", "GUI", "NativeSpriteData.xml");
        if (!File.Exists(sprites))
            Assert.Inconclusive("vanilla sprite data not found at " + sprites);

        StringAssert.Contains(File.ReadAllText(sprites), @"<Name>General\Mission\noble</Name>");
    }

    [TestMethod]
    public void TheModule_IsInFeatureModulesOnce_RegistersTheProvider_AndOwnsNoSaveData()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<PrisonerBannersModule>().Count());
        var module = new PrisonerBannersModule();

        Assert.AreEqual("PrisonerBanners", module.Id);
        Assert.IsFalse(module.OwnsSaveData);
        Assert.IsNull(module.ParkedReason);
        using var container = new Container();
        module.RegisterServices(container);
        Assert.AreSame(container.Resolve<PrisonerBannersSettingsProvider>(), container.Resolve<PrisonerBannersSettingsProvider>());
        foreach (var owner in new[] { "Main/IoC.cs", "Main/SubModule.cs", "Client/TaomClientComposition.cs" })
            StringAssert.DoesNotMatch(ReadSource(owner, stripComments: true), new Regex("PrisonerBanners"), owner);
    }
}
