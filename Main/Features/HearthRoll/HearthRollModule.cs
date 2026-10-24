using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.HearthRoll;

/// <summary>
/// Hearth Roll (#906) as a feature module: the clan screen's Members list tints an unhappy companion's row and shows each child's
/// age. It registers only the settings provider. The mixin and the three prefab insertions live in the Client submodule, which
/// UIExtenderEx finds by assembly scan, and they resolve what they need from the container when a row refreshes. No behaviors, no
/// Harmony and nothing saved.
/// </summary>
internal sealed class HearthRollModule : TaomFeatureModule
{
    public override string Id => "HearthRoll";

    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<HearthRollSettingsProvider>(Reuse.Singleton);
    }
}
