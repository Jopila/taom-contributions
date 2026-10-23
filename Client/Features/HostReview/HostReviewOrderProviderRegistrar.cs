using TAOM.Features.HostReview;
using TAOM.Features.HostReview.Hooks;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order.Visual;

namespace TAOM.Client.Features.HostReview;

/// <summary>Registers Host Review's order provider with the engine (ADR-013); core's null registrar registers nothing.</summary>
internal sealed class HostReviewOrderProviderRegistrar : IHostReviewOrderProviderRegistrar
{
    // Once per process: the engine's factory rejects a second provider of the same type. HostReviewModule calls this
    // only at ApplyPhase.ProcessLoad.
    public void Register() => VisualOrderFactory.RegisterProvider(new HostReviewVisualOrderProvider());
}
