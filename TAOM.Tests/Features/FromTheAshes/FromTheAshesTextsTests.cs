using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FromTheAshes;
using TAOM.Features.FromTheAshes.Hooks;

namespace TAOM.Tests.Features.FromTheAshes;

/// <summary>
/// The words of From the Ashes. A TextObject keeps its raw text in <c>Value</c> without the localization manager, so the
/// tests pin which key each block and outcome maps to rather than the rendered sentence.
/// </summary>
[TestClass]
public class FromTheAshesTextsTests
{
    private const string ItemsKey = "{=taom_ashes_need_items}";

    [TestMethod]
    public void Reason_OnlyNeedMaterialMapsToTheItemsText()
    {
        foreach (var block in Enum.GetValues(typeof(AshesBlock)).Cast<AshesBlock>())
        {
            var text = FromTheAshesTexts.Reason(new AshesStartCheck { Block = block, Variant = AshesVariant.Free, Count = 3 });

            var isItems = text != null && text.Value.StartsWith(ItemsKey, StringComparison.Ordinal);
            Assert.AreEqual(block == AshesBlock.NeedMaterial, isItems, block + " must not fall through to the items text");
        }
    }

    [TestMethod]
    public void IsShown_HidesAHiddenBlockAndAnOptionForTheWrongVariant()
    {
        var allowedFree = new AshesStartCheck { Block = AshesBlock.None, Variant = AshesVariant.Free };
        var blockedFree = new AshesStartCheck { Block = AshesBlock.NeedLabour, Variant = AshesVariant.Free };
        var hiddenFree = new AshesStartCheck { Block = AshesBlock.NotLooted, Variant = AshesVariant.Free };

        Assert.IsTrue(FromTheAshesTexts.IsShown(allowedFree, AshesVariant.Free));
        Assert.IsTrue(FromTheAshesTexts.IsShown(blockedFree, AshesVariant.Free), "a blocked option shows greyed out");
        Assert.IsFalse(FromTheAshesTexts.IsShown(allowedFree, AshesVariant.Thralls), "the thrall option stays hidden for a free player");
        Assert.IsFalse(FromTheAshesTexts.IsShown(blockedFree, AshesVariant.Thralls));
        Assert.IsFalse(FromTheAshesTexts.IsShown(hiddenFree, AshesVariant.Free), "a hidden block hides the option");
        Assert.IsFalse(FromTheAshesTexts.IsShown(allowedFree, AshesVariant.Project));
    }

    [TestMethod]
    public void Reason_EveryBlockTheMenuExplains_HasATooltip()
    {
        foreach (var block in Enum.GetValues(typeof(AshesBlock)).Cast<AshesBlock>().Where(b => b != AshesBlock.None && !FromTheAshesTexts.IsHidden(b)))
            Assert.IsNotNull(FromTheAshesTexts.Reason(new AshesStartCheck { Block = block, Variant = AshesVariant.Free }), block.ToString());
    }

    [TestMethod]
    public void Reason_ThrallLabour_NamesCaptives()
    {
        var thralls = FromTheAshesTexts.Reason(new AshesStartCheck { Block = AshesBlock.NeedLabour, Variant = AshesVariant.Thralls, Count = 20 });
        var free = FromTheAshesTexts.Reason(new AshesStartCheck { Block = AshesBlock.NeedLabour, Variant = AshesVariant.Free, Count = 20 });

        StringAssert.StartsWith(thralls!.Value, "{=taom_ashes_need_thralls}");
        StringAssert.StartsWith(free!.Value, "{=taom_ashes_need_troops}");
    }

    [TestMethod]
    public void EndMessage_EveryEndOutcome_HasAMessage()
    {
        // Stopped is the player's own choice and None/Running are not ends: no message by design.
        var silent = new[] { AshesOutcome.None, AshesOutcome.Running, AshesOutcome.Stopped };
        foreach (var outcome in Enum.GetValues(typeof(AshesOutcome)).Cast<AshesOutcome>())
        {
            var message = FromTheAshesTexts.EndMessage(new AshesResult { Outcome = outcome, Kind = AshesJobKind.VillageRebuild });

            Assert.AreEqual(!silent.Contains(outcome), message != null, outcome.ToString());
        }
    }

    [TestMethod]
    public void EndMessage_ShortOfMaterials_IsWordedForTheStalledWork()
    {
        var message = FromTheAshesTexts.EndMessage(new AshesResult { Outcome = AshesOutcome.ShortOfMaterials, Kind = AshesJobKind.VillageRebuild });

        StringAssert.StartsWith(message!.Value, "{=taom_ashes_short_rebuild}");
    }

    [TestMethod]
    public void EndMessage_ThrallCompletion_CreditsTheThralls()
    {
        var thralls = FromTheAshesTexts.EndMessage(new AshesResult { Outcome = AshesOutcome.Completed, Kind = AshesJobKind.VillageRebuild, Variant = AshesVariant.Thralls });
        var free = FromTheAshesTexts.EndMessage(new AshesResult { Outcome = AshesOutcome.Completed, Kind = AshesJobKind.VillageRebuild, Variant = AshesVariant.Free });

        StringAssert.StartsWith(thralls!.Value, "{=taom_ashes_done_thralls}");
        StringAssert.StartsWith(free!.Value, "{=taom_ashes_done_rebuild}");
    }
}
