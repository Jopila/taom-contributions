using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Features.FieldCamp.Activities;

/// <summary>
/// The decisions of camp work (#860, docs/features/field-camp.md "Camp work"): whether the men may be set to hunt, fell timber or
/// fish, how far along the saved work is, what a tick adds, the one injury roll at the midpoint, and the payout. The work lives in
/// the camp's own <see cref="CampState"/> (fields 108 to 111), so it is saved with the camp and breaking the camp takes it along.
/// Every engine read and write goes through <see cref="ICampWorkAdapter"/>, every number through <see cref="CampActivityRules"/>.
/// Single-player only (the Far Watch predicate). Main thread; a process singleton with no state of its own.
/// </summary>
internal sealed class CampActivityService
{
    private readonly ICampService _camps;
    private readonly ICampSettingsProvider _settings;
    private readonly ICampWorkAdapter _work;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IModLogger _logger;

    public CampActivityService(ICampService camps, ICampSettingsProvider settings, ICampWorkAdapter work, ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated, IModLogger logger)
    {
        _camps = camps;
        _settings = settings;
        _work = work;
        _coop = coop;
        _dedicated = dedicated;
        _logger = logger;
    }

    /// <summary>The camp has a saved work, whatever its progress.</summary>
    internal bool HasWork => _camps.PlayerCamp is { } camp && camp.Activity != (int)CampActivity.None;

    /// <summary>
    /// The Field Camps master toggle. Off, no work starts and a saved one ends the way a Far Watch ride does: the next
    /// <see cref="Current"/>, <see cref="Advance"/> or <see cref="Complete"/> clears it, pays nothing and lets the wait land.
    /// </summary>
    internal bool Enabled => _settings.Enabled;

    /// <summary>
    /// Read live on every use (ICoopSessionProvider's contract). Stands down while a co-op session is live, while a co-op mod
    /// TAOM cannot probe is loaded, on a dedicated server and on a peer that may not write the world.
    /// </summary>
    private bool IsSinglePlayer => CampGate.IsSinglePlayer(_coop, _dedicated);

    // --- The offer ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// What stops any work before it is chosen, in this order: the master toggle off, a shared campaign, an encounter or battle, a camp that is not a
    /// raised field or fortified camp, a Far Watch ride out. None when the men can be set to something.
    /// </summary>
    internal CampWorkBlock GlobalBlock(CampWorkFacts facts)
    {
        if (!_settings.Enabled) return CampWorkBlock.FeatureDisabled;
        if (!IsSinglePlayer) return CampWorkBlock.SharedCampaign;
        if (facts.Busy) return CampWorkBlock.Busy;
        var camp = _camps.PlayerCamp;
        if (camp == null || !facts.CampReady || !IsWorkCamp(camp)) return CampWorkBlock.CampNotReady;
        if (facts.ScoutRideOut) return CampWorkBlock.ScoutRideOut;
        return CampWorkBlock.None;
    }

    /// <summary>The global block for the camp as it is now (one engine pass).</summary>
    internal CampWorkBlock GlobalBlock() => GlobalBlock(_work.Read());

    /// <summary>Can the men start this work now, and if not, why. Writes nothing.</summary>
    internal CampWorkOffer GetOffer(CampActivity activity)
    {
        var facts = _work.Read();
        var block = GlobalBlock(facts);
        if (block != CampWorkBlock.None) return new CampWorkOffer(block);
        if (HasWork) return new CampWorkOffer(CampWorkBlock.AlreadyWorking);
        if (activity == CampActivity.None) return new CampWorkOffer(CampWorkBlock.NothingHere);
        if (facts.HealthyMen <= 0) return new CampWorkOffer(CampWorkBlock.NoMen);

        bool water = activity == CampActivity.Fish && _work.WaterNearby();
        if (!(CampActivityRules.GroundFactor(activity, facts.TerrainType, water) > 0f)) return new CampWorkOffer(CampWorkBlock.NothingHere);

        int tools = CampActivityRules.ToolsNeeded(facts.HealthyMen);
        if (tools > facts.ToolsHeld) return new CampWorkOffer(CampWorkBlock.NoTools, tools);
        return new CampWorkOffer(CampWorkBlock.None, tools);
    }

    /// <summary>
    /// Sets the men to the work when <see cref="GetOffer"/> answers ready (one method, never a copy of its checks); whatever it
    /// answers is returned. The target hours are a snapshot of the setting, so changing it mid-work moves nothing.
    /// </summary>
    internal CampWorkOffer TryStart(CampActivity activity)
    {
        var offer = GetOffer(activity);
        if (!offer.Ready) return offer;

        var camp = _camps.PlayerCamp!;
        float hours = _settings.CampActivityHours;
        if (!FiniteFloatValidator.IsFinite(hours) || !(hours > 0f)) hours = CampSettingsProvider.DefaultActivityHours;
        camp.Activity = (int)activity;
        camp.ActivityHoursDone = 0f;
        camp.ActivityTargetHours = hours;
        camp.InjuryRolled = false;
        return offer;
    }

    // --- The work ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// How far along the saved work is. Cheap: the wait menu's init and the load ask it. It writes only to end a work that cannot
    /// run: with the master toggle off it clears the work and answers not running.
    /// </summary>
    internal CampWorkProgress Current()
    {
        var camp = _camps.PlayerCamp;
        if (camp == null || camp.Activity == (int)CampActivity.None) return default;
        if (!_settings.Enabled)
        {
            camp.ClearActivity();
            return default;
        }

        if (!CampActivityRules.IsSane(camp.Activity, camp.ActivityHoursDone, camp.ActivityTargetHours)) return default;
        return Progress(camp, 0);
    }

    /// <summary>
    /// With the master toggle off or in a shared campaign the work is cleared and nothing else happens (see <see cref="Enabled"/>;
    /// the shared case also sets <see cref="CampWorkProgress.EndedBySharedCampaign"/>). Otherwise adds the ticked hours (a delta that is not a positive finite number is
    /// ignored) and makes the midpoint injury roll once.
    /// A record that cannot run is cleared and logged. At fraction 1 the work stays saved: the engine ends the wait and the menu
    /// calls <see cref="Complete"/>.
    /// </summary>
    internal CampWorkProgress Advance(double deltaHours)
    {
        var camp = _camps.PlayerCamp;
        if (camp == null || camp.Activity == (int)CampActivity.None) return default;
        if (!_settings.Enabled)
        {
            camp.ClearActivity();
            return default;
        }

        // A work carried into a shared campaign ends here, before any roll or write: cleared, the wait lands, and the progress says
        // why so the wait menu can print the line.
        if (!IsSinglePlayer)
        {
            camp.ClearActivity();
            return new CampWorkProgress(CampActivity.None, 0f, 0d, 0, endedBySharedCampaign: true);
        }

        if (!CampActivityRules.IsSane(camp.Activity, camp.ActivityHoursDone, camp.ActivityTargetHours))
        {
            _logger.LogWarning("[FieldCamp] cleared a camp work record that could not run");
            camp.ClearActivity();
            return default;
        }

        float before = CampActivityRules.Fraction(camp.ActivityHoursDone, camp.ActivityTargetHours);
        if (FiniteFloatValidator.IsFinite(deltaHours) && deltaHours > 0d)
        {
            double sum = camp.ActivityHoursDone + deltaHours;
            camp.ActivityHoursDone = sum >= camp.ActivityTargetHours ? camp.ActivityTargetHours : (float)sum;
        }

        float after = CampActivityRules.Fraction(camp.ActivityHoursDone, camp.ActivityTargetHours);
        int injured = 0;
        if (CampActivityRules.InjuryRollDue(before, after, camp.InjuryRolled))
        {
            camp.InjuryRolled = true;
            if (CampActivityRules.InjuryHits(_work.NextRandom()))
            {
                int hit = CampActivityRules.InjuredMen(_work.Read().HealthyRegulars);
                if (hit > 0) injured = _work.WoundRegulars(hit);
            }
        }

        return Progress(camp, injured);
    }

    /// <summary>
    /// The work is done: pays it. Idempotent: with no work it does nothing, so a second call cannot pay again. The work is CLEARED
    /// before anything is written to the world, so a throw in a roster write cannot leave a work that pays twice. The goods come
    /// from the men and the ground as they are now, the tools are taken up to what is held, and the work COSTS morale by the men and
    /// the clan's tier. The master toggle off, or a shared campaign, drops the work and pays nothing.
    /// </summary>
    internal CampWorkReport? Complete()
    {
        var camp = _camps.PlayerCamp;
        if (camp == null || camp.Activity == (int)CampActivity.None) return null;

        var activity = (CampActivity)camp.Activity;
        camp.ClearActivity();
        if (!_settings.Enabled || !IsSinglePlayer) return null;

        var facts = _work.Read();
        bool water = activity == CampActivity.Fish && _work.WaterNearby();
        float factor = CampActivityRules.GroundFactor(activity, facts.TerrainType, water);
        int goods = CampActivityRules.Yield(_settings.CampActivityYieldPerTenMen, _settings.CampActivityYieldCap, facts.HealthyMen, factor);
        int paid = goods > 0 ? _work.AddGoods(activity, goods) : 0;

        int tools = System.Math.Min(CampActivityRules.ToolsNeeded(facts.HealthyMen), facts.ToolsHeld);
        if (tools > 0) _work.TakeTools(tools);

        float moraleCost = CampActivityRules.MoraleCost(facts.HealthyMen, facts.ClanTier);
        if (moraleCost > 0f) _work.LowerMorale(moraleCost);

        return new CampWorkReport(activity, paid, moraleCost, tools);
    }

    /// <summary>Discards the work and its progress; false when there was none. The Stop button and a fault both end here.</summary>
    internal bool Stop()
    {
        var camp = _camps.PlayerCamp;
        if (camp == null || camp.Activity == (int)CampActivity.None) return false;
        camp.ClearActivity();
        return true;
    }

    private static bool IsWorkCamp(CampState camp) => camp.TypeEnum == CampType.Field || camp.TypeEnum == CampType.Fortified;

    private static CampWorkProgress Progress(CampState camp, int injured) =>
        new((CampActivity)camp.Activity, CampActivityRules.Fraction(camp.ActivityHoursDone, camp.ActivityTargetHours),
            camp.ActivityTargetHours, injured);
}
