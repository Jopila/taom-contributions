using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.TokensOfEsteem.Models;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IGiftConversationAdapter"/>: the conversation statics a repeatable line reads, as
/// vanilla's fief grant list uses them (<c>CompanionRolesCampaignBehavior</c>, v1.5.5 lines 131, 199, 205 and 231). A row is
/// the line text <c>{=!}{GIFT_ROW}</c> whose variable this adapter fills with a localized sentence, so the two kinds of row
/// (goods, purse) share one line.
/// </summary>
public sealed class GiftConversationAdapter : IGiftConversationAdapter
{
    private readonly IModLogger _logger;

    public GiftConversationAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public string? PartnerId => Campaign.Current == null ? null : Hero.OneToOneConversationHero?.StringId;

    public GiftOption? CurrentOption => Campaign.Current == null ? null : ConversationSentence.CurrentProcessedRepeatObject as GiftOption;

    public GiftOption? SelectedOption => Campaign.Current == null ? null : ConversationSentence.SelectedRepeatObject as GiftOption;

    public void SetOptions(IReadOnlyList<GiftOption> options)
    {
        try
        {
            // IReadOnlyList<out T>: the option list is the engine's list of objects as it stands; the engine only reads it.
            ConversationSentence.SetObjectsToRepeatOver(options);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] Handing the gift list to the conversation failed: {ex.Message}");
        }
    }

    public void WriteRow(GiftOption option)
    {
        try
        {
            TextObject row;
            if (option.Kind == GiftOptionKind.Purse)
            {
                row = new TextObject("{=taom_gift_row_purse}Give a purse of {PURSE_GOLD} gold.");
                row.SetTextVariable("PURSE_GOLD", option.Amount);
            }
            else
            {
                row = new TextObject("{=taom_gift_row_good}Give {GOOD_NAME} (you carry {GOOD_COUNT}).");
                row.SetTextVariable("GOOD_NAME", option.Name);
                row.SetTextVariable("GOOD_COUNT", option.Count);
            }
            ConversationSentence.SelectedRepeatLine.SetTextVariable("GIFT_ROW", row);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Gifts] Writing a gift row failed: {ex.Message}");
        }
    }
}
