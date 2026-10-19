using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Features.BondsOfBattle.Hooks;

/// <summary>
/// What the prefix read before vanilla's Done reset it. The default value (no lord) means "do nothing". The two flags are
/// facts about the lord; <see cref="TroopHandoverService"/> decides what they mean.
/// </summary>
public readonly record struct HandoverCapture(string? LordHeroId, string LordName, int Influence, bool LordIsPlayerClan, bool LordIsPlayerHero);

/// <summary>
/// Patch106, the troop hand-over (docs/features/bonds-of-battle.md). <c>PartyScreenLogic.DoneLogic(bool)</c> is public
/// (v1.5.5). On success vanilla pays the influence in <c>CurrentData.PartyInfluenceChangeAmount.Item2</c> and then resets
/// it to 0, so the prefix keeps the lord and that number in Harmony's <c>__state</c> and the postfix acts only when the
/// call returned true. The influence is non-zero only on a donating screen (Donate Troops to another clan's party, or
/// donate to a garrison); only Donate Troops has a left owner party, the receiving party's own, so the donate-to-garrison
/// screens capture no lord. Every actor comes from the screen's own left party, nothing is inferred. A second Done after
/// success reads 0. The state is Harmony's, so a stripped prefix leaves the default and the postfix does nothing. Both halves
/// swallow and log their own faults: a relation bonus must never keep a party screen from closing.
/// </summary>
[HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.DoneLogic))]
[HarmonyPatchCategory(BondsOfBattleModule.PatchCategory)]
public static class PartyScreenLogic_DoneLogic_Patch
{
    private static TroopHandoverService? _service;
    private static IModLogger? _logger;

    [HarmonyPrefix]
    public static void Prefix(PartyScreenLogic __instance, out HandoverCapture __state)
    {
        __state = default;
        try
        {
            __state = Capture(__instance);
        }
        catch (Exception ex)
        {
            LogFault("prefix", ex);
        }
    }

    [HarmonyPostfix]
    public static void Postfix(bool __result, HandoverCapture __state)
    {
        if (!__result || __state.Influence <= 0 || string.IsNullOrEmpty(__state.LordHeroId))
            return;

        try
        {
            Apply(__state);
        }
        catch (Exception ex)
        {
            LogFault("postfix", ex);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static HandoverCapture Capture(PartyScreenLogic screen)
    {
        var influence = screen.CurrentData?.PartyInfluenceChangeAmount.Item2 ?? 0;
        if (influence <= 0)
            return default;

        var lord = screen.LeftOwnerParty?.MobileParty?.LeaderHero;
        if (lord == null)
            return default;

        return new HandoverCapture(lord.StringId, lord.Name?.ToString() ?? string.Empty, influence,
            lord.Clan == Clan.PlayerClan, lord == CharacterObject.PlayerCharacter?.HeroObject);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Apply(HandoverCapture capture) =>
        (_service ??= TAOM.IoC.Resolve<TroopHandoverService>()).OnHandover(
            capture.LordHeroId, capture.LordName, capture.Influence, capture.LordIsPlayerClan, capture.LordIsPlayerHero);

    private static void LogFault(string half, Exception ex)
    {
        try
        {
            (_logger ??= TAOM.IoC.Resolve<IModLogger>()).LogError($"[BondsOfBattle] Patch106 {half} failed: {ex.Message}");
        }
        catch
        {
            // The log is gone too; the party screen still closes.
        }
    }
}
