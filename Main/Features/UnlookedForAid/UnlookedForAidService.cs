using TAOM.Core.Validation;

namespace TAOM.Features.UnlookedForAid;

/// <summary>
/// Help Unlooked-For (#872, docs/features/unlooked-for-aid.md): the pure decision behind <c>TaomMobilePartyAIModel</c>.
/// <para>
/// <b>Threading.</b> The engine asks this on a background AI worker and on the main thread. So <see cref="Decide"/>
/// reads no MCM, resolves nothing through IoC, logs nothing and rolls no random number. The only state is the strength
/// ratio, loaded and validated once in the constructor (which runs on the main thread, when the model is built in
/// <c>OnGameStart</c>), and one bool read through <see cref="IUnlookedForAidSettingsProvider"/>, which captured the MCM
/// settings reference on that same thread.
/// </para>
/// </summary>
public sealed class UnlookedForAidService
{
    private readonly IUnlookedForAidSettingsProvider _settings;
    private readonly float _minStrengthRatio;

    public UnlookedForAidService(IUnlookedForAidSettingsProvider settings, IUnlookedForAidConfigProvider config)
    {
        _settings = settings;
        _minStrengthRatio = config.MinStrengthRatio;
    }

    /// <summary>
    /// Whether the lord still avoids the target, given that vanilla's rule already said it should: true keeps vanilla's
    /// avoid, false lifts it. Lifted only for an AI lord facing a besieger it is strong enough for, or a party shut inside
    /// a besieged settlement, and never at a siege the player leads.
    /// </summary>
    public bool Decide(SiegeAvoidanceFacts facts)
    {
        // Struct checks first: most questions end here without reading the switch. The player's own party never reaches
        // this call: ShouldPartyCheckInitiativeBehavior refuses MobileParty.MainParty (DefaultMobilePartyAIModel.cs:94),
        // which gates the only path (MobilePartyAi.cs:483-485, then DefaultMobilePartyAIModel.cs:382 and :518).
        if (!facts.IsLordParty || facts.PlayerLedSiege || !(facts.TargetIsBesieger || facts.TargetInBesiegedSettlement))
            return true;
        if (!_settings.Enabled)
            return true;

        if (facts.TargetIsBesieger)
            return DecideAgainstBesieger(facts);

        // Parties shut inside a besieged settlement never take initiative (DefaultMobilePartyAIModel.cs:151 skips every
        // target for a party whose settlement is under siege), so there is nothing to flee.
        return false;
    }

    // NaN (an unread or torn strength) and infinity keep vanilla's answer.
    private bool DecideAgainstBesieger(SiegeAvoidanceFacts facts)
    {
        if (!FiniteFloatValidator.IsFinite(facts.OwnStrength) || !FiniteFloatValidator.IsFinite(facts.CampStrength))
            return true;
        if (facts.CampStrength <= 0f)
            return false;
        return !(facts.OwnStrength / facts.CampStrength >= _minStrengthRatio);
    }
}
