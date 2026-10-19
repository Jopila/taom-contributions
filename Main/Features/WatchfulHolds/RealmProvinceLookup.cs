using TAOM.Features.RealmBorders;

namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// <see cref="IWatchfulProvinceLookup"/> over the Realm Borders province map, the lookup
/// <c>RealmBorderService.OnHourlyTick</c> makes for the crossing notices. The territory is per map and
/// outlives a campaign, so nothing is cached here. With the Realm Borders switch off the lookup has no answer, so the
/// gate is off with the feature that draws the provinces.
/// </summary>
public sealed class RealmProvinceLookup : IWatchfulProvinceLookup
{
    private readonly RealmTerritoryService _territory;
    private readonly IRealmBordersSettings _borders;

    public RealmProvinceLookup(RealmTerritoryService territory, IRealmBordersSettings borders)
    {
        _territory = territory;
        _borders = borders;
    }

    public bool TryGetProvinceFief(float x, float y, out string fiefId)
    {
        fiefId = string.Empty;
        if (!_borders.Enabled)
            return false;

        var result = _territory.Result;
        if (_territory.State != TerritoryState.Ready || result == null)
            return false;

        int province = result.Map.ProvinceAt(x, y);
        if (province >= 0 && province < result.FiefIds.Count)
            fiefId = result.FiefIds[province];
        return true;
    }
}
