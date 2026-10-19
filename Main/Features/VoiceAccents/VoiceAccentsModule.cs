using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Composition;
using TAOM.Features.VoiceAccents.Models;

namespace TAOM.Features.VoiceAccents;

/// <summary>
/// Voice Accents (#821, docs/features/voice-accents.md): maps TAOM cultures to fitting vanilla dialogue
/// accents and silences creatures, through a campaign <c>VoiceOverModel</c>. No patch, behavior or save data.
/// </summary>
internal sealed class VoiceAccentsModule : TaomFeatureModule
{
    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<VoiceOverModel, TaomVoiceOverModel>(ModelTarget.Campaign,
            resolver => new TaomVoiceOverModel(resolver.Resolve<VoiceAccentService>())),
    };

    public override string Id => "VoiceAccents";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IVoiceAccentsConfigProvider, VoiceAccentsConfigProvider>(Reuse.Singleton);
        registrator.Register<VoiceAccentService>(Reuse.Singleton);
    }

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
