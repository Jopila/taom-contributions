using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.HardQuestions;

/// <summary>
/// Loads and validates <c>hard_questions/hard_questions.json</c>. A missing or unparseable file falls back to
/// the compiled defaults; each value failing its rule (NaN or infinity first, then range and sign) reverts to
/// its default with a warning and one summary warning follows. Cached for the process (Reuse.Singleton), so a
/// JSON edit needs a full restart.
/// </summary>
public class HardQuestionsConfigProvider : IHardQuestionsConfigProvider
{
    internal const float MaxMagnitude = 1_000_000f;
    internal const float MaxMultiplier = 1000f;

    private static HardQuestionsField F(string name, HardQuestionsFieldKind kind,
        Func<HardQuestionsConfig, float> get, Action<HardQuestionsConfig, float> set) => new(name, kind, get, set);

    internal static readonly IReadOnlyList<HardQuestionsField> Fields = new[]
    {
        F("bribeMultiplierClan", HardQuestionsFieldKind.Multiplier, c => c.BribeMultiplierClan, (c, v) => c.BribeMultiplierClan = v),
        F("bribeMultiplierKingdom", HardQuestionsFieldKind.Multiplier, c => c.BribeMultiplierKingdom, (c, v) => c.BribeMultiplierKingdom = v),
        F("calculatingBribeFactor", HardQuestionsFieldKind.Multiplier, c => c.CalculatingBribeFactor, (c, v) => c.CalculatingBribeFactor = v),
        F("charmXpClan", HardQuestionsFieldKind.Reward, c => c.CharmXpClan, (c, v) => c.CharmXpClan = v),
        F("charmXpKingdom", HardQuestionsFieldKind.Reward, c => c.CharmXpKingdom, (c, v) => c.CharmXpKingdom = v),
        F("woundChance", HardQuestionsFieldKind.Chance, c => c.WoundChance, (c, v) => c.WoundChance = v),
        F("deathChance", HardQuestionsFieldKind.Chance, c => c.DeathChance, (c, v) => c.DeathChance = v),
        F("rogueryXpClan", HardQuestionsFieldKind.Reward, c => c.RogueryXpClan, (c, v) => c.RogueryXpClan = v),
        F("rogueryXpKingdom", HardQuestionsFieldKind.Reward, c => c.RogueryXpKingdom, (c, v) => c.RogueryXpKingdom = v),
        F("forceLordRelationClan", HardQuestionsFieldKind.Penalty, c => c.ForceLordRelationClan, (c, v) => c.ForceLordRelationClan = v),
        F("forceLordRelationKingdom", HardQuestionsFieldKind.Penalty, c => c.ForceLordRelationKingdom, (c, v) => c.ForceLordRelationKingdom = v),
        F("forceFamilyRelationClan", HardQuestionsFieldKind.Penalty, c => c.ForceFamilyRelationClan, (c, v) => c.ForceFamilyRelationClan = v),
        F("forceFamilyRelationKingdom", HardQuestionsFieldKind.Penalty, c => c.ForceFamilyRelationKingdom, (c, v) => c.ForceFamilyRelationKingdom = v),
        F("forceKingRelationKingdom", HardQuestionsFieldKind.Penalty, c => c.ForceKingRelationKingdom, (c, v) => c.ForceKingRelationKingdom = v),
        F("forceMercyXpClan", HardQuestionsFieldKind.Penalty, c => c.ForceMercyXpClan, (c, v) => c.ForceMercyXpClan = v),
        F("forceMercyXpKingdom", HardQuestionsFieldKind.Penalty, c => c.ForceMercyXpKingdom, (c, v) => c.ForceMercyXpKingdom = v),
        F("forceHonorXpClan", HardQuestionsFieldKind.Penalty, c => c.ForceHonorXpClan, (c, v) => c.ForceHonorXpClan = v),
        F("forceHonorXpKingdom", HardQuestionsFieldKind.Penalty, c => c.ForceHonorXpKingdom, (c, v) => c.ForceHonorXpKingdom = v),
        F("deathExtraHonorXp", HardQuestionsFieldKind.Penalty, c => c.DeathExtraHonorXp, (c, v) => c.DeathExtraHonorXp = v),
        F("companionMercyMaxLoss", HardQuestionsFieldKind.Penalty, c => c.CompanionMercyMaxLoss, (c, v) => c.CompanionMercyMaxLoss = v),
        F("stripHonorXp", HardQuestionsFieldKind.Penalty, c => c.StripHonorXp, (c, v) => c.StripHonorXp = v),
        F("stripRenownLoss", HardQuestionsFieldKind.Penalty, c => c.StripRenownLoss, (c, v) => c.StripRenownLoss = v),
        F("stripLordRelation", HardQuestionsFieldKind.Penalty, c => c.StripLordRelation, (c, v) => c.StripLordRelation = v),
        F("stripKingdomRelation", HardQuestionsFieldKind.Penalty, c => c.StripKingdomRelation, (c, v) => c.StripKingdomRelation = v),
        F("stripHonorLordRelation", HardQuestionsFieldKind.Penalty, c => c.StripHonorLordRelation, (c, v) => c.StripHonorLordRelation = v),
        F("companionHonorMaxLoss", HardQuestionsFieldKind.Penalty, c => c.CompanionHonorMaxLoss, (c, v) => c.CompanionHonorMaxLoss = v),
        F("sameSideTraitMultiplier", HardQuestionsFieldKind.Multiplier, c => c.SameSideTraitMultiplier, (c, v) => c.SameSideTraitMultiplier = v),
        F("enemySideTraitMultiplier", HardQuestionsFieldKind.Multiplier, c => c.EnemySideTraitMultiplier, (c, v) => c.EnemySideTraitMultiplier = v),
        F("neutralTraitMultiplier", HardQuestionsFieldKind.Multiplier, c => c.NeutralTraitMultiplier, (c, v) => c.NeutralTraitMultiplier = v),
    };

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<HardQuestionsConfig> _config;

    public HardQuestionsConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<HardQuestionsConfig>(LoadConfig);
    }

    public HardQuestionsConfig GetConfig() => _config.Value;

    private HardQuestionsConfig LoadConfig()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "hard_questions", "hard_questions.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"HardQuestionsConfigProvider: hard_questions.json not found at {path}, using defaults");
            return new HardQuestionsConfig();
        }

        HardQuestionsConfig parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<HardQuestionsConfig>(File.ReadAllText(path)) ?? new HardQuestionsConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError($"HardQuestionsConfigProvider: Failed to parse hard_questions.json: {ex.Message}");
            return new HardQuestionsConfig();
        }

        return Validate(parsed);
    }

    private HardQuestionsConfig Validate(HardQuestionsConfig parsed)
    {
        var defaults = new HardQuestionsConfig();
        var rejected = false;
        foreach (var field in Fields)
        {
            var value = field.Get(parsed);
            if (IsValid(field.Kind, value))
                continue;
            var fallback = field.Get(defaults);
            _logger.LogWarning($"HardQuestionsConfigProvider: {field.Name}={value} breaks its {field.Kind} rule, reverting to default {fallback}");
            field.Set(parsed, fallback);
            rejected = true;
        }

        if (rejected)
            _logger.LogWarning("HardQuestionsConfigProvider: hard_questions.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo("HardQuestionsConfigProvider: Loaded hard_questions.json");
        return parsed;
    }

    private static bool IsValid(HardQuestionsFieldKind kind, float value) => kind switch
    {
        HardQuestionsFieldKind.Chance => FiniteFloatValidator.IsFiniteInRange(value, 0f, 1f),
        HardQuestionsFieldKind.Penalty => FiniteFloatValidator.IsFiniteInRange(value, -MaxMagnitude, 0f),
        HardQuestionsFieldKind.Reward => FiniteFloatValidator.IsFiniteInRange(value, 0f, MaxMagnitude),
        _ => FiniteFloatValidator.IsFiniteInRange(value, 0f, MaxMultiplier),
    };
}
