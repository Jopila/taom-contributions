using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Validation;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IMapTerrainAdapter"/> over <c>Campaign.Current.MapSceneWrapper</c> (v1.5.3). The terrain
/// type is read straight off the face (<c>PathFaceRecord.FaceGroupIndex</c>, which is exactly what
/// <c>MapScene.GetFaceTerrainType</c> returns) after an <c>IsValid</c> check, because
/// <c>GetFaceTerrainType</c> raises <c>Debug.FailedAssert</c> on an invalid face and the territory
/// sampling asks about the open sea thousands of times. Heights come straight from the heightfield
/// (<c>Scene.GetTerrainHeight</c>, as vanilla drapes its hover outline; over water it is the bed), not from
/// <c>MapScene.GetHeightAtPoint</c>: that is a physics query against collision bodies, and one border
/// tile's worth of them froze the game for 286 ms in the first look session. <c>IsInForest</c> (#849) reads the party's own face,
/// the member vanilla reads: a struct copy while the stored position has a warm face cache, vanilla's native face lookup while it is cold.
/// </summary>
public sealed class MapTerrainAdapter : IMapTerrainAdapter
{
    public bool TryGetBounds(out float minX, out float minY, out float maxX, out float maxY)
    {
        minX = minY = maxX = maxY = 0f;
        var scene = Campaign.Current?.MapSceneWrapper;
        if (scene == null)
            return false;
        scene.GetMapBorders(out Vec2 min, out Vec2 max, out _);
        (minX, minY, maxX, maxY) = (min.x, min.y, max.x, max.y);
        return maxX > minX && maxY > minY;
    }

    public int TerrainTypeAt(float x, float y)
    {
        var scene = Campaign.Current?.MapSceneWrapper;
        if (scene == null)
            return -1;
        // isOnLand: true. Without NavalDLC native accepts any face; with it loaded (TAOM refuses it,
        // #120) the lookup keeps only land-navigable faces, and mountains, rivers and lakes would read
        // as "no face". War Sails support would need vanilla's land-then-sea fallback here
        // (SandBoxNavigationCache.GetFaceRecordForPoint).
        var face = scene.GetFaceIndex(new CampaignVec2(new Vec2(x, y), true));
        return face.IsValid() ? face.FaceGroupIndex : -1;
    }

    public bool IsInForest(MobileParty party)
    {
        // Null party first, then the campaign map: CurrentNavigationFace is a computed getter (Position.Face) that
        // dereferences Campaign.Current when the position's cache is cold, so it must not be touched without a campaign.
        if (party == null || Campaign.Current?.MapSceneWrapper == null)
            return false;
        // The party's own face, as vanilla reads it, not a new CampaignVec2 (which would always pay the native lookup). The
        // IsValid check is MapScene.GetFaceTerrainType's own, which otherwise raises Debug.FailedAssert on an invalid face; the
        // terrain type is the face group index.
        var face = party.CurrentNavigationFace;
        return face.IsValid() && face.FaceGroupIndex == (int)TerrainType.Forest;
    }

    public float HeightAt(float x, float y, float fallback)
    {
        var scene = (Campaign.Current?.MapSceneWrapper as MapScene)?.Scene;
        if (scene == null)
            return fallback;
        float height = scene.GetTerrainHeight(new Vec2(x, y), true);
        return FiniteFloatValidator.IsFinite(height) ? height : fallback;
    }
}
