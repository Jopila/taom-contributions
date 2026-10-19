using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;

namespace TAOM.Features.ShadowfaxScreens.Quests.Hooks;

/// <summary>
/// Keeps the date sort right for a deferred item. The sort reads a finished quest's date from its first or last stage, which
/// a deferred item does not have yet; vanilla's <c>GetJournalLogAt</c> then returns null and the item would sort as undated.
/// A postfix, only when vanilla returned null and the item is deferred, supplies the first or last journal entry, the same
/// log that stage would have held. The target is a protected method of a private abstract nested type, so it is resolved by
/// name; <c>logIndex</c> is declared as <c>object</c> because its enum (First = 0, Last = 1) is private too.
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(QuestLogDeferCalls.Category)]
public static class QuestItemComparerBase_GetJournalLogAt_QuestLogPatch
{
    private const int LastIndex = 1;

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
        => AccessTools.Method(AccessTools.Inner(typeof(QuestItemSortControllerVM), "QuestItemComparerBase"), "GetJournalLogAt");

    [HarmonyPostfix]
    public static void Postfix(QuestItemVM questItem, object logIndex, ref JournalLog? __result)
    {
        if (__result != null || questItem == null) return;
        var entry = QuestLogDeferCalls.SortFallback(questItem, Convert.ToInt32(logIndex) == LastIndex);
        if (entry is JournalLog log)
            __result = log;
    }
}
