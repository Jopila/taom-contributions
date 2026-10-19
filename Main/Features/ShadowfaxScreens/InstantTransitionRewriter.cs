using System.Xml;

namespace TAOM.Features.ShadowfaxScreens;

/// <summary>
/// Pure XML transform behind Shadowfax Screens: every <c>VisualDefinition</c> in a prefab changes
/// state at once. Vanilla v1.5.5 slides most screen panels in over 0.45 s and animates list rows for
/// 0.075 to 0.15 s.
///
/// It sets <c>TransitionDuration</c> to 0 on each definition in <c>Prefab/VisualDefinitions</c>, the
/// only block <c>WidgetPrefab.LoadFrom</c> reads. A definition without the attribute gets it too,
/// because Gauntlet gives it a 0.2 s default (<c>VisualDefinitionTemplate</c>). Nothing else changes:
/// the ease settings and the VisualStates stay, so each panel ends in its vanilla position. With a
/// zero duration <c>Widget.UpdateVisualDefinitions</c> snaps to the target state on its first tick,
/// and <c>DelayOnBegin</c> has no effect. Null-safe and idempotent.
/// </summary>
public static class InstantTransitionRewriter
{
    private const string Instant = "0";

    /// <summary>Returns the number of VisualDefinitions this call changed.</summary>
    public static int MakeInstant(XmlDocument? document)
    {
        var definitions = document?["Prefab"]?["VisualDefinitions"]?.ChildNodes;
        if (definitions == null)
            return 0;

        int changed = 0;
        foreach (XmlNode node in definitions)
        {
            if (node is not XmlElement { Name: "VisualDefinition" } definition
                || definition.GetAttribute("TransitionDuration") == Instant)
                continue;

            definition.SetAttribute("TransitionDuration", Instant);
            changed++;
        }

        return changed;
    }
}
