using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.HardQuestions.Hooks;

namespace TAOM.Features.HardQuestions;

/// <summary>
/// Hard Questions (docs/features/hard-questions.md) as a feature module: a service graph and two campaign
/// behaviors, no patch and no model. Both behaviors are built with <c>new</c> for every campaign, because each
/// resets per-campaign state in its constructor and a container singleton is constructed once per process.
/// The campaign behavior saves the used topics in SyncData.
/// </summary>
internal sealed class HardQuestionsModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new HardQuestionsCampaignBehavior(
            r.Resolve<HardQuestionsState>(),
            r.Resolve<StripService>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IModLogger>())),
        CampaignBehaviorDecl.Of(r => new HardQuestionsDialogBehavior(
            r.Resolve<InterrogationService>(),
            r.Resolve<ICaptiveLordAdapter>())),
    };

    public override string Id => "HardQuestions";

    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) =>
        HardQuestionsIoC.RegisterHardQuestionsFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
