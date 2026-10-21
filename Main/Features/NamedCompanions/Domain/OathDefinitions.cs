using System.Collections.Generic;
using Newtonsoft.Json;

namespace TAOM.Features.NamedCompanions.Domain;

/// <summary>What a named companion asks for before joining the player's party.</summary>
public sealed class HireTermsDefinition
{
    /// <summary>Gold price. Null keeps the vanilla price; when set it replaces it (0 means items only).</summary>
    [JsonProperty("gold")]
    public int? Gold { get; set; }

    [JsonProperty("items")]
    public List<ItemCostDefinition> Items { get; set; }

    [JsonProperty("requires_flags")]
    public List<string> RequiresFlags { get; set; }

    /// <summary>Text id of the line shown while the terms are unmet; empty or unresolved falls back to the generic line.</summary>
    [JsonProperty("locked_string_id")]
    public string LockedStringId { get; set; }
}

public sealed class ItemCostDefinition
{
    [JsonProperty("item_id")]
    public string ItemId { get; set; }

    [JsonProperty("count")]
    public int Count { get; set; } = 1;
}

public enum TopicWhen
{
    Any,
    Recruited,
    Unrecruited,
}

public sealed class TopicDefinition
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("prompt_string_id")]
    public string PromptStringId { get; set; }

    [JsonProperty("reply_string_id")]
    public string ReplyStringId { get; set; }

    [JsonProperty("when")]
    public string When { get; set; } = "any";

    /// <summary>Parsed form of <see cref="When"/>, set by the validator.</summary>
    [JsonIgnore]
    public TopicWhen WhenKind { get; set; } = TopicWhen.Any;

    [JsonProperty("min_relation")]
    public int MinRelation { get; set; } = -100;

    /// <summary>Hidden while the relation is above this (-100..100, default 100).</summary>
    [JsonProperty("max_relation")]
    public int MaxRelation { get; set; } = 100;

    /// <summary>
    /// Percent chance (1..100) the topic is offered on a given day; the roll is fixed per character, topic and day
    /// (<see cref="Oaths.OathDailyRoll"/>). 100 means always.
    /// </summary>
    [JsonProperty("random_chance")]
    public int RandomChance { get; set; } = 100;

    [JsonProperty("requires_flags")]
    public List<string> RequiresFlags { get; set; }

    /// <summary>Items the companion hands to the player; they arrive silently in the party inventory.</summary>
    [JsonProperty("give_items")]
    public List<ItemCostDefinition> GiveItems { get; set; }

    /// <summary>Items the player hands over; the topic is blocked until the party holds them.</summary>
    [JsonProperty("take_items")]
    public List<ItemCostDefinition> TakeItems { get; set; }

    [JsonProperty("sets_flags")]
    public List<string> SetsFlags { get; set; }

    /// <summary>Relation change on completion, -10..10; a negative value lowers it.</summary>
    [JsonProperty("relation_gain")]
    public int RelationGain { get; set; }

    [JsonProperty("gold_cost")]
    public int GoldCost { get; set; }

    [JsonProperty("gold_reward")]
    public int GoldReward { get; set; }

    [JsonProperty("once")]
    public bool Once { get; set; }

    [JsonProperty("cooldown_days")]
    public int CooldownDays { get; set; }
}
