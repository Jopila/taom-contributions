using TAOM.Features.FieldCamp.Domain;
using TaleWorlds.Library;

namespace TAOM.Features.FieldCamp;

/// <summary>There is no map scene to draw on (a dedicated server, or a client where TAOM.Client did not load); Show reports not ready, so the caller keeps retrying, which costs one call.</summary>
public sealed class NullCampVisualService : ICampVisualService
{
    public bool Show(string partyId, CampType type, Vec2 position) => false;

    public bool IsShown(string partyId) => false;

    public void Remove(string partyId)
    {
    }

    public void ClearAll()
    {
    }
}
