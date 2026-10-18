using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.AnimationVariety.Hooks;

namespace TAOM.Features.AnimationVariety;

/// <summary>
/// The <c>[AnimVariety]</c> proof probe for random swing clips through the action-set
/// <c>alternative_group</c> attribute. The service is built with the behavior so every mission starts
/// clean; the probe is off unless the MCM switch is on, and does nothing in a mission without
/// <c>act_taom_*_b..h</c> action codes.
/// <para>
/// The same module carries the gait variety feature (<see cref="GaitVarietyService"/>): a random step size
/// and a random gait set for orc and goblin warriors, applied once per soldier at spawn. Its service is
/// built per mission too; the variant catalog is a process singleton, because action sets load once.
/// </para>
/// </summary>
internal sealed class AnimationVarietyModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new AnimVarietyMissionBehavior(
            new AnimVarietyService(
                r.Resolve<IAnimVarietyAdapter>(), r.Resolve<IAnimVarietyProbeSettingsProvider>(), r.Resolve<IModLogger>()),
            r.Resolve<IModLogger>())),
        MissionBehaviorDecl.Of((_, r) => new GaitVarietyMissionBehavior(
            new GaitVarietyService(
                r.Resolve<GaitVariantCatalog>(), r.Resolve<IGaitVarietySettingsProvider>(), r.Resolve<IModLogger>()),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "AnimationVariety";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IAnimVarietyAdapter, AnimVarietyAdapter>(Reuse.Singleton);
        registrator.Register<IGaitVarietyAdapter, GaitVarietyAdapter>(Reuse.Singleton);
        registrator.Register<IGaitVarietySettingsProvider, GaitVarietySettingsProvider>(Reuse.Singleton);
        registrator.Register<IAnimVarietyProbeSettingsProvider, AnimVarietyProbeSettingsProvider>(Reuse.Singleton);
        registrator.Register<GaitVariantCatalog>(Reuse.Singleton);
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
