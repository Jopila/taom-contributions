using System.Collections.Generic;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem;

public interface ICareerConfigProvider
{
    IReadOnlyList<CareerDefinition> LoadCareers();
    IReadOnlyList<CareerChoiceGroupDefinition> LoadChoiceGroups();
    IReadOnlyList<CareerChoiceDefinition> LoadChoices();
    int GetMaxPerkPoints();

    /// <summary>#769 E7: flat part of the gold price of a retrain or a fork-holding career switch.</summary>
    int GetRetrainGoldBase();

    /// <summary>#769 E7: the per-hero-level part of that price.</summary>
    int GetRetrainGoldPerLevel();
    AbilityTemplateData GetAbilityTemplate(string templateId);
    AbilityTuningConfig GetAbilityTuning();
}
