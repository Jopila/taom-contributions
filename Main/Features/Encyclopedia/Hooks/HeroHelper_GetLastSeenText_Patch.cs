using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;

namespace TAOM.Features.Encyclopedia.Hooks;

/// <summary>
/// Contains the vanilla <see cref="NullReferenceException"/> in <c>HeroHelper.GetLastSeenText</c>
/// (player crash 9b2a103e).
///
/// WHY: v1.5.x rewrote the method. Its prisoner, army and at-sea branches read
/// <c>partyBase.LeaderHero.EncyclopediaLinkWithName</c> and
/// <c>Army.LeaderParty.LeaderHero.EncyclopediaLinkWithName</c> with no null check
/// (v1.5.5 HeroHelper.cs:53, 58, 65, 80, 86, 98); v1.4.8 read only the settlement.
/// <c>PartyBase.LeaderHero</c> is <c>MobileParty?.LeaderHero</c>, and vanilla
/// <c>MapEvent.CaptureDefeatedPartyMembers</c> gives a captured lord to any winner the battle reward
/// model lists, bandits included. So the encyclopedia page of a known lord held by a leaderless party,
/// or in an army whose leading party has no leader, throws. No TAOM patch is on the throwing frames
/// (GetLastSeenText, EncyclopediaHeroPageVM.Refresh).
///
/// It swallows an NRE only when the NRE was thrown in the target's own body (not in another owner's
/// prefix or postfix, or a callee with its own frame) AND the hero's state reaches one of those reads
/// (<see cref="LastSeenState.ReachesUnguardedLeaderRead"/>). Everything else propagates with its throw
/// site kept. The only caller, <c>EncyclopediaHeroPageVM.Refresh</c>, calls <c>.ToString()</c> on the
/// result, so the fallback is never null: the v1.4.8 "last seen near X" text, else "never seen", else
/// empty. Priority.First puts this finalizer before PatchShield's (solo play only; the shield does not
/// install under co-op), so the exception's frames are still intact when it reads them.
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

    // Cold target (one encyclopedia page open), so binding __originalMethod costs nothing that matters
    // (lessons/harmony-il.md, "A value-returning finalizer that hands back its exception ...").
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static Exception? Finalizer(
        Exception? __exception, Hero? hero, ref TextObject __result, MethodBase? __originalMethod)
    {
        if (__exception is null) return null;
        if (__exception is NullReferenceException && ThrewInOriginal(__exception, __originalMethod))
        {
            var state = LastSeenState.Capture(hero);
            if (state.ReachesUnguardedLeaderRead)
            {
                __result = Contain(state, __exception);
                return null;
            }
        }
        return RethrowStackPreserver.PreserveForRethrow(__exception, __originalMethod);
    }

    /// <summary>True when the exception's first frame is the patched target's replacement body.</summary>
    internal static bool ThrewInOriginal(Exception exception, MethodBase? original)
    {
        if (original == null) return false;
        try
        {
            var frame = new StackTrace(exception, false).GetFrame(0);
            if (frame == null) return false;
            return Same(Harmony.GetOriginalMethodFromStackframe(frame), original)
                || Same(frame.GetMethod(), original);
        }
        catch
        {
            return false;
        }
    }

    internal static TextObject Contain(LastSeenState state, Exception exception)
    {
        var (text, kind) = FallbackText(state);
        Report(state, exception, kind);
        return text;
    }

    internal static (TextObject Text, string Kind) FallbackText(LastSeenState state)
    {
        try
        {
            if (state.SettlementLink == null)
                return (GameTexts.FindText("str_never_seen_encyclopedia_entry"), "never seen");

            var text = GameTexts.FindText("str_last_seen_encyclopedia_entry");
            text.SetTextVariable("SETTLEMENT", state.SettlementLink);
            text.SetTextVariable("IS_IN_SETTLEMENT", state.IsInSettlement ? 1 : 0);
            return (text, "last seen");
        }
        catch
        {
            return (TextObject.GetEmpty(), "empty");
        }
    }

    private static void Report(LastSeenState state, Exception exception, string kind)
    {
        try
        {
            if (!Reported.Add(state.HeroId)) return;

            var frame = new StackTrace(exception, false).GetFrame(0);
            _logger?.LogWarning(
                $"[LastSeenGuard] contained vanilla GetLastSeenText NRE for hero '{state.HeroId}' " +
                $"(IL offset {frame?.GetILOffset().ToString() ?? "?"}): prisoner={state.IsPrisoner} " +
                $"party='{state.PartyId}' mobile={state.PartyIsMobile} partyLeaderMissing={state.PartyLeaderMissing} " +
                $"army={(state.HasArmy ? state.ArmyLeaderMissing ? "leaderless" : "led" : "none")}; shown: {kind}");
        }
        catch
        {
            // Diagnostic only: a failed log line must never undo the swallow.
        }
    }

    private static bool Same(MethodBase? a, MethodBase? b) =>
        a != null && b != null && a.Module == b.Module && a.MetadataToken == b.MetadataToken;
}
