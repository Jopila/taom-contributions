using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// Reads the XML a UIExtenderEx prefab extension inserts, through its <c>[PrefabExtensionXmlNodes]</c> method. The attribute is
/// matched by name, so no TaleWorlds or UIExtenderEx attribute is constructed and it runs without the game.
/// </summary>
public static class PrefabNodes
{
    public static List<XmlElement> Of(Type prefab)
    {
        var instance = Activator.CreateInstance(prefab, nonPublic: true)!;
        var method = prefab.GetMethods().Single(m => m.CustomAttributes.Any(a => a.AttributeType.Name == "PrefabExtensionXmlNodesAttribute"));
        return ((IEnumerable<XmlNode>)method.Invoke(instance, null)!).OfType<XmlElement>().ToList();
    }
}
