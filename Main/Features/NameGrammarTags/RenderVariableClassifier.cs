using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TAOM.Features.NameGrammarTags;

/// <summary>What a text variable of a vanilla row holds, as far as its name tells.</summary>
public enum NameKind
{
    Other,
    Kingdom,
    Clan,
    Settlement,
    Culture,
}

/// <summary>Which text of the object a variable carries.</summary>
public enum NameForm
{
    Name,
    Informal,
    Formal,
    Link,
}

/// <summary>A classified variable. <c>Slot</c> is the name with its form suffix removed: KINGDOM and KINGDOM_LINK are one slot.</summary>
public readonly record struct NameVariable(string Name, NameKind Kind, NameForm Form, string Slot);

/// <summary>Classifies the variables of vanilla rows by name, so the render can bind the same kind of TextObject vanilla's code sets.</summary>
public static class RenderVariableClassifier
{
    private static readonly Regex BraceVariable = new Regex(@"\{([A-Z][A-Z0-9_]*(?:\.[A-Z0-9_]+)*)\}", RegexOptions.Compiled);

    private static readonly Regex GenderArgument = new Regex(@"GENDER\(([A-Z][A-Z0-9_]*)\)", RegexOptions.Compiled);

    /// <summary>Whole name tokens that make a variable text about the object (a title, a cost), not the object's name.</summary>
    private static readonly HashSet<string> NotAName = new HashSet<string>(StringComparer.Ordinal)
    {
        "DESCRIPTION", "TERM", "OFFICIAL", "LEADER", "RULER", "ICON", "COST", "PAYMENT", "AMOUNT", "DURATION",
        "POLICY", "SUPPORT", "ADJECTIVE", "YEAR", "INFO", "DETAILS", "REPUTATION", "TITLE", "FIRSTNAME",
    };

    private static readonly string[] FormSuffixes =
        { "_INFORMAL_NAME", "_INFORMALNAME", "_FORMAL_NAME", "_FORMALNAME", "_NAME", "_LINK", "_SHORT" };

    public static NameVariable Classify(string variable)
    {
        var name = variable ?? string.Empty;
        return new NameVariable(name, KindOf(name), FormOf(name), SlotOf(name));
    }

    /// <summary>The variables a row uses: <c>{NAME}</c> tokens and the argument of <c>{#GENDER(NAME)}</c>, in order, once each.</summary>
    public static IReadOnlyList<string> VariablesIn(string text)
    {
        var seen = new List<(int Position, string Name)>();
        foreach (Match m in BraceVariable.Matches(text ?? string.Empty))
            seen.Add((m.Index, m.Groups[1].Value));
        foreach (Match m in GenderArgument.Matches(text ?? string.Empty))
            seen.Add((m.Index, m.Groups[1].Value));
        return seen.OrderBy(s => s.Position).Select(s => s.Name).Distinct(StringComparer.Ordinal).ToList();
    }

    private static NameKind KindOf(string name)
    {
        if (name.Length == 0 || name.IndexOf('.') >= 0 || name.Split('_').Any(token => NotAName.Contains(token)))
            return NameKind.Other;
        // Culture is checked before settlement: SETTLEMENT_CULTURE_NAME holds a culture name.
        if (Has(name, "CULTURE")) return NameKind.Culture;
        if (Has(name, "KINGDOM", "FACTION", "REALM")) return NameKind.Kingdom;
        if (Has(name, "CLAN")) return NameKind.Clan;
        if (Has(name, "SETTLEMENT", "TOWN", "CASTLE", "VILLAGE", "FIEF")) return NameKind.Settlement;
        return NameKind.Other;
    }

    private static NameForm FormOf(string name)
    {
        if (name.EndsWith("_LINK", StringComparison.Ordinal)) return NameForm.Link;
        if (name.IndexOf("INFORMAL", StringComparison.Ordinal) >= 0 || name.EndsWith("_SHORT", StringComparison.Ordinal))
            return NameForm.Informal;
        if (name.IndexOf("FORMAL", StringComparison.Ordinal) >= 0) return NameForm.Formal;
        return NameForm.Name;
    }

    private static string SlotOf(string name)
    {
        foreach (var suffix in FormSuffixes)
            if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
                return SlotOf(name.Substring(0, name.Length - suffix.Length));
        return name;
    }

    private static bool Has(string name, params string[] words) =>
        words.Any(w => name.IndexOf(w, StringComparison.Ordinal) >= 0);
}
