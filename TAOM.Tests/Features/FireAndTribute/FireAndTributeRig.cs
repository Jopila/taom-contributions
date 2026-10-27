using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Enlistment;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;
using TAOM.Features.InitialChildGeneration;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>One party the fake engine holds: the snapshot a read returns, plus what was done to it.</summary>
internal sealed class FatParty
{
    public RaidHostSnapshot Snapshot { get; set; } = new RaidHostSnapshot
    {
        Exists = true, IsActive = true, HasHome = true, DistanceToHome = 60f, Strength = 200f, Speed = 5f, X = 100f, Y = 100f,
    };

    public string ClanId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public Dictionary<string, int> Roster { get; } = new Dictionary<string, int>();

    public bool Completed { get; set; }
}

/// <summary>A hand-written engine for the service: parties by id, a call log, and switches for each way it can refuse.</summary>
internal sealed class FakeRaidHostAdapter : IRaidHostAdapter
{
    private int _next = 1;

    public Dictionary<string, FatParty> Parties { get; } = new Dictionary<string, FatParty>();

    public List<string> Calls { get; } = new List<string>();

    public Dictionary<string, float> Power { get; } = new Dictionary<string, float>
    {
        ["snaga"] = 1f, ["grunt"] = 2f, ["lurker"] = 4f, ["pirate"] = 1.5f, ["dwarf"] = 2f,
    };

    /// <summary>Probe answers by clan id, then by anchor id; anything else is None.</summary>
    public Dictionary<string, RaidSpawnFailure> ProbeByClan { get; } = new Dictionary<string, RaidSpawnFailure>();

    public Dictionary<string, RaidSpawnFailure> ProbeByAnchor { get; } = new Dictionary<string, RaidSpawnFailure>();

    /// <summary>Anchor owners by anchor id (faction id, culture id, a kingdom?); an anchor not listed has an unreadable owner.</summary>
    public Dictionary<string, (string FactionId, string CultureId, bool IsKingdom)> AnchorOwners { get; } = new Dictionary<string, (string FactionId, string CultureId, bool IsKingdom)>();

    public RaidSpawnFailure SpawnFailure { get; set; } = RaidSpawnFailure.None;

    public string? NearestTown { get; set; } = "town_X";

    /// <summary>Every village the fake map holds: the one list both the selection and the keep-check read.</summary>
    public List<VillageReading> Villages { get; } = new List<VillageReading>();

    public List<NearbyParty> Nearby { get; } = new List<NearbyParty>();

    public bool DespawnRefuses { get; set; }

    public bool AddTroopsAddsNothing { get; set; }

    public bool AddTroopsThrows { get; set; }

    /// <summary>Raised after a successful despawn, as the engine's destroy event is.</summary>
    public Action<string>? OnDespawned { get; set; }

    public RaidSpawnFailure Probe(string clanId, string anchorId)
    {
        Calls.Add($"probe:{clanId}:{anchorId}");
        if (ProbeByClan.TryGetValue(clanId, out var byClan))
            return byClan;
        return ProbeByAnchor.TryGetValue(anchorId, out var byAnchor) ? byAnchor : RaidSpawnFailure.None;
    }

    public bool TryGetAnchorOwner(string anchorId, out string factionId, out string cultureId, out bool isKingdom)
    {
        var known = AnchorOwners.TryGetValue(anchorId, out var owner);
        factionId = known ? owner.FactionId : string.Empty;
        cultureId = known ? owner.CultureId : string.Empty;
        isKingdom = known && owner.IsKingdom;
        return known;
    }

    public RaidHostSpawn SpawnHost(string clanId, string anchorId, string keyedName, float spawnRadiusDays)
    {
        Calls.Add($"spawn:{clanId}:{anchorId}:{spawnRadiusDays.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        if (SpawnFailure != RaidSpawnFailure.None)
            return new RaidHostSpawn(SpawnFailure, null, null, null);
        var id = "host_" + _next++;
        Parties[id] = new FatParty { ClanId = clanId, Name = keyedName };
        return new RaidHostSpawn(RaidSpawnFailure.None, id, "hideout_" + clanId, NearestTown);
    }

    public float TroopPower(string troopId) => Power.TryGetValue(troopId, out var p) ? p : float.NaN;

    public int AddTroops(string partyId, string troopId, int count)
    {
        Calls.Add($"add:{partyId}:{troopId}:{count}");
        if (AddTroopsThrows)
            throw new InvalidOperationException("boom");
        if (AddTroopsAddsNothing || !Parties.TryGetValue(partyId, out var party))
            return 0;
        party.Roster[troopId] = party.Roster.TryGetValue(troopId, out var have) ? have + count : count;
        return count;
    }

    public void CompleteSpawn(string partyId)
    {
        Calls.Add("complete:" + partyId);
        if (Parties.TryGetValue(partyId, out var party))
            party.Completed = true;
    }

    public void ReleasePin(string partyId)
    {
        Calls.Add("release:" + partyId);
    }

    /// <summary>Party ids whose read throws, as a broken engine call would.</summary>
    public HashSet<string> ReadThrows { get; } = new HashSet<string>();

    public RaidHostSnapshot Read(string partyId)
    {
        Calls.Add("read:" + partyId);
        if (ReadThrows.Contains(partyId))
            throw new InvalidOperationException("read failed");
        return Parties.TryGetValue(partyId, out var party) ? party.Snapshot : RaidHostSnapshot.Missing;
    }

    public IReadOnlyList<VillageReading> ReadVillages()
    {
        Calls.Add("villages");
        return Villages.ToList();
    }

    public VillageReading ReadVillage(string villageId)
    {
        Calls.Add("village:" + villageId);
        var found = Villages.Where(v => v.Id == villageId).Cast<VillageReading?>().FirstOrDefault();
        return found ?? VillageReading.Missing;
    }

    /// <summary>Adds the village, or replaces the one with its id.</summary>
    public void Put(VillageReading village)
    {
        Villages.RemoveAll(v => v.Id == village.Id);
        Villages.Add(village);
    }

    /// <summary>Puts a village of <paramref name="kingdomId"/> at <paramref name="x"/>, 100 (the host stands at 100, 100).</summary>
    public void Put(string id, string? kingdomId, float x = 101f, float hearth = 400f, bool normal = true) =>
        Put(new VillageReading(true, kingdomId, normal, hearth, id, x, 100f));

    public void RemoveVillagesOf(string kingdomId) => Villages.RemoveAll(v => v.KingdomId == kingdomId);

    public IReadOnlyList<NearbyParty> PartiesNear(string hostPartyId, float radius)
    {
        Calls.Add($"near:{hostPartyId}:{radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        return Nearby.ToList();
    }

    public void SpareMainParty(string hostPartyId, int hours) => Calls.Add($"spare:{hostPartyId}:{hours}");

    public void OrderEngage(string hostPartyId, string targetPartyId) => Calls.Add($"engage:{hostPartyId}:{targetPartyId}");

    public void OrderRaid(string hostPartyId, string villageId)
    {
        Calls.Add($"raid:{hostPartyId}:{villageId}");
        if (Parties.TryGetValue(hostPartyId, out var party))
            party.Snapshot.RaidTargetId = villageId;
    }

    public void OrderGoHome(string hostPartyId)
    {
        Calls.Add("home:" + hostPartyId);
        if (Parties.TryGetValue(hostPartyId, out var party))
            party.Snapshot.RaidTargetId = null;
    }

    public bool Despawn(string partyId)
    {
        Calls.Add("despawn:" + partyId);
        if (DespawnRefuses || !Parties.TryGetValue(partyId, out var party) || !party.Snapshot.IsActive || party.Snapshot.InMapEvent)
            return false;
        Parties.Remove(partyId);
        OnDespawned?.Invoke(partyId);
        return true;
    }

    public int Count(string prefix) => Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));

    public bool Called(string exact) => Calls.Contains(exact);
}

internal sealed class FakeFatNotices : IFireAndTributeNoticeAdapter
{
    public List<string> Lines { get; } = new List<string>();

    public void ShowSpawned(string keyedHostName, string? nearestTownId) => Lines.Add($"spawned:{keyedHostName}:{nearestTownId}");

    public void ShowGoingHome(string keyedHostName) => Lines.Add("going:" + keyedHostName);

    public void ShowWentHome(string keyedHostName) => Lines.Add("home:" + keyedHostName);

    public void ShowBroken(string keyedHostName) => Lines.Add("broken:" + keyedHostName);
}

internal sealed class FakeFatSettings : IFireAndTributeSettings
{
    public bool Enabled { get; set; } = true;

    public bool FreeHostsEnabled { get; set; } = true;

    public bool NeutralHostsEnabled { get; set; } = true;

    public bool EvilHostsEnabled { get; set; } = true;

    public bool EnvoysEnabled { get; set; } = true;
}

/// <summary>The player as the envoy reads them, and the purse it charges.</summary>
internal sealed class FakeEnvoyPlayer : ITributeEnvoyAdapter
{
    public bool Exists { get; set; } = true;

    public string? ClanId { get; set; } = "player_clan";

    public string? KingdomId { get; set; } = "empire_w";

    public int Gold { get; set; } = 100000;

    public int Charm { get; set; }

    /// <summary>The villages bound to the clan's fiefs, as the engine adapter sums them.</summary>
    public int Villages { get; set; }

    public bool ReadThrows { get; set; }

    public List<int> Charged { get; } = new List<int>();

    public EnvoyPlayerReading ReadPlayer()
    {
        if (ReadThrows)
            throw new InvalidOperationException("player read failed");
        return Exists ? new EnvoyPlayerReading(true, ClanId, KingdomId, Gold, Charm, Villages) : EnvoyPlayerReading.Missing;
    }

    public bool TryCharge(int amount)
    {
        if (amount < 0 || Gold < amount)
            return false;
        Gold -= amount;
        Charged.Add(amount);
        return true;
    }
}

/// <summary>
/// The envoy's popup and lines: the offers shown, the callback held, the outcomes told. Like the engine's gate the map is not
/// free while this popup is open, and a popup closed by <see cref="Hide"/> (a load, <c>HideInquiry</c>) runs no callback.
/// </summary>
internal sealed class FakeEnvoyPresenter : IEnvoyPresenterAdapter
{
    public bool MapFree { get; set; } = true;

    public bool ShowThrows { get; set; }

    public bool OutcomeThrows { get; set; }

    public bool IsOpen { get; private set; }

    public List<EnvoyOffer> Offers { get; } = new List<EnvoyOffer>();

    public List<(EnvoyOutcome Outcome, string Host, int Price, int Days)> Outcomes { get; } = new List<(EnvoyOutcome, string, int, int)>();

    public Action<EnvoyChoice>? OnChosen { get; private set; }

    public EnvoyOffer? Last => Offers.Count == 0 ? null : Offers[Offers.Count - 1];

    public bool IsMapFree() => MapFree && !IsOpen;

    public void ShowOffer(EnvoyOffer offer, Action<EnvoyChoice> onChosen)
    {
        if (ShowThrows)
            throw new InvalidOperationException("popup failed");
        Offers.Add(offer);
        OnChosen = onChosen;
        IsOpen = true;
    }

    public void ShowOutcome(EnvoyOutcome outcome, string keyedHostName, int price, int days)
    {
        if (OutcomeThrows)
            throw new InvalidOperationException("message failed");
        Outcomes.Add((outcome, keyedHostName, price, days));
    }

    /// <summary>
    /// The player clicks an option. As in the engine, the callback runs while the popup is still open (the map is not
    /// free), and the popup closes afterwards; a popup the callback showed takes its place.
    /// </summary>
    public void Choose(EnvoyChoice choice)
    {
        var callback = OnChosen ?? throw new InvalidOperationException("no popup is open");
        int shownBefore = Offers.Count;
        callback(choice);
        IsOpen = Offers.Count > shownBefore;
    }

    /// <summary>The popup closes with no answer and no callback, as a load or the engine's <c>HideInquiry</c> closes it.</summary>
    public void Hide() => IsOpen = false;
}

internal sealed class FakeFatSnapshots : IKingdomWarSnapshotAdapter
{
    public List<KingdomWarSnapshot> Kingdoms { get; } = new List<KingdomWarSnapshot>();

    public int Day { get; set; } = 100;

    public bool DayReadable { get; set; } = true;

    public double Hours { get; set; } = 2400;

    public IReadOnlyList<KingdomWarSnapshot> GetKingdoms() => Kingdoms;

    public string GetCampaignId() => "c";

    public int GetElapsedDay() => DayReadable ? Day : 0;

    public bool TryGetElapsedDay(out int day)
    {
        day = DayReadable ? Day : 0;
        return DayReadable;
    }

    public double GetNowHours() => Hours;
}

internal sealed class FakeFatAlignment : IAlignmentService
{
    public Dictionary<string, FactionSide> ByKingdom { get; } = new Dictionary<string, FactionSide>();

    public FactionSide GetKingdomSide(string kingdomId) => ByKingdom.TryGetValue(kingdomId, out var s) ? s : FactionSide.Neutral;

    public FactionSide GetCultureSide(string cultureId) => FactionSide.Neutral;

    // As AlignmentService: the kingdom's own side, else its culture's (ByKingdom also holds culture ids).
    public FactionSide ResolveSide(string kingdomId, string cultureId) =>
        GetKingdomSide(kingdomId) is var side && side != FactionSide.Neutral ? side : GetKingdomSide(cultureId ?? string.Empty);

    public bool AreEnemyAlignments(string kingdomIdA, string kingdomIdB) => AreEnemyAlignments(GetKingdomSide(kingdomIdA), GetKingdomSide(kingdomIdB));

    public bool AreSameAlignment(string kingdomIdA, string kingdomIdB) => GetKingdomSide(kingdomIdA) == GetKingdomSide(kingdomIdB);

    public bool AreEnemyAlignments(FactionSide sideA, FactionSide sideB) => sideA != sideB;

    public bool AreSameAlignment(FactionSide sideA, FactionSide sideB) => sideA == sideB;
}

/// <summary>A scripted random source: doubles and ints are consumed in order, then a fallback repeats.</summary>
internal sealed class FakeFatRandom : IRandomSource
{
    public Queue<double> Doubles { get; } = new Queue<double>();

    public Queue<int> Ints { get; } = new Queue<int>();

    public double DoubleFallback { get; set; } = 0.0;

    public int IntFallback { get; set; }

    public int DoubleCalls { get; private set; }

    public int IntCalls { get; private set; }

    public double NextDouble()
    {
        DoubleCalls++;
        return Doubles.Count > 0 ? Doubles.Dequeue() : DoubleFallback;
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        IntCalls++;
        int value = Ints.Count > 0 ? Ints.Dequeue() : IntFallback;
        return Math.Max(minInclusive, Math.Min(maxExclusive - 1, minInclusive + value));
    }
}

internal sealed class FakeFatConfigProvider : IFireAndTributeConfigProvider
{
    public FireAndTributeConfig Config { get; set; } = FireAndTributeRig.DefaultConfig();

    public FireAndTributeConfig GetConfig() => Config;
}

/// <summary>The service and every fake it talks to, in a campaign at day 100, Full War, one realm per side raidable.</summary>
internal sealed class FireAndTributeRig
{
    public FakeRaidHostAdapter Adapter { get; } = new FakeRaidHostAdapter();

    public FakeFatNotices Notices { get; } = new FakeFatNotices();

    public FakeFatSettings Settings { get; } = new FakeFatSettings();

    public FakeFatSnapshots Snapshots { get; } = new FakeFatSnapshots();

    public FakeFatAlignment Alignment { get; } = new FakeFatAlignment();

    public FakeFatRandom Random { get; } = new FakeFatRandom();

    public FakeFatConfigProvider ConfigProvider { get; } = new FakeFatConfigProvider();

    public FakeEnvoyPlayer Player { get; } = new FakeEnvoyPlayer();

    public FakeEnvoyPresenter Presenter { get; } = new FakeEnvoyPresenter();

    public IEnlistmentStateQuery Enlistment { get; } = Substitute.For<IEnlistmentStateQuery>();

    public IWarOfTheRingService WarOfTheRing { get; } = Substitute.For<IWarOfTheRingService>();

    public ICoopSessionProvider Coop { get; } = Substitute.For<ICoopSessionProvider>();

    public IDedicatedServerProvider Dedicated { get; } = Substitute.For<IDedicatedServerProvider>();

    public IModLogger Logger { get; } = Substitute.For<IModLogger>();

    public FireAndTributeService Service { get; }

    public FireAndTributeRig()
    {
        WarOfTheRing.IsEnabled.Returns(true);
        WarOfTheRing.CurrentPhase.Returns(WarPhase.FullWar);
        WarOfTheRing.GetEffectivePhaseDay(WarPhase.FullWar).Returns(44);

        Alignment.ByKingdom["empire_w"] = FactionSide.Free;
        Alignment.ByKingdom["vlandia"] = FactionSide.Free;
        Alignment.ByKingdom["empire_s"] = FactionSide.Evil;
        Alignment.ByKingdom["isengard"] = FactionSide.Evil;
        Alignment.ByKingdom["umbar"] = FactionSide.Neutral;
        Snapshots.Kingdoms.Add(Kingdom("empire_w", 12000f));
        Snapshots.Kingdoms.Add(Kingdom("vlandia", 8000f));
        Snapshots.Kingdoms.Add(Kingdom("empire_s", 15000f));
        Snapshots.Kingdoms.Add(Kingdom("isengard", 6000f));
        Snapshots.Kingdoms.Add(Kingdom("umbar", 5000f));
        foreach (var k in Snapshots.Kingdoms)
        {
            // Three raidable villages each, nearest first: "<kingdom>_v1" to "<kingdom>_v3", east of the host.
            for (int i = 1; i <= 3; i++)
                Adapter.Put(k.Id + "_v" + i, k.Id, x: 100f + i);
        }

        Service = new FireAndTributeService(Adapter, Notices, ConfigProvider, Settings, Snapshots, Alignment,
            WarOfTheRing, Coop, Dedicated, Random, Player, Presenter, Enlistment, Logger);
        // The engine raises the destroy event inside the removal.
        Adapter.OnDespawned = id => Service.OnPartyDestroyed(id);
    }

    public static KingdomWarSnapshot Kingdom(string id, float strength) =>
        new KingdomWarSnapshot { Id = id, CultureId = id, Strength = strength };

    public static FireAndTributeConfig DefaultConfig() => new FireAndTributeConfig
    {
        Tuning = new FireAndTributeTuning(),
        Hosts = new List<RaidHostDef>
        {
            new RaidHostDef
            {
                Id = "gundabad", Side = FactionSide.Evil, ClanId = "gundabad_raiders",
                Name = "{=taom_fat_host_gundabad}The Host of Gundabad",
                Anchors = new[] { "town_G1", "castle_G4" },
                TargetSides = new[] { FactionSide.Free },
                PreferredTargets = new string[0],
                Troops = new[] { new HostTroop("snaga", 0.5f), new HostTroop("grunt", 0.3f), new HostTroop("lurker", 0.2f) },
            },
            new RaidHostDef
            {
                Id = "umbar", Side = FactionSide.Neutral, ClanId = "umbar_corsairs",
                Name = "{=taom_fat_host_umbar}The Corsair Host of Umbar",
                Anchors = new[] { "town_U1" },
                TargetSides = new[] { FactionSide.Free, FactionSide.Evil },
                PreferredTargets = new string[0],
                Troops = new[] { new HostTroop("pirate", 1f) },
            },
            new RaidHostDef
            {
                Id = "erebor", Side = FactionSide.Free, ClanId = "erebor_warriors",
                Name = "{=taom_fat_host_erebor}The Blacklock Warband",
                Anchors = new[] { "town_E1" },
                TargetSides = new[] { FactionSide.Evil },
                PreferredTargets = new string[0],
                Troops = new[] { new HostTroop("dwarf", 1f) },
            },
        },
    };

    /// <summary>Spawns a Gundabad host through the console path and returns its record.</summary>
    public RaidHostRecord SpawnEvil()
    {
        var result = Service.TrySpawn(FactionSide.Evil, dryRun: false);
        var record = LiveRecords().SingleOrDefault(r => r.Side == FactionSide.Evil);
        if (record == null)
            throw new InvalidOperationException("the evil host did not ride: " + result);
        return record;
    }

    public IReadOnlyList<RaidHostRecord> LiveRecords() =>
        FireAndTributeSaveCodec.Parse(Service.CaptureState()).Hosts;

    public FireAndTributeState State() => FireAndTributeSaveCodec.Parse(Service.CaptureState());

    /// <summary>Changes the saved record and loads it back, as a save edited between two sessions would be.</summary>
    public void Edit(Action<FireAndTributeState> change)
    {
        var state = State();
        change(state);
        Service.LoadFrom(FireAndTributeSaveCodec.Serialize(state));
    }

    /// <summary>
    /// Gives the player's clan <paramref name="count"/> villages of the kingdom, all far from the host (x 300 and up) so
    /// none is the nearest raid target, and returns their ids.
    /// </summary>
    public List<string> OwnVillages(int count, string kingdomId = "empire_w")
    {
        var ids = new List<string>();
        for (int i = 1; i <= count; i++)
        {
            string id = "player_v" + i;
            Adapter.Put(new VillageReading(true, kingdomId, true, 400f, id, 300f + i, 100f, "player_clan"));
            ids.Add(id);
        }
        Player.Villages = Adapter.Villages.Count(v => v.OwnerClanId == "player_clan");
        return ids;
    }

    /// <summary>The player's clan holds no village any more.</summary>
    public void DisownVillages()
    {
        Adapter.Villages.RemoveAll(v => v.OwnerClanId == "player_clan");
        Player.Villages = 0;
    }

    public FatParty PartyOf(RaidHostRecord record) => Adapter.Parties[record.PartyId];

    public int Warnings(string contains) =>
        Logger.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning) && ((string)c.GetArguments()[0]!).Contains(contains));
}
