using System;
using System.Collections.Generic;
using TAOM.Adapters;

namespace TAOM.Features.FarWatch;

/// <summary>
/// The report half of the service (#847, docs/features/far-watch.md "Report"): which parties in sight the scout counts, in
/// what order, what he learns of each (the digit stages are Hidden Hosts' rule, asked through
/// <see cref="TAOM.Features.HiddenHosts.HiddenHostsService.RaiseByScout"/>), which six keep their names, and the breakdown of
/// a settlement's defenders. The adapter reports facts; every "who is included" decision here is policy.
/// </summary>
internal sealed partial class FarWatchService
{
    /// <summary>
    /// Raises every eligible party in sight by the stage count of the scout's skill, lists the first six that have a direction
    /// (army leaders, then lords, then the rest, nearest first) and names those six until <paramref name="namedUntil"/>.
    /// </summary>
    private void SweepSurroundings(ScoutReport report, double now, int skill, double namedUntil)
    {
        var sightings = new List<ScoutSighting>();
        _world.CollectSightings(sightings);

        var eligible = new List<ScoutSighting>(sightings.Count);
        for (int i = 0; i < sightings.Count; i++)
            if (IsReportable(sightings[i])) eligible.Add(sightings[i]);
        eligible.Sort(FarWatchMath.CompareSightings);

        int stages = FarWatchMath.Stages(skill);
        var named = new List<string>(FarWatchMath.MaxRows);
        for (int i = 0; i < eligible.Count; i++)
        {
            var sighting = eligible[i];
            var intel = sighting.Intel;
            string countText = _hidden.RaiseByScout(in intel, stages);
            int direction = FarWatchMath.CompassSector(sighting.Dx, sighting.Dy);
            if (report.Hosts.Count >= FarWatchMath.MaxRows || direction < 0) continue;

            report.Hosts.Add(new ScoutHostRow(sighting.Name, countText,
                FarWatchMath.Composition(sighting.Foot, sighting.Archers, sighting.Horse, sighting.HorseArchers), direction));
            named.Add(intel.StringId!);
        }

        _store.SetNamed(named, namedUntil);
    }

    /// <summary>
    /// A party the scout can learn something about: one with data, and not a villager, a caravan, a party attached to an army,
    /// a party inside a settlement (never visible), the main party, the player's clan or army, or a party the engine hides.
    /// </summary>
    private static bool IsReportable(ScoutSighting s) =>
        s.Intel.HasData && s.Intel.StringId != null
        && !s.IsVillager && !s.IsCaravan && !s.IsAttachedToArmy && !s.InSettlement
        && !s.Intel.IsMainParty && !s.Intel.InPlayerClan && !s.Intel.InPlayerArmy && !s.Intel.IsInfoHidden;

    /// <summary>The defenders of the settlement the scout looked over, party by party, and what Hidden Hosts remembers of them.</summary>
    private void ReadSettlement(ScoutReport report, string targetId, string targetName)
    {
        report.SiteName = targetName;
        var site = _world.ReadSite(targetId);
        if (!site.Found) return;

        report.SiteName = string.IsNullOrEmpty(site.Name) ? targetName : site.Name;
        report.SiteTotal = site.Intel.DefenderCount;
        var intel = site.Intel;
        _hidden.RecordScoutedSettlement(in intel);
        for (int i = 0; i < site.Defenders.Count; i++)
        {
            var d = site.Defenders[i];
            report.Defenders.Add(new ScoutDefenderRow(d.Kind, d.Name, d.Count, FarWatchMath.Composition(d.Foot, d.Archers, d.Horse, d.HorseArchers)));
        }
    }
}
