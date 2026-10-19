using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace TAOM.Features.VoiceAccents.Models;

/// <summary>
/// Dialogue voice selection (docs/features/voice-accents.md). The base model's <c>GetAccentClass</c>
/// returns "" for every non-vanilla culture, which turns the last regex tier of
/// <c>GetSoundPathForCharacter</c> into <c>.+_.+</c> and plays a random vanilla voice. The accent
/// is overridden here (the base reads it back through this model), and silence is returned before
/// the base runs. The speaker's race is in scope while the base runs, so a race row can set the accent.
/// Every decision is the service's.
/// </summary>
public class TaomVoiceOverModel : DefaultVoiceOverModel
{
    private readonly VoiceAccentService _service;

    public TaomVoiceOverModel(VoiceAccentService service)
    {
        _service = service;
    }

    public override string GetAccentClass(CultureObject culture, bool isHighClass)
    {
        return _service.ResolveAccent(culture?.StringId, isHighClass) ?? base.GetAccentClass(culture, isHighClass);
    }

    public override string GetSoundPathForCharacter(CharacterObject character, VoiceObject voiceObject)
    {
        return _service.SoundPath(character?.Culture?.StringId, character?.Race ?? -1,
            () => base.GetSoundPathForCharacter(character, voiceObject));
    }
}
