using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public class CareerSwitchService
{
    private readonly ICareerDataService _dataService;
    private readonly ICareerRegistry _registry;
    private readonly ICareerCreationHandler _creationHandler;
    private readonly ICareerPurseAdapter _purse;
    private readonly IModLogger _logger;

    public CareerSwitchService(
        ICareerDataService dataService,
        ICareerRegistry registry,
        ICareerCreationHandler creationHandler,
        ICareerPurseAdapter purse,
        IModLogger logger)
    {
        _dataService = dataService;
        _registry = registry;
        _creationHandler = creationHandler;
        _purse = purse;
        _logger = logger;
    }

    public bool CanSwitch(ICareerHeroAdapter hero, string newCareerStringId)
    {
        if (hero == null || string.IsNullOrEmpty(newCareerStringId))
        {
            _logger.LogDebug($"CareerSystem: CanSwitch — null hero or empty careerId '{newCareerStringId}'");
            return false;
        }

        // Reject switching to the hero's current career. The picker filters this out via
        // GetEligibleSwitchTargets, but this is the boundary defense -- a future caller invoking
        // SwitchCareer directly must not silently "succeed" into a no-op cache refresh.
        var heroId = hero.StringId;
        if (!string.IsNullOrEmpty(heroId))
        {
            var currentCareerId = _dataService.GetCareerStringId(heroId);
            if (!string.IsNullOrEmpty(currentCareerId) &&
                string.Equals(currentCareerId, newCareerStringId, System.StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug($"CareerSystem: CanSwitch — rejecting same-career switch '{newCareerStringId}' for hero '{heroId}'");
                return false;
            }
        }

        var eligible = _registry.IsEligible(newCareerStringId, hero);
        _logger.LogDebug($"CareerSystem: CanSwitch — hero culture='{hero.CultureStringId}' career='{newCareerStringId}' result={eligible}");
        return eligible;
    }

    /// <summary>
    /// #769 E7: the gold <see cref="SwitchCareer"/> will charge. Leaving a career that holds fork picks
    /// costs the registry's retrain price; leaving any other career is free.
    /// </summary>
    public int GetSwitchCost(string heroStringId, int heroLevel)
    {
        var careerId = _dataService.GetCareerStringId(heroStringId);
        var career = string.IsNullOrEmpty(careerId) ? null : _registry.GetCareer(careerId);
        if (career == null) return 0;

        return CareerChoiceRules.HoldsForkPick(career, _registry, _dataService.GetOrCreateData(heroStringId))
            ? _registry.GetRetrainCost(heroLevel)
            : 0;
    }

    /// <summary>
    /// Clears the hero's old career and assigns the new one through the character-creation handler
    /// (set, stamp the tree version, seed the root, refresh the cache). The charge comes first, so an
    /// ineligible target or a short purse leaves the old career untouched.
    /// </summary>
    public CareerSwitchResult SwitchCareer(string heroStringId, ICareerHeroAdapter hero, string newCareerStringId)
    {
        if (!CanSwitch(hero, newCareerStringId))
        {
            _logger.LogWarning($"CareerSwitch: hero '{heroStringId}' not eligible for '{newCareerStringId}'");
            return CareerSwitchResult.NotEligible;
        }

        // Charge before anything changes, so a short purse leaves the old career untouched.
        var cost = GetSwitchCost(heroStringId, hero.Level);
        if (cost > 0 && !_purse.ChargeGold(cost))
        {
            _logger.LogWarning($"CareerSwitch: hero '{heroStringId}' cannot afford the {cost} gold to leave their career");
            return CareerSwitchResult.NotEnoughGold;
        }

        var oldCareer = _dataService.GetCareerStringId(heroStringId);

        _logger.LogInfo($"CareerSystem: Clearing old career '{oldCareer}' for hero '{heroStringId}'");
        _dataService.ClearCareer(heroStringId);
        _creationHandler.OnCareerSelected(heroStringId, newCareerStringId);

        _logger.LogInfo($"CareerSwitch: hero '{heroStringId}' switched from '{oldCareer}' to '{newCareerStringId}'");
        return CareerSwitchResult.Switched;
    }
}
