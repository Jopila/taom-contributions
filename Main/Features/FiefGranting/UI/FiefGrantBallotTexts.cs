using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.Localization;

namespace TAOM.Features.FiefGranting.UI;

/// <summary>
/// Every line of player text the fief-vote ballot adds (#866), as inline <c>{=taom_fiefgrant_ballot_*}default</c> literals so
/// the harvester registers them.
/// </summary>
internal static class FiefGrantBallotTexts
{
    internal static string Count(int count) =>
        new TextObject("{=taom_fiefgrant_ballot_count}Current fiefs: {COUNT}").SetTextVariable("COUNT", count).ToString();

    /// <summary>
    /// The hover: a title, then one fief per line (or a line saying the clan holds none), then, when
    /// <paramref name="notCountedName"/> is set, the fief under vote that the count leaves out. Names sort ignoring case; the
    /// game pins the invariant culture, so the order is the same on every machine.
    /// </summary>
    internal static string Hint(IReadOnlyList<string> names, string? notCountedName)
    {
        var text = new StringBuilder(new TextObject("{=taom_fiefgrant_ballot_hint_title}Fiefs held").ToString());
        if (names.Count == 0)
            text.Append('\n').Append(new TextObject("{=taom_fiefgrant_ballot_hint_none}Holds no town or castle.").ToString());
        foreach (var name in names.OrderBy(n => n, StringComparer.InvariantCultureIgnoreCase))
            text.Append('\n').Append(name);
        if (!string.IsNullOrEmpty(notCountedName))
            text.Append('\n').Append(new TextObject("{=taom_fiefgrant_ballot_hint_excluded}Not counting {SETTLEMENT}, the fief under vote")
                .SetTextVariable("SETTLEMENT", notCountedName).ToString());
        return text.ToString();
    }
}
