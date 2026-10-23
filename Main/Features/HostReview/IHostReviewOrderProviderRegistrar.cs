namespace TAOM.Features.HostReview;

/// <summary>
/// Registers TAOM's order provider with the engine once per process, so the order bar is available in a Host
/// Review mission. The provider derives from a TaleWorlds.MountAndBlade.View type, so it lives in TAOM.Client
/// (ADR-013).
/// </summary>
public interface IHostReviewOrderProviderRegistrar
{
    void Register();
}
