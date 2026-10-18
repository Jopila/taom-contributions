using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

[TestClass]
public class WarVetoReasonsTests
{
    [TestMethod]
    public void Text_Permanent_KeepsTheExistingWarBlockedKey()
    {
        StringAssert.StartsWith(WarVetoReasons.Text(WarVeto.Permanent), "{=taom_war_blocked}");
    }

    [TestMethod]
    public void Text_EveryVeto_HasItsOwnKeyedInlineDefault()
    {
        var vetoes = Enum.GetValues(typeof(WarVeto)).Cast<WarVeto>().Where(v => v != WarVeto.None).ToList();
        var keys = new HashSet<string>();

        foreach (var veto in vetoes)
        {
            var text = WarVetoReasons.Text(veto);
            StringAssert.Matches(text, new System.Text.RegularExpressions.Regex(@"^\{=taom_[a-z_]+\}\S"));
            keys.Add(text.Substring(0, text.IndexOf('}')));
        }

        Assert.AreEqual(vetoes.Count, keys.Count, "each veto needs a distinct string key");
    }

    [TestMethod]
    public void Text_None_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => WarVetoReasons.Text(WarVeto.None));
    }
}
