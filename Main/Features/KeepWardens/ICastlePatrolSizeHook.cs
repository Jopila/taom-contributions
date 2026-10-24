using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.KeepWardens;

/// <summary>
/// The narrow seam <c>TaomPartySizeModel</c> reads for castle patrols (#888). Vanilla's patrol size limit reads only the town
/// Guard House and answers 0 for a castle, so a castle patrol would show "25/0" and never refill.
/// </summary>
public interface ICastlePatrolSizeHook
{
    /// <summary>
    /// For a patrol party whose home is a castle, when <paramref name="result"/> came back as 0, replaces it with the
    /// configured limit for that castle's Guard House level. Any other party, or a non-zero base, is left untouched. Ignores
    /// the MCM switch: a live patrol must never drop to a limit of 0.
    /// </summary>
    void ApplyCastlePatrolSize(PartyBase? party, ref ExplainedNumber result, bool includeDescriptions);
}
