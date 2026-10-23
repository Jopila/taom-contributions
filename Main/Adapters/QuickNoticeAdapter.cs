using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <inheritdoc />
public sealed class QuickNoticeAdapter : IQuickNoticeAdapter
{
    private readonly IModLogger _logger;

    public QuickNoticeAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool Show(string text, string announcerHeroId)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
                return false;
            var hero = string.IsNullOrEmpty(announcerHeroId)
                ? null
                : Campaign.Current?.CampaignObjectManager?.Find<Hero>(announcerHeroId);
            // "{=!}" keeps the wrapper out of translation lookup; {T} is still tokenized, a no-op for rendered prose.
            var message = new TextObject("{=!}{T}");
            message.SetTextVariable("T", text);
            MBInformationManager.AddQuickInformation(message, 3000, hero?.CharacterObject);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] QuickNoticeAdapter.Show failed: {ex.Message}");
            return false;
        }
    }
}
