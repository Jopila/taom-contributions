using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TAOM.Client;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Gauntlet finds a widget type by scanning the AppDomain (ADR-013, B4a): <c>WidgetInfo.CollectWidgetTypes</c>
/// takes every loaded assembly that references TaleWorlds.GauntletUI and every type in it assignable to
/// Widget, and <c>WidgetFactory</c> then looks a prefab tag up by SHORT type name and calls a public
/// <c>(UIContext)</c> constructor. These tests pin each of those preconditions for the widgets that live in
/// TAOM.Client. The runtime proof is the client boot.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClientWidgetDiscoveryTests
{
    private static readonly string[] FrontEndWidgets =
    {
        "TAOM.Features.FactionUI.UI.Widgets.FilteredPropertyRow",
        "TAOM.Features.FactionUI.UI.Widgets.FrontEndRichTextWidget",
        "TAOM.Features.FactionUI.UI.Widgets.FrontEndTextWidget",
        "TAOM.Features.FactionUI.UI.Widgets.NarrativeRandomButton",
        "TAOM.Features.FactionUI.UI.Widgets.ScrollShortcutButton",
        "TAOM.Features.FactionUI.UI.Widgets.StageJumpButton",
        "TAOM.Features.FactionUI.UI.Widgets.ThemeIconWidget",
        "TAOM.Features.FactionMap.Widgets.BannerWidget",
        "TAOM.Features.FactionMap.Widgets.FactionImageWidget",
        "TAOM.Features.FactionMap.Widgets.MapContainerWidget",
        "TAOM.Features.FactionMap.Widgets.PolygonWidget",
    };

    // B4b: the campaign map's settlement plate is a client widget too; the engine builds it from a prefab tag.
    private static readonly string[] CampaignMapWidgets =
    {
        "TAOM.Features.SettlementNameplateRelation.TaomSettlementPlateWidget",
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) =>
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    private static Type[] TypesOf(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).ToArray()!; }
    }

    [TestMethod]
    public void TaomClient_ReferencesTaleWorldsGauntletUI()
    {
        // The predicate of WidgetInfo.CheckAssemblyReferencesThis: the compiler drops a reference no code uses,
        // and an assembly that does not name GauntletUI is never scanned for widgets.
        var gauntlet = typeof(Widget).Assembly.GetName().Name;

        var references = typeof(ClientSubModule).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        CollectionAssert.Contains(references.ToList(), gauntlet,
            "TAOM.Client.dll must reference " + gauntlet + " or Gauntlet never scans it for widget types");
    }

    [TestMethod]
    public void EveryFrontEndWidget_LivesInTaomClient_WithAUIContextConstructor()
    {
        var client = typeof(ClientSubModule).Assembly;
        var core = typeof(SubModule).Assembly;
        var problems = new List<string>();

        foreach (var fullName in FrontEndWidgets.Concat(CampaignMapWidgets))
        {
            if (core.GetType(fullName) != null)
                problems.Add(fullName + " is still defined in TAOM.dll");
            var type = client.GetType(fullName);
            if (type == null) { problems.Add(fullName + " is not defined in TAOM.Client.dll"); continue; }
            if (!typeof(Widget).IsAssignableFrom(type))
                problems.Add(fullName + " is not a Widget");
            if (type.GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(UIContext) }, null) == null)
                problems.Add(fullName + " has no public (UIContext) constructor (WidgetFactory.CreateBuiltinWidget)");
        }

        CollectionAssert.AreEqual(new List<string>(), problems);
    }

    [TestMethod]
    public void TaomWidgets_TwoAssemblies_NoTwoShareAShortName()
    {
        // WidgetFactory keys builtin widget types by Type.Name, so a tie between TAOM.dll and TAOM.Client.dll
        // would leave one of the two unreachable from a prefab.
        var names = new[] { typeof(SubModule).Assembly, typeof(ClientSubModule).Assembly }
            .SelectMany(TypesOf)
            .Where(t => typeof(Widget).IsAssignableFrom(t))
            .GroupBy(t => t.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key + " (" + string.Join(", ", g.Select(t => t.FullName)) + ")")
            .ToList();

        CollectionAssert.AreEqual(new List<string>(), names);
    }
}
