using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Quests;
using TAOM.Features.ShadowfaxScreens.Quests.Hooks;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IQuestJournalAdapter"/> (docs/features/shadowfax-screens.md). The one reflection site
/// is the private <c>QuestItemVM.PopulateQuestLog(JournalLog, bool)</c>, cached once at <see cref="Bind"/>; the rest is public
/// API (<c>JournalLogEntry.GetEntries</c>, <c>IViewDataTracker.UnExaminedQuestLogs</c>). The replay goes through the
/// <see cref="MethodInfo"/> so every other patch on PopulateQuestLog still runs.
/// </summary>
public class QuestJournalAdapter : IQuestJournalAdapter
{
    private MethodInfo? _populateQuestLog;

    public bool IsBound => _populateQuestLog != null;

    public string? Bind()
    {
        _populateQuestLog = null;
        try
        {
            var populate = AccessTools.Method(typeof(QuestItemVM), "PopulateQuestLog", new[] { typeof(JournalLog), typeof(bool) });
            if (populate == null)
                return "QuestItemVM.PopulateQuestLog(JournalLog, bool) not found";

            // The module runner ignores whether a category attached. A skipped PopulateQuestLog with no on-demand build, no
            // updated dot or no sort fallback would leave quests empty or undated, so deferral needs all four patches.
            var missing = new[]
            {
                Attached(populate, typeof(QuestItemVM_PopulateQuestLog_QuestLogPatch), prefix: true),
                Attached(AccessTools.Method(typeof(QuestsVM), "SetSelectedItem"), typeof(QuestsVM_SetSelectedItem_QuestLogPatch), prefix: true),
                Attached(AccessTools.Method(typeof(QuestItemVM), nameof(QuestItemVM.UpdateIsUpdated)), typeof(QuestItemVM_UpdateIsUpdated_QuestLogPatch), prefix: false),
                Attached(QuestItemComparerBase_GetJournalLogAt_QuestLogPatch.TargetMethod(), typeof(QuestItemComparerBase_GetJournalLogAt_QuestLogPatch), prefix: false),
            }.Where(name => name != null).ToList();
            if (missing.Count > 0)
                return "not attached: " + string.Join(", ", missing);

            _populateQuestLog = populate;
            return null;
        }
        catch (Exception ex)
        {
            _populateQuestLog = null;
            return ex.GetType().Name + ": " + ex.Message;
        }
    }

    public void Rebuild(object item)
    {
        var populate = _populateQuestLog ?? throw new InvalidOperationException("QuestJournalAdapter is not bound");
        var vm = (QuestItemVM)item;
        var entry = vm.QuestLogEntry ?? throw new InvalidOperationException("the item has no journal entry");
        foreach (var log in entry.GetEntries())
            Invoke(populate, vm, log);

        vm.UpdateIsUpdated();
        vm.RefreshValues();
    }

    public object? FirstOrLastEntry(object item, bool last)
    {
        var entries = ((QuestItemVM)item).QuestLogEntry?.GetEntries();
        if (entries == null) return null;
        return last ? entries.LastOrDefault() : entries.FirstOrDefault();
    }

    public bool AnyUnexamined(object item)
    {
        var entry = ((QuestItemVM)item).QuestLogEntry;
        var unexamined = Campaign.Current?.GetCampaignBehavior<IViewDataTracker>()?.UnExaminedQuestLogs;
        if (entry == null || unexamined == null || unexamined.Count == 0) return false;
        return entry.GetEntries().Any(log => unexamined.Any(u => u == log));
    }

    private static void Invoke(MethodInfo populate, QuestItemVM vm, JournalLog log)
    {
        try
        {
            populate.Invoke(vm, new object[] { log, false });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }

    // Returns the patch's name when it is NOT attached, else null.
    private static string? Attached(MethodBase? target, Type patchType, bool prefix)
    {
        var info = target == null ? null : Harmony.GetPatchInfo(target);
        var patches = prefix ? info?.Prefixes : info?.Postfixes;
        return patches != null && patches.Any(p => p.PatchMethod?.DeclaringType == patchType) ? null : patchType.Name;
    }
}
