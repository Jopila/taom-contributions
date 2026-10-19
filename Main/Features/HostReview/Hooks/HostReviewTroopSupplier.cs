using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Features.HostReview.Hooks;

/// <summary>
/// Hands the review lineup to the engine's spawn logic, one troop at a time, in lineup order. The origin
/// comes from a factory so the allocation can be tested with a fake; in the game it is an invincible
/// party origin, so nothing the review does is written back to the party.
/// </summary>
internal sealed class HostReviewTroopSupplier : IMissionTroopSupplier
{
    private readonly IReadOnlyList<CharacterObject> _lineup;
    private readonly Func<CharacterObject, int, IAgentOriginBase> _makeOrigin;
    private int _next;

    public HostReviewTroopSupplier(IReadOnlyList<CharacterObject> lineup, Func<CharacterObject, int, IAgentOriginBase> makeOrigin)
    {
        _lineup = lineup;
        _makeOrigin = makeOrigin;
    }

    public int NumRemovedTroops => 0;

    public int NumTroopsNotSupplied => _lineup.Count - _next;

    public bool AnyTroopRemainsToBeSupplied => _next < _lineup.Count;

    public IEnumerable<IAgentOriginBase> SupplyTroops(int numberToAllocate)
    {
        var count = Math.Max(0, Math.Min(numberToAllocate, _lineup.Count - _next));
        var supplied = new List<IAgentOriginBase>(count);
        for (var i = 0; i < count; i++)
            supplied.Add(Supply());
        return supplied;
    }

    public IAgentOriginBase? SupplyOneTroop() => AnyTroopRemainsToBeSupplied ? Supply() : null;

    public IEnumerable<IAgentOriginBase> GetAllTroops()
    {
        var all = new List<IAgentOriginBase>(_lineup.Count);
        for (var i = 0; i < _lineup.Count; i++)
            all.Add(_makeOrigin(_lineup[i], i));
        return all;
    }

    public BasicCharacterObject? GetGeneralCharacter() => _lineup.Count > 0 ? _lineup[0] : null;

    public int GetNumberOfPlayerControllableTroops() => _lineup.Count;

    private IAgentOriginBase Supply()
    {
        var rank = _next;
        return _makeOrigin(_lineup[_next++], rank);
    }
}
