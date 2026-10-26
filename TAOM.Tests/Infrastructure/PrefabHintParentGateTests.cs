using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// A Gauntlet <c>HintWidget</c> has no hover of its own: it relays its parent widget's hover events (v1.5.5 HintWidget.cs:18),
/// and hit-testing never picks a widget with <c>DoNotAcceptEvents="true"</c> (EventManager.cs:897). A hint under such a
/// parent never shows. The trap shipped three times (Reeve's Ledger #857, Messengers #898, Smith's Measure #910;
/// docs/reviews/lessons/localization-ui.md), so this gate builds the XML of every in-code PrefabExtension in TAOM.Client and
/// fails on a hint whose parent refuses events. A hint at the root of an insert has a vanilla parent and is skipped.
/// </summary>
[TestClass]
public class PrefabHintParentGateTests
{
    private const string DocumentAttribute = "PrefabExtensionXmlDocumentAttribute";

    private static IEnumerable<(Type Type, XmlDocument Document)> Documents()
    {
        foreach (var type in LoadableTypes(typeof(TAOM.Client.ClientSubModule).Assembly))
        {
            var method = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.CustomAttributes.Any(a => a.AttributeType.Name == DocumentAttribute));
            if (method == null || type.IsAbstract)
                continue;
            var instance = Activator.CreateInstance(type, nonPublic: true)!;
            yield return (type, (XmlDocument)method.Invoke(instance, null)!);
        }
    }

    // Some Client types name game assemblies the test host may not load; the extensions themselves load without them, and
    // the count floor below fails the gate if they ever stop doing so.
    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    // Gauntlet nests a widget's children in a <Children> element, so a hint's widget parent is two levels up.
    private static XmlElement? WidgetParent(XmlElement hint)
    {
        var up = hint.ParentNode as XmlElement;
        if (up != null && up.Name == "Children")
            up = up.ParentNode as XmlElement;
        return up;
    }

    [TestMethod]
    public void TheGate_ScansEveryInCodePrefabExtension()
    {
        Assert.IsTrue(Documents().Count() >= 9, "fewer in-code prefab extensions than TAOM.Client ships: the scan found nothing to check");
    }

    [TestMethod]
    public void NoHintWidget_SitsUnderAParentThatRefusesEvents()
    {
        var bad = new List<string>();
        foreach (var (type, document) in Documents())
        {
            foreach (var hint in document.GetElementsByTagName("HintWidget").Cast<XmlElement>())
            {
                var parent = WidgetParent(hint);
                if (parent != null && string.Equals(parent.GetAttribute("DoNotAcceptEvents"), "true", StringComparison.OrdinalIgnoreCase))
                    bad.Add($"{type.FullName}: HintWidget '{hint.GetAttribute("DataSource")}' under <{parent.Name} Id=\"{parent.GetAttribute("Id")}\">");
            }
        }

        Assert.AreEqual(0, bad.Count, "a hint under a parent with DoNotAcceptEvents=\"true\" never shows:\n" + string.Join("\n", bad));
    }
}
