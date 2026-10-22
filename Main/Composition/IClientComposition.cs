using DryIoc;

namespace TAOM.Composition;

/// <summary>
/// The TAOM.Client submodule's registrations in core's container (ADR-013). Core never references
/// TAOM.Client: the client submodule's constructor puts its implementation in
/// <see cref="ClientComposition.Current"/>, and IoC.Configure runs it after every core registration.
/// </summary>
public interface IClientComposition
{
    /// <summary>
    /// Registrations only, never a Resolve. A registration that passes no ifAlreadyRegistered REPLACES
    /// every core default registration of that service type (a client adapter over core's null adapter,
    /// and all of a collection's core contributors). To add to a contributor collection instead, pass
    /// IfAlreadyRegistered.AppendNotKeyed explicitly. A replacement cannot reach a service core already
    /// resolved during its own registration (BannerInjectionIoC, HeroRaceIoC): that graph
    /// keeps the core instance.
    /// </summary>
    void RegisterServices(IRegistrator registrator);
}
