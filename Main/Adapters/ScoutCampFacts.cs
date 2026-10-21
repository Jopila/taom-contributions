namespace TAOM.Adapters;

/// <summary>
/// Plain facts the Far Watch rules (docs/features/far-watch.md) read about the player's camp and party. The adapter reports
/// what the campaign says and decides nothing: who may scout, what blocks a ride and what ends one are the service's.
/// <c>default</c> means there was no campaign.
/// </summary>
public readonly struct ScoutCampFacts
{
    /// <summary>The main party has a camp of any type standing.</summary>
    public bool CampExists { get; init; }

    /// <summary>The camp's raise is finished (<c>CampState.IsReady</c>).</summary>
    public bool CampReady { get; init; }

    /// <summary>A player encounter is live (<c>PlayerEncounter.Current != null</c>).</summary>
    public bool InEncounter { get; init; }

    /// <summary>The main party is in a map event.</summary>
    public bool InMapEvent { get; init; }

    /// <summary>The main party is inside a settlement.</summary>
    public bool InSettlement { get; init; }

    public string? MainHeroId { get; init; }

    public string? MainHeroName { get; init; }

    public int MainHeroSkill { get; init; }

    public bool MainHeroWounded { get; init; }

    /// <summary>The main hero is the only man in the party (<c>MemberRoster.TotalManCount == 1</c>).</summary>
    public bool PartyAlone { get; init; }

    /// <summary>The hero in the party's Scout role (<c>GetRoleHolder</c>, never <c>EffectiveScout</c>, which falls back to the leader), or null.</summary>
    public string? AssignedScoutId { get; init; }

    public string? AssignedScoutName { get; init; }

    public int AssignedScoutSkill { get; init; }

    public bool AssignedScoutWounded { get; init; }

    /// <summary>The assigned scout is in the main party, alive and not a prisoner.</summary>
    public bool AssignedScoutInParty { get; init; }
}
