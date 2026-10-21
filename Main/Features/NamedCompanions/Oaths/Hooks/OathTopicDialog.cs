using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.NamedCompanions.Oaths.Hooks;

/// <summary>
/// The topic half of Oaths of Service (#904): one "other things" option on <c>hero_main_options</c>, one player
/// line per configured topic, one reply. The chosen (companion, topic) pair lives in a transient field, set when
/// the player picks the line and read by the reply line the conversation moves to next.
/// </summary>
internal sealed class OathTopicDialog
{
    private static readonly IReadOnlyList<TopicDefinition> NoTopics = new TopicDefinition[0];

    private readonly OathTopicService _topics;
    private readonly INamedCompanionConfigProvider _config;
    private readonly ICoopSessionProvider _coop;
    private readonly Func<string, string> _itemName;
    private readonly IModLogger _logger;
    private string _companionId;
    private string _topicId;

    public OathTopicDialog(OathTopicService topics, INamedCompanionConfigProvider config, ICoopSessionProvider coop,
        Func<string, string> itemName, IModLogger logger)
    {
        _topics = topics;
        _config = config;
        _coop = coop;
        _itemName = itemName;
        _logger = logger;
    }

    public void Register(CampaignGameStarter starter)
    {
        starter.AddPlayerLine("taom_oath_topics_open", "hero_main_options", "taom_oath_topics_npc",
            "{=taom_oath_topics_open}There is something I would ask you.", () => Visible().Count > 0, null);
        starter.AddDialogLine("taom_oath_topics_npc", "taom_oath_topics_npc", "taom_oath_topics",
            "{=taom_oath_topics_npc}Ask, then.", () => true, null);
        foreach (var companion in _config.GetCompanions())
            foreach (var topic in companion.Topics ?? new List<TopicDefinition>())
                AddTopicLine(starter, companion.CharacterId, topic);
        starter.AddPlayerLine("taom_oath_topics_back", "taom_oath_topics", "lord_pretalk",
            "{=D33fIGQe}Never mind.", () => true, null, 1);
        starter.AddDialogLine("taom_oath_topic_reply", "taom_oath_topic_reply", "lord_pretalk", "{=!}{TAOM_OATH_REPLY}",
            SetReplyText, Complete);
        // The conversation manager has no exit when no NPC line matches, so a reply that cannot be built must not stall it.
        starter.AddDialogLine("taom_oath_topic_reply_fallback", "taom_oath_topic_reply", "lord_pretalk",
            "{=taom_oath_reply_fallback}Another time, perhaps.", () => true, null, 1);
    }

    private void AddTopicLine(CampaignGameStarter starter, string companionId, TopicDefinition topic)
    {
        if (!GameTexts.TryGetText(topic.PromptStringId, out var prompt)) return;
        // Value keeps the {=key} prefix, so the line is localized when it is shown, not when it is registered.
        starter.AddPlayerLine($"taom_oath_topic_{companionId}_{topic.Id}", "taom_oath_topics", "taom_oath_topic_reply",
            prompt.Value, () => IsOffered(companionId, topic),
            () => { _companionId = companionId; _topicId = topic.Id; }, 100,
            (out TextObject why) => CanChoose(companionId, topic, out why));
    }

    private IReadOnlyList<TopicDefinition> Visible()
    {
        try
        {
            var hero = Hero.OneToOneConversationHero;
            // Hero.Clan is CompanionOf ?? own clan, so a companion promoted to lord counts as recruited, as in the anchor.
            return hero == null ? NoTopics : _topics.Visible(hero.StringId, hero.Clan != null, Today());
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Oaths] could not list topics: {ex}");
            return NoTopics;
        }
    }

    private bool IsOffered(string companionId, TopicDefinition topic) =>
        Hero.OneToOneConversationHero?.StringId == companionId && Visible().Contains(topic);

    private bool CanChoose(string companionId, TopicDefinition topic, out TextObject why)
    {
        why = TextObject.GetEmpty();
        var blocker = _topics.Blocker(companionId, topic, Today(), _coop.IsCoopClient);
        if (blocker == TopicBlocker.None) return true;
        if (blocker == TopicBlocker.Items)
        {
            why = new TextObject("{=taom_oath_missing_hint}You still need: {TAOM_OATH_MISSING}");
            why.SetTextVariable("TAOM_OATH_MISSING",
                OathTextRenderer.Missing(_topics.MissingTakeItems(companionId, topic), _itemName));
            return false;
        }
        why = blocker == TopicBlocker.Purse ? new TextObject("{=taom_oath_hint_purse}You cannot afford that.")
            : blocker == TopicBlocker.Coop ? new TextObject("{=taom_oath_coop_hint}Only the host can seal an oath in a shared campaign.")
            : new TextObject("{=taom_oath_hint_cooldown}Not so soon after the last time.");
        return false;
    }

    private bool SetReplyText()
    {
        var topic = _topics.FindTopic(_companionId, _topicId);
        if (topic == null || !GameTexts.TryGetText(topic.ReplyStringId, out var reply)) return false;
        MBTextManager.SetTextVariable("TAOM_OATH_REPLY", reply);
        return true;
    }

    private void Complete()
    {
        try { _topics.Complete(_companionId, _topicId, Today(), _coop.IsCoopClient); }
        catch (Exception ex) { _logger.LogError($"[Oaths] topic consequence failed: {ex}"); }
    }

    private static int Today() => (int)CampaignTime.Now.ToDays;
}
