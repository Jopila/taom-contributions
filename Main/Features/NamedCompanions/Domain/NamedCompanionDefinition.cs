using System.Collections.Generic;
using Newtonsoft.Json;

namespace TAOM.Features.NamedCompanions.Domain;

public sealed class NamedCompanionDefinition
{
    [JsonProperty("character_id")]
    public string CharacterId { get; set; }

    [JsonProperty("spawn_settlement")]
    public string SpawnSettlement { get; set; }

    [JsonProperty("race")]
    public string Race { get; set; }

    [JsonProperty("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Where the daily anchor sends a <c>stay_at_home</c> companion. Optional: empty means the spawn settlement.
    /// Spawn and re-place on load still use <see cref="SpawnSettlement"/>.
    /// </summary>
    [JsonProperty("home_settlement")]
    public string HomeSettlement { get; set; }

    /// <summary>While unrecruited, the companion returns to its home settlement (else its spawn one) each day.</summary>
    [JsonProperty("stay_at_home")]
    public bool StayAtHome { get; set; }

    /// <summary>Null means vanilla hire.</summary>
    [JsonProperty("hire_terms")]
    public HireTermsDefinition HireTerms { get; set; }

    /// <summary>Null or empty means no extra dialog topics.</summary>
    [JsonProperty("topics")]
    public List<TopicDefinition> Topics { get; set; }
}
