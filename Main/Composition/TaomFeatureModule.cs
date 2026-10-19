using System;
using System.Collections.Generic;
using DryIoc;

namespace TAOM.Composition;

/// <summary>When a feature module's patch categories apply: one value per SubModule hook that applies categories.</summary>
internal enum ApplyPhase
{
    /// <summary>OnSubModuleLoad, once per process.</summary>
    ProcessLoad,

    /// <summary>The first OnBeforeInitialModuleScreenSetAsRoot, once per process.</summary>
    MainMenu,

    /// <summary>The first OnGameInitializationFinished, once per process.</summary>
    GameInit,

    /// <summary>The first OnMissionBehaviorInitialize, once Mission.Current exists, once per process.</summary>
    FirstMission,
}

/// <summary>
/// One feature's complete wiring, owned by the feature's folder instead of SubModule.cs and IoC.cs,
/// with empty defaults: an enabled module that registers, declares and does nothing. A feature
/// overrides only the dimensions it has. The runner visits modules in <see cref="FeatureModules.All"/>
/// order: <see cref="RegisterServices"/> for every module (parked ones included), then
/// <see cref="InitializeStatics"/> after every registration, then per lifecycle phase its categories,
/// <see cref="OnPhase"/>, campaign objects (<see cref="RegisterCampaignObjects"/>), behaviors, models and
/// mission behaviors.
/// </summary>
internal abstract class TaomFeatureModule
{
    /// <summary>The feature name, as in "WandererAllegiance"; used in every log line and test message.</summary>
    public abstract string Id { get; }

    /// <summary>
    /// Null for an enabled module. A parked module names why and the issue, and gets only its service
    /// registration. Compile-time only, never read from MCM (persisted MCM values outlive default flips).
    /// </summary>
    public virtual string? ParkedReason => null;

    /// <summary>
    /// True when the module's state must survive a save: a behavior persists SyncData, or RegisterCampaignObjects
    /// registers objects a save references. Such a module fails closed.
    /// </summary>
    public virtual bool OwnsSaveData => false;

    /// <summary>Container registrations only. IRegistrator has no Resolve, so an eager resolve does not compile.</summary>
    public virtual void RegisterServices(IRegistrator registrator) { }

    /// <summary>Patch-static handshakes, after every module and hand-wired feature has registered.</summary>
    public virtual void InitializeStatics(IResolver resolver) { }

    /// <summary>
    /// Registers engine objects the campaign must know before it builds its lists (building types). Runs in
    /// campaign start, for a CampaignGameStarter only: campaign objects register first; every behavior and model
    /// factory then runs before any is added, so a factory throw adds no behavior or model; a module whose save
    /// holds its objects owns save data, so that throw fails closed.
    /// </summary>
    public virtual void RegisterCampaignObjects(IResolver resolver) { }

    public virtual IReadOnlyList<PatchCategoryDecl> PatchCategories => Array.Empty<PatchCategoryDecl>();

    public virtual IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Array.Empty<CampaignBehaviorDecl>();

    public virtual IReadOnlyList<GameModelDecl> GameModels => Array.Empty<GameModelDecl>();

    public virtual IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Array.Empty<MissionBehaviorDecl>();

    /// <summary>Non-patch side effects for a phase (hotkeys, watchdog starts), after that phase's categories.</summary>
    public virtual void OnPhase(ApplyPhase phase, IResolver resolver) { }
}
