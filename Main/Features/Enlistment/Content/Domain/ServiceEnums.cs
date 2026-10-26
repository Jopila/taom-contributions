namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>
/// Service rank ladder, the US Army enlisted ranks. Ordinal order is the promotion order and the
/// ordinal is persisted as an int, so never reorder or renumber. Saves written when the ladder had
/// four ranks (Recruit, Soldier, Veteran, Sergeant) keep their ordinals 0 to 3 and read as Private,
/// Specialist, Sergeant and Staff Sergeant. The kit tier is a separate four-value key: see
/// <c>EnlistmentKitTiers.For</c>.
/// </summary>
public enum ServiceRank
{
    Private = 0,
    Specialist = 1,
    Sergeant = 2,
    StaffSergeant = 3,
    SergeantFirstClass = 4,
    MasterSergeant = 5,
    SergeantMajor = 6,
}

/// <summary>Player's chosen service role. Persisted as int — never reorder.</summary>
public enum ServiceAssignment
{
    Infantry = 0,
    Archer = 1,
    Cavalry = 2,
    Support = 3,
}

/// <summary>Commander-reputation domains (donor's four, enum-typed instead of strings).</summary>
public enum ReputationDomain
{
    None = 0,
    Field = 1,
    Logistics = 2,
    Command = 3,
    Siege = 4,
}
