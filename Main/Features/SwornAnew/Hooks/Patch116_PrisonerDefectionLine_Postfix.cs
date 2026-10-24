using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.SwornAnew.Hooks;

/// <summary>
/// Patch116, #869: postfix on the private <c>LordDefectionCampaignBehavior.conversation_player_start_defection_with_prisoner_on_condition()</c>
/// (v1.5.5), the condition of the prisoner line "I have an offer for you: join us, and be set free." Vanilla's condition never
/// asks <c>DefectionModel</c>, so without this the line would offer a Gondor lord a place in Mordor while the diplomacy option, which
/// does ask the model, refused it. When vanilla offers the line, the full service verdict that
/// <c>TaomDefectionModel</c> uses is applied: the postfix can only take the line away, on every peer (it hides a line locally; the
/// verdict is not deferred to a co-op host). A free hero and the switch off leave vanilla's answer alone, and a fault keeps it. The
/// line shows for a prisoner of the player's party and for one in a dungeon of the player's clan. The only caller of the target is
/// the condition delegate of the line registered in <c>AddLordDefectionPersuasionOptions</c>, from the conversation system on the
/// main thread.
/// Notes: docs/reference/harmony-patch-registry.md "Patch116_SwornAnew".
/// </summary>
[HarmonyPatchCategory(SwornAnewModule.PatchCategory)]
public static class Patch116_PrisonerDefectionLine_Postfix
{
    private const string ConditionName = "conversation_player_start_defection_with_prisoner_on_condition";

    private static SwornAnewService? _service;
    private static ISwornAnewAdapter? _adapter;
    private static IModLogger? _logger;

    public static void Initialize(SwornAnewService service, ISwornAnewAdapter adapter, IModLogger logger)
    {
        _service = service;
        _adapter = adapter;
        _logger = logger;
    }

    public static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(LordDefectionCampaignBehavior), ConditionName, Type.EmptyTypes);

    [HarmonyPostfix]
    public static void Postfix(ref bool __result)
    {
        var service = _service;
        var adapter = _adapter;
        if (!__result || service == null || adapter == null)
            return;

        try
        {
            if (service.HidesPrisonerLine(adapter.ReadConversationPartner()))
                __result = false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[SwornAnew] Patch116 postfix failed, keeping vanilla's answer: {ex}");
        }
    }
}
