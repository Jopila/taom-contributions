using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Adapters;

/// <summary>
/// The campaign map's terrain: its extent, the navmesh terrain type under a point, the ground height and whether a party stands in
/// forest. Main-thread only.
/// </summary>
public interface IMapTerrainAdapter
{
    /// <summary>The playable extent from the scene's border markers; false with no campaign map loaded.</summary>
    bool TryGetBounds(out float minX, out float minY, out float maxX, out float maxY);

    /// <summary>
    /// The <c>TaleWorlds.Core.TerrainType</c> value of the land navmesh face under the point, or -1
    /// where the land navmesh has no face (open sea, off the map).
    /// </summary>
    int TerrainTypeAt(float x, float y);

    /// <summary>
    /// True when the party stands on a forest face (false for a null party, no campaign map or an invalid face): a struct copy while the
    /// party's stored position has a warm face cache; while it is cold (after a load, until the party moves) each read repeats vanilla's
    /// native face lookup.
    /// </summary>
    bool IsInForest(MobileParty party);

    /// <summary>The ground height at the point, or <paramref name="fallback"/> when the scene cannot say.</summary>
    float HeightAt(float x, float y, float fallback);
}
