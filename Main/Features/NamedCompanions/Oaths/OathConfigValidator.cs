using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TAOM.Core.Logging;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.NamedCompanions.Oaths;

public sealed class OathValidationResult
{
    /// <summary>How many values were reverted to a default or skipped.</summary>
    public int Reversions { get; set; }
}

/// <summary>
/// Semantic checks for the oath fields of <c>named_companion_config.json</c> (csharp-architecture.md,
/// "Config Providers MUST Validate"). Mutates the definitions in place: a bad value reverts to its default
/// or the entry is skipped, each with a warning, then one summary warning.
/// </summary>
public static class OathConfigValidator
{
    private const int MaxGold = 1_000_000;
    private const int MaxItemEntries = 6;
    private const int MaxItemCount = 999;
    private const int MaxTopicGoldCost = 100_000;
    private const int MaxTopicGoldReward = 10_000;
    private const int MaxRelationGain = 10;
    private const int MaxCooldownDays = 365;

    private static readonly Regex TopicIdPattern = new("^[a-z0-9_]{1,48}$");

    public static OathValidationResult Validate(IEnumerable<NamedCompanionDefinition> companions, IModLogger logger)
    {
        var result = new OathValidationResult();
        if (companions == null) return result;

        var topicIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var companion in companions)
        {
            if (companion == null) continue;
            var who = companion.CharacterId ?? "<no character_id>";
            companion.HireTerms = ValidateHireTerms(companion.HireTerms, who, logger, result);
            companion.Topics = ValidateTopics(companion.Topics, who, topicIds, logger, result);
        }

        if (result.Reversions > 0)
            logger.LogWarning($"NamedCompanion oaths: {result.Reversions} invalid value(s) reverted or skipped; see the warnings above");
        return result;
    }

    /// <summary>
    /// Drops what cannot resolve against the loaded game: hire terms or topics naming an unknown item, topics whose
    /// prompt or reply text is unknown, an unknown locked line (the generic line applies instead) and an unknown
    /// home settlement (the spawn settlement applies instead). Returns the number of drops.
    /// </summary>
    public static int ResolveIds(
        IEnumerable<NamedCompanionDefinition> companions,
        Func<string, bool> itemExists,
        Func<string, bool> textExists,
        Func<string, bool> settlementExists,
        IModLogger logger)
    {
        var drops = 0;
        if (companions == null) return drops;

        foreach (var companion in companions)
        {
            if (companion == null) continue;
            var who = companion.CharacterId ?? "<no character_id>";

            if (!string.IsNullOrEmpty(companion.HomeSettlement) && !settlementExists(companion.HomeSettlement))
            {
                drops++;
                logger.LogWarning($"NamedCompanion oaths [{who}]: unknown home_settlement '{companion.HomeSettlement}' cleared; the spawn settlement is the home");
                companion.HomeSettlement = null;
            }

            var terms = companion.HireTerms;
            if (terms != null)
            {
                // One unknown item drops the whole block: the rest of the terms would be a cheaper hire than configured.
                var unknown = FirstUnknownItem(terms.Items, itemExists);
                if (unknown != null)
                {
                    drops++;
                    logger.LogWarning($"NamedCompanion oaths [{who}]: unknown item '{unknown}' in hire terms; the whole block is dropped and vanilla hire applies");
                    companion.HireTerms = null;
                    terms = null;
                }
            }

            if (terms != null)
            {
                if (!string.IsNullOrEmpty(terms.LockedStringId) && !textExists(terms.LockedStringId))
                {
                    drops++;
                    logger.LogWarning($"NamedCompanion oaths [{who}]: unknown locked_string_id '{terms.LockedStringId}' cleared");
                    terms.LockedStringId = null;
                }
            }

            if (companion.Topics == null) continue;
            var topics = new List<TopicDefinition>();
            foreach (var topic in companion.Topics)
            {
                if (!textExists(topic.PromptStringId) || !textExists(topic.ReplyStringId))
                {
                    drops++;
                    logger.LogWarning($"NamedCompanion oaths [{who}]: topic '{topic.Id}' skipped, its prompt or reply text id is unknown");
                    continue;
                }
                // Like the hire terms: a topic that gave or took only part of its items would not be the one configured.
                var unknownItem = FirstUnknownItem(topic.GiveItems, itemExists) ?? FirstUnknownItem(topic.TakeItems, itemExists);
                if (unknownItem != null)
                {
                    drops++;
                    logger.LogWarning($"NamedCompanion oaths [{who}]: topic '{topic.Id}' skipped, unknown item '{unknownItem}' in give_items or take_items");
                    continue;
                }
                topics.Add(topic);
            }
            companion.Topics = topics;
        }

        if (drops > 0)
            logger.LogWarning($"NamedCompanion oaths: {drops} unresolved id(s) dropped; see the warnings above");
        return drops;
    }

    private static string FirstUnknownItem(List<ItemCostDefinition> items, Func<string, bool> itemExists)
    {
        if (items == null) return null;
        foreach (var item in items)
            if (!itemExists(item.ItemId)) return item.ItemId;
        return null;
    }

    private static HireTermsDefinition ValidateHireTerms(
        HireTermsDefinition terms, string who, IModLogger logger, OathValidationResult result)
    {
        if (terms == null) return null;

        if (terms.Gold.HasValue && (terms.Gold.Value < 0 || terms.Gold.Value > MaxGold))
        {
            Revert(result, logger, $"[{who}] hire_terms.gold {terms.Gold.Value} is outside 0..{MaxGold}; vanilla price applies");
            terms.Gold = null;
        }

        terms.Items = ValidateItems(terms.Items, "hire_terms", who, logger, result);
        if (terms.Gold == 0 && terms.Items.Count == 0)
        {
            Revert(result, logger, $"[{who}] hire_terms.gold 0 with no items asks for nothing (0 means items only); vanilla price applies");
            terms.Gold = null;
        }
        terms.RequiresFlags = ValidateFlags(terms.RequiresFlags, "hire_terms.requires_flags", who, logger, result);

        if (!terms.Gold.HasValue && terms.Items.Count == 0 && terms.RequiresFlags.Count == 0)
        {
            Revert(result, logger, $"[{who}] hire_terms sets no gold, items or flags; vanilla hire applies");
            return null;
        }
        return terms;
    }

    private static List<ItemCostDefinition> ValidateItems(
        List<ItemCostDefinition> items, string field, string who, IModLogger logger, OathValidationResult result)
    {
        var kept = new List<ItemCostDefinition>();
        if (items == null) return kept;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
            {
                Revert(result, logger, $"[{who}] {field} item with an empty item_id skipped");
                continue;
            }
            if (item.Count < 1 || item.Count > MaxItemCount)
            {
                Revert(result, logger, $"[{who}] {field} item '{item.ItemId}' count {item.Count} is outside 1..{MaxItemCount}; skipped");
                continue;
            }
            if (!seen.Add(item.ItemId))
            {
                Revert(result, logger, $"[{who}] {field} item '{item.ItemId}' is listed twice; the repeat is skipped");
                continue;
            }
            if (kept.Count >= MaxItemEntries)
            {
                Revert(result, logger, $"[{who}] {field} lists more than {MaxItemEntries} items; '{item.ItemId}' dropped");
                continue;
            }
            kept.Add(item);
        }
        return kept;
    }

    private static List<string> ValidateFlags(
        List<string> flags, string field, string who, IModLogger logger, OathValidationResult result)
    {
        var kept = new List<string>();
        if (flags == null) return kept;

        foreach (var raw in flags)
        {
            if (OathFlagName.TryNormalize(raw, out var flag)) kept.Add(flag);
            else Revert(result, logger, $"[{who}] {field} entry '{raw}' is not a valid flag name; skipped");
        }
        return kept;
    }

    private static List<TopicDefinition> ValidateTopics(
        List<TopicDefinition> topics, string who, HashSet<string> topicIds, IModLogger logger, OathValidationResult result)
    {
        var kept = new List<TopicDefinition>();
        if (topics == null) return kept;

        foreach (var topic in topics)
        {
            if (topic == null) continue;
            if (TopicIsValid(topic, who, topicIds, logger, result)) kept.Add(topic);
        }
        return kept;
    }

    private static bool TopicIsValid(
        TopicDefinition topic, string who, HashSet<string> topicIds, IModLogger logger, OathValidationResult result)
    {
        if (topic.Id == null || !TopicIdPattern.IsMatch(topic.Id))
            return Skip(result, logger, $"[{who}] topic id '{topic.Id}' does not match ^[a-z0-9_]{{1,48}}$; topic skipped");
        if (!topicIds.Add(topic.Id))
            return Skip(result, logger, $"[{who}] topic id '{topic.Id}' is not unique in the file; topic skipped");
        if (string.IsNullOrWhiteSpace(topic.PromptStringId))
            return Skip(result, logger, $"[{who}] topic '{topic.Id}' has no prompt_string_id; topic skipped");
        if (string.IsNullOrWhiteSpace(topic.ReplyStringId))
            return Skip(result, logger, $"[{who}] topic '{topic.Id}' has no reply_string_id; topic skipped");

        if (!TryParseWhen(topic.When, out var when))
            return Skip(result, logger, $"[{who}] topic '{topic.Id}' has unknown when '{topic.When}'; topic skipped");
        topic.WhenKind = when;

        if (topic.MinRelation < -100 || topic.MinRelation > 100)
        {
            Revert(result, logger, $"[{who}] topic '{topic.Id}' min_relation {topic.MinRelation} is outside -100..100; reverted to -100");
            topic.MinRelation = -100;
        }
        if (topic.MaxRelation < -100 || topic.MaxRelation > 100)
        {
            Revert(result, logger, $"[{who}] topic '{topic.Id}' max_relation {topic.MaxRelation} is outside -100..100; reverted to 100");
            topic.MaxRelation = 100;
        }
        if (topic.MinRelation > topic.MaxRelation)
            return Skip(result, logger, $"[{who}] topic '{topic.Id}' min_relation {topic.MinRelation} is above max_relation {topic.MaxRelation}; topic skipped");
        if (topic.RandomChance < 1 || topic.RandomChance > 100)
        {
            Revert(result, logger, $"[{who}] topic '{topic.Id}' random_chance {topic.RandomChance} is outside 1..100; reverted to 100");
            topic.RandomChance = 100;
        }
        if (topic.RelationGain < -MaxRelationGain || topic.RelationGain > MaxRelationGain)
        {
            Revert(result, logger, $"[{who}] topic '{topic.Id}' relation_gain {topic.RelationGain} is outside -{MaxRelationGain}..{MaxRelationGain}; reverted to 0");
            topic.RelationGain = 0;
        }
        if (topic.GoldCost < 0 || topic.GoldCost > MaxTopicGoldCost)
        {
            Revert(result, logger, $"[{who}] topic '{topic.Id}' gold_cost {topic.GoldCost} is outside 0..{MaxTopicGoldCost}; reverted to 0");
            topic.GoldCost = 0;
        }
        if (topic.GoldReward < 0 || topic.GoldReward > MaxTopicGoldReward)
        {
            Revert(result, logger, $"[{who}] topic '{topic.Id}' gold_reward {topic.GoldReward} is outside 0..{MaxTopicGoldReward}; reverted to 0");
            topic.GoldReward = 0;
        }
        if (topic.CooldownDays < 0 || topic.CooldownDays > MaxCooldownDays)
        {
            Revert(result, logger, $"[{who}] topic '{topic.Id}' cooldown_days {topic.CooldownDays} is outside 0..{MaxCooldownDays}; reverted to 0");
            topic.CooldownDays = 0;
        }

        if (topic.GoldCost > 0 && topic.GoldReward > 0)
            return Skip(result, logger, $"[{who}] topic '{topic.Id}' sets both gold_cost and gold_reward; topic skipped");

        topic.GiveItems = ValidateItems(topic.GiveItems, $"topic '{topic.Id}' give_items", who, logger, result);
        topic.TakeItems = ValidateItems(topic.TakeItems, $"topic '{topic.Id}' take_items", who, logger, result);

        var changesState = topic.GoldCost > 0 || topic.GoldReward > 0 || topic.RelationGain != 0
            || topic.GiveItems.Count > 0 || topic.TakeItems.Count > 0;
        if (changesState && !topic.Once && topic.CooldownDays < 1)
            return Skip(result, logger, $"[{who}] topic '{topic.Id}' pays, charges, moves items or changes relation without once or cooldown_days; topic skipped");

        topic.RequiresFlags = ValidateFlags(topic.RequiresFlags, $"topic '{topic.Id}' requires_flags", who, logger, result);
        topic.SetsFlags = ValidateFlags(topic.SetsFlags, $"topic '{topic.Id}' sets_flags", who, logger, result);
        return true;
    }

    private static bool TryParseWhen(string raw, out TopicWhen when)
    {
        when = TopicWhen.Any;
        if (string.IsNullOrEmpty(raw)) return true;
        switch (raw.ToLowerInvariant())
        {
            case "any": return true;
            case "recruited": when = TopicWhen.Recruited; return true;
            case "unrecruited": when = TopicWhen.Unrecruited; return true;
            default: return false;
        }
    }

    private static bool Skip(OathValidationResult result, IModLogger logger, string message)
    {
        Revert(result, logger, message);
        return false;
    }

    private static void Revert(OathValidationResult result, IModLogger logger, string message)
    {
        result.Reversions++;
        logger.LogWarning($"NamedCompanion oaths {message}");
    }
}
