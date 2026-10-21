using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TAOM.Adapters;

namespace TAOM.Features.Execution.Hooks;

/// <summary>
/// Suppresses the trait penalty for starting a blood feud when the player executes a lord of an
/// ENEMY alignment, so putting an orc captain to the sword does not cost the same Honor as
/// murdering a fellow man.
///
/// <para>
/// v1.5.0 deleted <c>TraitLevelingHelper.OnLordExecuted()</c>, which took no arguments and was the
/// whole reason the old ThreadLocal <c>ExecutionContext</c> snapshot existed. Its successor,
/// <c>OnBloodFeudStarted(Hero)</c>, is called from
/// <c>ExecutionCampaignBehavior.OnPlayerExecutedHero</c> with the victim as a parameter and the
/// player as the only possible executor. Vanilla gates that call on the victim's clan not already
/// feuding with the player, so the penalty is now once per clan rather than once per execution.
/// That is vanilla's behaviour, not ours.
/// </para>
///
/// <para>
/// The victim's clan may already have been destroyed by the kill when this runs:
/// <c>KillCharacterAction.ApplyInternal</c> destroys it before dispatching <c>OnHeroKilled</c>,
/// which nulls its kingdom. That is why the participant carries the culture as well: the clan is then
/// its own map faction with its own culture. The alignment service reads a listed clan's row first and
/// falls back to that culture only for an unlisted clan.
/// There is deliberately no "unknown, defer to vanilla" early return.
/// </para>
///
/// <para>Only the TRAIT hit is suppressed. The feud itself still starts, whatever the sides.</para>
/// </summary>
[HarmonyPatch(typeof(TraitLevelingHelper), nameof(TraitLevelingHelper.OnBloodFeudStarted))]
[HarmonyPatchCategory("Patch14_Execution")]
public static class TraitLevelingHelper_OnBloodFeudStarted_Patch
{
    private static IOnExecutionAction _hook;

    public static void Initialize(IOnExecutionAction hook)
    {
        _hook = hook;
    }

    [HarmonyPrefix]
    public static bool Prefix(Hero executedHero)
    {
        var hook = _hook;
        if (hook == null) return true;

        // Boundary: sealed engine types to one id and culture pair per entity, no early return.
        return hook.ShouldApplyHonorPenalty(FactionSides.OfHero(executedHero), FactionSides.OfPlayer());
    }
}
