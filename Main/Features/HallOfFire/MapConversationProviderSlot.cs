using SandBox.View;
using SandBox.View.Map;

namespace TAOM.Features.HallOfFire;

/// <summary>
/// The engine side of <see cref="IMapConversationProviderSlot"/>: <c>SandBoxViewSubModule.MapConversationDataProvider</c>
/// (static getter) and <c>SetMapConversationDataProvider</c> (public static). Both read the module instance, so they
/// are valid only after the SandBox.View module has loaded; the caller guards against a throw.
/// </summary>
public sealed class MapConversationProviderSlot : IMapConversationProviderSlot
{
    public IMapConversationDataProvider? Current => SandBoxViewSubModule.MapConversationDataProvider;

    public void Set(IMapConversationDataProvider provider) => SandBoxViewSubModule.SetMapConversationDataProvider(provider);
}
