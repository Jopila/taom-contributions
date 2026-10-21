using SandBox.View.Map;

namespace TAOM.Features.HallOfFire;

/// <summary>ADR-007 seam over the engine's single map conversation provider slot, so the installer is tested against a fake.</summary>
public interface IMapConversationProviderSlot
{
    IMapConversationDataProvider? Current { get; }

    void Set(IMapConversationDataProvider provider);
}
