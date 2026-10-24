using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.LoremastersLine;

/// <summary>
/// The Loremaster's Line (#877, docs/features/loremasters-line.md): a marker on the character screen's skill bar where
/// learning is at its slowest. The module registers the settings provider only. The view model mixin and the prefab
/// insertion live in TAOM.Client and need no registration: UIExtenderEx finds them by assembly scan, and the mixin
/// resolves what it needs from the container when the screen opens. It saves nothing.
/// </summary>
internal sealed class LoremastersLineModule : TaomFeatureModule
{
    public override string Id => "LoremastersLine";

    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<LoremastersLineSettingsProvider>(Reuse.Singleton);
    }
}
