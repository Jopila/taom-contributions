using System;
using System.Collections.Generic;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.Encyclopedia.Hooks;

/// <summary>
/// Answers vanilla <c>HeroHelper.GetLastSeenText</c> for the one state where it would throw a
/// <see cref="NullReferenceException"/> (player crash 9b2a103e), and leaves every other call to vanilla.
///
/// WHY: v1.5.x rewrote the method. Its prisoner, army and at-sea branches read
/// <c>partyBase.LeaderHero.EncyclopediaLinkWithName</c> and
/// <c>Army.LeaderParty.LeaderHero.EncyclopediaLinkWithName</c> with no null check
/// (v1.5.5 HeroHelper.cs:53, 58, 65, 80, 86, 98); v1.4.8 read only the settlement.
/// <c>PartyBase.LeaderHero</c> is <c>MobileParty?.LeaderHero</c>, and vanilla
/// <c>MapEvent.CaptureDefeatedPartyMembers</c> gives a captured lord to any winner the battle reward
/// model lists, bandits included. So the page of a lord the player knows, with a last-known settlement,
/// held by a leaderless party or in an army whose leading party has no leader, throws. No TAOM patch is
/// on the throwing frames (GetLastSeenText, EncyclopediaHeroPageVM.Refresh).
///
/// A prefix, not a finalizer: a finalizer cannot tell the vanilla NRE from one thrown by another
/// owner's prefix, postfix or transpiler, or by a callee the JIT inlines, because they all throw in the
/// same replacement frame (Codex 2026-10-26 proved each). The prefix reads the state first
/// (<see cref="LastSeenState.ReachesUnguardedLeaderRead"/>), returns the v1.4.8 "last seen near X" text
/// and skips the original only in that state; nothing is swallowed. The only caller,
/// <c>EncyclopediaHeroPageVM.Refresh</c>, calls <c>.ToString()</c> on the result, so the text is never
/// null: "last seen", else "never seen", else empty.
/// </summary>
[HarmonyPatch(typeof(HeroHelper), nameof(HeroHelper.GetLastSeenText))]
[HarmonyPatchCategory(PatchCategory)]
public static class HeroHelper_GetLastSeenText_Patch
{
    public const string PatchCategory = "Patch123_LastSeenTextGuard";

    private static readonly HashSet<string> Reported = new();
    private static IModLogger? _logger;

    public static void Initialize(IModLogger? logger)
    {
        _logger = logger;
        Reported.Clear();
    }

    [HarmonyPrefix]
    public static bool Prefix(Hero? hero, ref TextObject __result) =>
        Decide(LastSeenState.Capture(hero), () => hero?.LastKnownClosestSettlement?.EncyclopediaLinkWithName,
            ref __result);

    /// <summary>False (skip vanilla) with the fallback text only when vanilla would read a missing leader.
    /// Never throws: an exception leaving a prefix would end the game the same way.</summary>
    internal static bool Decide(LastSeenState state, Func<TextObject?> readSettlementLink, ref TextObject result)
    {
        if (!state.ReachesUnguardedLeaderRead) return true;

        var (text, kind) = FallbackText(ReadLink(readSettlementLink), state.IsInSettlement);
        Report(state, kind);
        result = text;
        return false;
    }

    internal static (TextObject Text, string Kind) FallbackText(TextObject? settlementLink, bool isInSettlement)
    {
        try
        {
            if (settlementLink == null)
                return (GameTexts.FindText("str_never_seen_encyclopedia_entry"), "never seen");

            var text = GameTexts.FindText("str_last_seen_encyclopedia_entry");
            text.SetTextVariable("SETTLEMENT", settlementLink);
            text.SetTextVariable("IS_IN_SETTLEMENT", isInSettlement ? 1 : 0);
            return (text, "last seen");
        }
        catch
        {
            return (TextObject.GetEmpty(), "empty");
        }
    }

    // The link is formatting, not classification: a failed read degrades the text, never the containment.
    private static TextObject? ReadLink(Func<TextObject?> read)
    {
        try { return read(); }
        catch { return null; }
    }

    private static void Report(LastSeenState state, string kind)
    {
        try
        {
            if (!Reported.Add(state.HeroId)) return;

            _logger?.LogWarning(
                $"[LastSeenGuard] vanilla GetLastSeenText would read a missing leader for hero '{state.HeroId}': " +
                $"prisoner={state.IsPrisoner} party='{state.PartyId}' mobile={state.PartyIsMobile} " +
                $"partyLeaderMissing={state.PartyLeaderMissing} " +
                $"army={(state.HasArmy ? state.ArmyLeaderMissing ? "leaderless" : "led" : "none")}; shown: {kind}");
        }
        catch
        {
            // Diagnostic only: a failed log line must never reach the caller.
        }
    }
}
