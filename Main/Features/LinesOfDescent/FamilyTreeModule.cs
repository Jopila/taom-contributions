using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;

namespace TAOM.Features.LinesOfDescent;

/// <summary>
/// Lines of Descent (#905) as a feature module: the family tree service and its engine adapter. The popup, the clan screen
/// mixin and the hero page insertion live in the Client submodule and resolve <see cref="IFamilyTreeService"/> from the
/// container when a screen opens. No behaviors, no Harmony and nothing saved: the tree is read from the engine's family
/// lists each time it is opened.
/// </summary>
internal sealed class FamilyTreeModule : TaomFeatureModule
{
    public override string Id => "LinesOfDescent";

    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHeroFamilyAdapter, HeroFamilyAdapter>(Reuse.Singleton);
        registrator.Register<IFamilyTreeService, FamilyTreeService>(Reuse.Singleton);
    }
}
