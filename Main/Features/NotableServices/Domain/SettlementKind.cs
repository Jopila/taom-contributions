namespace TAOM.Features.NotableServices.Domain;

/// <summary>What kind of settlement a notable lives in; it decides where a gift of men goes.</summary>
public enum SettlementKind
{
    /// <summary>Anything else (a hideout, none): no gift is taken.</summary>
    Other,

    Village,

    Town,

    Castle,
}
