using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.HostReview.Domain;

namespace TAOM.Adapters;

public sealed class HostReviewPartyAdapter : IHostReviewPartyAdapter
{
    private readonly IModLogger _logger;

    public HostReviewPartyAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public int RealBattleSize =>
        Math.Min(BannerlordConfig.GetRealBattleSize(), DefaultBattleMissionAgentSpawnLogic.MaxNumberOfTroopsForMission);

    public IReadOnlyList<HostRosterEntry> GetRoster()
    {
        var entries = new List<HostRosterEntry>();
        try
        {
            var roster = MobileParty.MainParty?.MemberRoster;
            if (roster == null) return entries;

            for (var i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var character = element.Character;
                if (character == null) continue;

                // For a hero line Number is 1 and WoundedNumber reads HeroObject.IsWounded, so one formula fits every line.
                var healthy = Math.Max(0, element.Number - element.WoundedNumber);
                var isMainHero = character.IsHero && character.HeroObject == Hero.MainHero;
                entries.Add(new HostRosterEntry(i, character.IsHero, isMainHero, healthy));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[HostReview] Reading the roster failed: {ex.Message}");
            entries.Clear();
        }

        return entries;
    }
}
