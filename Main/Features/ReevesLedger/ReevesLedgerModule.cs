using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;

namespace TAOM.Features.ReevesLedger;

/// <summary>
/// The Reeve's Ledger (#857, docs/features/reeves-ledger.md): extras on the town management window. The module registers
/// services only. The two view model mixins and the two prefab insertions need no registration: UIExtenderEx finds them
/// by assembly scan, and they resolve what they need from the container when the window opens. It saves nothing, so a
/// fault never fails a campaign start.
/// </summary>
internal sealed class ReevesLedgerModule : TaomFeatureModule
{
    public override string Id => "ReevesLedger";

    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ReevesLedgerSettingsProvider>(Reuse.Singleton);
        registrator.Register<IFiefLedgerAdapter, FiefLedgerAdapter>(Reuse.Singleton);
        registrator.Register<ReevesLedgerService>(Reuse.Singleton);
    }
}
