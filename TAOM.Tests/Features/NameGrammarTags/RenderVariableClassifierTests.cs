using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Tests.Features.NameGrammarTags;

[TestClass]
public class RenderVariableClassifierTests
{
    [DataTestMethod]
    [DataRow("KINGDOM", NameKind.Kingdom, NameForm.Name)]
    [DataRow("KINGDOM_NAME", NameKind.Kingdom, NameForm.Name)]
    [DataRow("MAP_FACTION_NAME", NameKind.Kingdom, NameForm.Name)]
    [DataRow("KINGDOM_TO_CALL_TO_WAR_AGAINST_LINK", NameKind.Kingdom, NameForm.Link)]
    [DataRow("ENEMY_KINGDOM_INFORMAL_NAME", NameKind.Kingdom, NameForm.Informal)]
    [DataRow("ENEMYFACTION_INFORMALNAME", NameKind.Kingdom, NameForm.Informal)]
    [DataRow("CHARACTER_FACTION_SHORT", NameKind.Kingdom, NameForm.Informal)]
    [DataRow("KINGDOM_FORMAL_NAME", NameKind.Kingdom, NameForm.Formal)]
    [DataRow("KINGDOM_FORMAL_NAME_LINK", NameKind.Kingdom, NameForm.Link)]
    [DataRow("CLAN_NAME", NameKind.Clan, NameForm.Name)]
    [DataRow("CLAN", NameKind.Clan, NameForm.Name)]
    [DataRow("SETTLEMENT", NameKind.Settlement, NameForm.Name)]
    [DataRow("SETTLEMENT_LINK", NameKind.Settlement, NameForm.Link)]
    [DataRow("TARGET_SETTLEMENT", NameKind.Settlement, NameForm.Name)]
    [DataRow("SETTLEMENT_CULTURE_NAME", NameKind.Culture, NameForm.Name)]
    [DataRow("CULTURE", NameKind.Culture, NameForm.Name)]
    public void Classify_NameVariable_GetsItsKindAndForm(string variable, NameKind kind, NameForm form)
    {
        var result = RenderVariableClassifier.Classify(variable);
        Assert.AreEqual(kind, result.Kind, variable);
        Assert.AreEqual(form, result.Form, variable);
    }

    [DataTestMethod]
    [DataRow("RULER.NAME")]
    [DataRow("LORD.LINK")]
    [DataRow("GOLD_AMOUNT")]
    [DataRow("CLAN_DESCRIPTION")]
    [DataRow("FACTION_TERM")]
    [DataRow("FACTION_LEADER")]
    [DataRow("FACTION_OFFICIAL")]
    [DataRow("RULER_NAME_AND_TITLE")]
    [DataRow("CULTURE_ADJECTIVE")]
    [DataRow("TITLE")]
    [DataRow("REPUTATION")]
    public void Classify_NonNameVariable_IsOther(string variable)
    {
        Assert.AreEqual(NameKind.Other, RenderVariableClassifier.Classify(variable).Kind, variable);
    }

    [TestMethod]
    public void Classify_LinkAndNameOfTheSameThing_ShareASlot()
    {
        Assert.AreEqual(RenderVariableClassifier.Classify("KINGDOM").Slot, RenderVariableClassifier.Classify("KINGDOM_LINK").Slot);
        Assert.AreEqual(RenderVariableClassifier.Classify("CALLED_KINGDOM").Slot, RenderVariableClassifier.Classify("CALLED_KINGDOM_LINK").Slot);
        Assert.AreEqual(RenderVariableClassifier.Classify("KINGDOM").Slot, RenderVariableClassifier.Classify("KINGDOM_FORMAL_NAME").Slot);
    }

    [TestMethod]
    public void Classify_TwoDifferentKingdomVariables_HaveDifferentSlots()
    {
        Assert.AreNotEqual(RenderVariableClassifier.Classify("KINGDOM1_LINK").Slot, RenderVariableClassifier.Classify("KINGDOM2_LINK").Slot);
    }

    [TestMethod]
    public void VariablesIn_BracesAndGenderArguments_AreFoundInOrderOnce()
    {
        var text = "{#GENDER(CALLED_KINGDOM)}o {#}a {#}{\\#}{CALLED_KINGDOM} {?LORD.GENDER}x{?}y{\\?} {.di}{KINGDOM} {=abc}{KINGDOM} {LORD.NAME}";
        CollectionAssert.AreEqual(new[] { "CALLED_KINGDOM", "KINGDOM", "LORD.NAME" }, RenderVariableClassifier.VariablesIn(text).ToArray());
    }

    [TestMethod]
    public void VariablesIn_TextWithoutVariables_IsEmpty()
    {
        Assert.AreEqual(0, RenderVariableClassifier.VariablesIn("Nessuna variabile").Count);
    }
}
