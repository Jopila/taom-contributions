using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BillsBurden.UI;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.BillsBurden;

/// <summary>Source and data pins for the map-bar cargo row (#876): brush layer, sprite declaration, vanilla tooltip helpers.</summary>
[TestClass]
public class CargoMapBarWiringTests
{
    private const string MapBarBrushes = "Main/_Module/GUI/Brushes/MapBar.xml";
    private const string SpriteData = "Main/_Module/GUI/TAOMSpriteData.xml";
    private const string MixinFile = "Client/Features/BillsBurden/UI/CargoMapBarMixin.cs";
    private const string Sprite = @"MapBar\taom_cargo";

    [TestMethod]
    public void RightIconsBrush_HasExactlyOneCargoLayerWithTheCargoSprite()
    {
        var layers = LayersOf("MapBar.Right.Icons")
            .Where(l => (string?)l.Attribute("Name") == CargoMapBarMixin.ItemId)
            .ToList();

        Assert.AreEqual(1, layers.Count,
            $"MapBar.Right.Icons needs one BrushLayer named '{CargoMapBarMixin.ItemId}': the icon widget looks the layer up by the item id.");
        Assert.AreEqual(Sprite, (string?)layers[0].Attribute("Sprite"));
    }

    [TestMethod]
    public void Mixin_BuildsTheTooltipFromBothVanillaInventoryHelpers()
    {
        var source = ReadSource(MixinFile, stripComments: true);

        StringAssert.Contains(source, "GetPartyInventoryWeightTooltip");
        StringAssert.Contains(source, "GetPartyInventoryCapacityTooltip");
    }

    [TestMethod]
    public void Mixin_ReadsThePartyThroughCampaignCurrent_NeverMobilePartyMainParty()
    {
        var source = ReadSource(MixinFile, stripComments: true);

        StringAssert.Contains(source, "Campaign.Current?.MainParty");
        Assert.IsFalse(source.Contains("MobileParty.MainParty"), "MobileParty.MainParty throws without a campaign.");
    }

    // The first-refresh pins trunk gave the special-resource row (#958, .claude/rules/gui-ui.md): MapBarVM.Initialize
    // runs the first refresh before the view loads and the next one 2 s later, so a skip shows the row late.
    [TestMethod]
    public void Mixin_InsertsTheRowOnTheFirstRefresh_AfterOnlyTheNoPartyGuard()
    {
        var core = Between(ReadSource(MixinFile, stripComments: true), "private void OnRefreshCore()", "private void InsertItemOnce()");
        var insert = core.IndexOf("InsertItemOnce();", StringComparison.Ordinal);

        Assert.IsTrue(insert >= 0, "OnRefreshCore must insert the row.");
        Assert.AreEqual(1, Regex.Matches(core.Substring(0, insert), @"\breturn\s*;").Count,
            "Only the no-party guard may return before the insert; another early return can bring back a first-refresh skip.");
    }

    [TestMethod]
    public void Mixin_GuardsTheInsertWithContains_AndKeepsNoOneShotFlag()
    {
        var source = ReadSource(MixinFile, stripComments: true);

        StringAssert.Contains(Between(source, "private void InsertItemOnce()", "private List<TooltipProperty> GetTooltipProperties()"),
            "items.Contains(_item)", "Contains keeps one row and re-adds it after a rebuilt list.");
        Assert.IsFalse(Regex.IsMatch(source, @"\bprivate\s+bool\s+_"),
            "A one-shot bool flag cannot re-add the row to a rebuilt list; guard with Contains instead.");
    }

    [TestMethod]
    [Ignore("#876: sprite not baked yet; remove after the editor bake is synced back with tools/sync_sprite_bake.ps1")]
    public void CargoSprite_IsDeclaredInTheTaomSpriteSheet()
    {
        var doc = XDocument.Parse(ReadSource(SpriteData));
        var parts = doc.Descendants("SpritePart").Select(p => (string?)p.Element("Name")).ToList();
        var generics = doc.Descendants("GenericSprite").Select(p => (string?)p.Element("Name")).ToList();

        // Both: brush layers resolve through the GenericSprite table, which needs its SpritePart.
        CollectionAssert.Contains(parts, Sprite, $"TAOMSpriteData.xml declares no SpritePart '{Sprite}'.");
        CollectionAssert.Contains(generics, Sprite, $"TAOMSpriteData.xml declares no GenericSprite '{Sprite}'.");
    }

    private static string Between(string text, string from, string to)
    {
        var start = text.IndexOf(from, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"'{from}' not found in {MixinFile}.");
        var end = text.IndexOf(to, start, StringComparison.Ordinal);
        Assert.IsTrue(end > start, $"'{to}' not found after '{from}' in {MixinFile}.");
        return text.Substring(start, end - start);
    }

    private static System.Collections.Generic.IEnumerable<XElement> LayersOf(string brushName)
    {
        var doc = XDocument.Parse(ReadSource(MapBarBrushes));
        var brush = doc.Descendants("Brush").Single(b => (string?)b.Attribute("Name") == brushName);
        return brush.Element("Layers")!.Elements("BrushLayer");
    }
}
