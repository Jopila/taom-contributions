using TAOM.Adapters;
using TAOM.Core.Validation;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp.Hooks;

namespace TAOM.Features.FieldCamp.Activities;

/// <summary>What the healing model adds for one healing roll: the bonus, and whether the two lodging perks follow it.</summary>
public readonly struct CampRestGrant
{
    public CampRestGrant(float bonus, bool lodgingPerks)
    {
        Bonus = bonus;
        LodgingPerks = lodgingPerks;
    }

    /// <summary>A finite amount above 0, or 0 for none.</summary>
    public float Bonus { get; }

    /// <summary>A Good Day's Rest and Good Lodging count in camp: true while resting and not in a settlement (vanilla applies them there).</summary>
    public bool LodgingPerks { get; }
}

/// <summary>
/// Rest healing in camp (#860): the extra daily healing a resting party gets, and when. The party rests while the player's camp
/// stands and its raise is done (<see cref="ICampWorkAdapter.CampReady"/>) and the camp panel (<c>taom_fc_camp</c>) is the open
/// menu, with the feature on, the campaign single-player and at least one rate above 0 (both at 0 turn rest healing fully off,
/// lodging perks included). Any camp type that is raised rests. The work menus are not the camp panel, so a party at work does not
/// rest. It has no tick of its own: <c>TaomPartyHealingModel</c> reads the engine facts on every healing roll the engine makes and
/// asks <see cref="Grant"/>. The rates are validated here again (a stale value or a NaN adds nothing).
/// </summary>
public class CampRestHealing
{
    private readonly ICampSettingsProvider _settings;
    private readonly ICampWorkAdapter _work;
    private readonly IGameMenuAdapter _menus;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;

    public CampRestHealing(ICampSettingsProvider settings, ICampWorkAdapter work, IGameMenuAdapter menus,
        ICoopSessionProvider coop, IDedicatedServerProvider dedicated)
    {
        _settings = settings;
        _work = work;
        _menus = menus;
        _coop = coop;
        _dedicated = dedicated;
    }

    /// <summary>The party is resting right now: a raised camp, its panel open, the feature on, single-player, and at least one rate above 0.</summary>
    public bool IsResting() => CanRest() && _menus.CurrentMenuId == FieldCampCampaignBehavior.CampSubMenuId;

    /// <summary>The healing added per day while resting, for the troops or for the heroes; 0 when not resting or the rate is not a positive finite number.</summary>
    public float DailyBonus(bool forHeroes) =>
        IsResting() ? Rate(forHeroes ? _settings.CampRestHealHeroesPerDay : _settings.CampRestHealRegularsPerDay) : 0f;

    /// <summary>
    /// The one question the healing model asks, with the engine facts it read: only the main party rests, never prisoners, never a
    /// starving party (vanilla's starving case stands), and only while <see cref="IsResting"/>. The lodging perks follow unless the
    /// party is in a settlement, where vanilla has applied them already, so no perk is counted twice.
    /// </summary>
    public CampRestGrant Grant(bool isMainParty, bool isPrisoners, bool isStarving, bool inSettlement, bool forHeroes)
    {
        if (!isMainParty || isPrisoners || isStarving || !IsResting())
            return default;
        return new CampRestGrant(DailyBonus(forHeroes), lodgingPerks: !inSettlement);
    }

    /// <summary>
    /// The two rates the camp panel shows, or null when a camp here would heal nobody extra (the feature off, a shared campaign, no
    /// camp or one still being raised, both rates 0). Shown whatever menu is open: the panel draws it, so it is open by definition.
    /// </summary>
    public (float Regulars, float Heroes)? StatusRates() =>
        CanRest() ? (Rate(_settings.CampRestHealRegularsPerDay), Rate(_settings.CampRestHealHeroesPerDay)) : null;

    // CampReady() is true only for a standing camp whose raise is done, so no separate "a camp exists" check is needed.
    private bool CanRest() =>
        _settings.Enabled && CampGate.IsSinglePlayer(_coop, _dedicated) && _work.CampReady()
        && (Rate(_settings.CampRestHealRegularsPerDay) > 0f || Rate(_settings.CampRestHealHeroesPerDay) > 0f);

    private static float Rate(float value) => FiniteFloatValidator.IsFinite(value) && value > 0f ? value : 0f;
}
