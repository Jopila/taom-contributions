using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using TAOM.Adapters;
using TAOM.Features.FarWatch;

namespace TAOM.Features.FieldCamp.Activities;

/// <summary>
/// The campaign facts camp work reads and the writes a finished work makes (#860). Facts and writes only: it never decides who
/// may work or what a work yields (<see cref="CampActivityService"/> does). It holds no engine object between calls (a process
/// singleton; CampActivitiesWiringTests pins the fields). The ground is read through <see cref="IMapTerrainAdapter"/>, which checks a
/// face is valid before it reads the terrain, and the water search samples that same read at fixed points around the camp, not
/// the engine's own drifting neighbour search. Main thread only.
/// </summary>
internal sealed class CampWorkAdapter : ICampWorkAdapter
{
    /// <summary>The map distances, from the camp, at which the eight compass points are sampled for water.</summary>
    private static readonly float[] WaterSampleDistances = { 2f, 4f };

    private const int CompassPoints = 8;

    private readonly ICampService _camps;
    private readonly IMapTerrainAdapter _terrain;
    private readonly FarWatchStore _scoutRide;

    public CampWorkAdapter(ICampService camps, IMapTerrainAdapter terrain, FarWatchStore scoutRide)
    {
        _camps = camps;
        _terrain = terrain;
        _scoutRide = scoutRide;
    }

    public CampWorkFacts Read()
    {
        var main = Main;
        if (Campaign.Current == null || main == null) return default;

        var roster = main.MemberRoster;
        var at = main.GetPosition2D;
        return new CampWorkFacts
        {
            CampReady = CampReady(),
            HealthyMen = roster.TotalHealthyCount,
            HealthyRegulars = roster.TotalRegulars - roster.TotalWoundedRegulars,
            ClanTier = Clan.PlayerClan?.Tier ?? 0,
            ToolsHeld = ItemsHeld(main, DefaultItems.Tools),
            TerrainType = _terrain.TerrainTypeAt(at.x, at.y),
            Busy = Campaign.Current.PlayerEncounter != null || main.MapEvent != null,
            ScoutRideOut = _scoutRide.HasRide,
        };
    }

    public bool CampReady() => _camps.PlayerCamp is { } camp && camp.IsReady;

    public bool WaterNearby()
    {
        var main = Main;
        if (Campaign.Current == null || main == null) return false;

        var at = main.GetPosition2D;
        foreach (float distance in WaterSampleDistances)
        {
            for (int point = 0; point < CompassPoints; point++)
            {
                double angle = point * (Math.PI * 2d / CompassPoints);
                int terrain = _terrain.TerrainTypeAt(at.x + (float)(Math.Cos(angle) * distance), at.y + (float)(Math.Sin(angle) * distance));
                if (CampActivityRules.IsWater(terrain)) return true;
            }
        }

        return false;
    }

    public int AddGoods(CampActivity activity, int amount)
    {
        var main = Main;
        var item = GoodsOf(activity);
        if (main == null || item == null || amount <= 0) return 0;

        main.ItemRoster.AddToCounts(item, amount);
        return amount;
    }

    public void TakeTools(int count)
    {
        var main = Main;
        var tools = DefaultItems.Tools;
        if (main == null || tools == null || count <= 0) return;

        main.ItemRoster.AddToCounts(tools, -Math.Min(count, ItemsHeld(main, tools)));
    }

    public void LowerMorale(float cost)
    {
        var main = Main;
        if (main != null && cost > 0f) main.RecentEventsMorale -= cost;
    }

    public int WoundRegulars(int count)
    {
        var roster = Main?.MemberRoster;
        if (roster == null || count <= 0) return 0;

        // The engine's own draw: each man is picked evenly from all the healthy regulars, so the first troop type does not always
        // bleed first and a hero is never picked. The clamp is the healthy regulars, the draw's own range.
        int wounded = Math.Min(count, roster.TotalRegulars - roster.TotalWoundedRegulars);
        if (wounded <= 0) return 0;
        roster.WoundNumberOfNonHeroTroopsRandomly(wounded);
        return wounded;
    }

    public float NextRandom() => MBRandom.RandomFloat;

    public void ShowMessage(string text) => InformationManager.DisplayMessage(new InformationMessage(text, Colors.Green));

    /// <summary>The main party, read null-safe: <c>MobileParty.MainParty</c> dereferences <c>Campaign.Current</c> and throws without one.</summary>
    private static MobileParty? Main => Campaign.Current?.MainParty;

    private static int ItemsHeld(MobileParty party, ItemObject? item) => item == null ? 0 : party.ItemRoster.GetItemNumber(item);

    private static ItemObject? GoodsOf(CampActivity activity)
    {
        switch (activity)
        {
            case CampActivity.Hunt: return DefaultItems.Meat;
            case CampActivity.Timber: return DefaultItems.HardWood;
            // No DefaultItems member for fish: the vanilla trade good, looked up by id.
            case CampActivity.Fish: return MBObjectManager.Instance?.GetObject<ItemObject>("fish");
            default: return null;
        }
    }
}
