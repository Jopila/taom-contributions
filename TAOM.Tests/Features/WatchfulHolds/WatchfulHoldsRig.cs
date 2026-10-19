using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.WatchfulHolds;
using TAOM.Features.WatchfulHolds.Models;

namespace TAOM.Tests.Features.WatchfulHolds;

/// <summary>A scripted campaign: parties, forts and a record of everything the service did to them.</summary>
internal sealed class FakeWatchfulHoldsAdapter : IWatchfulHoldsAdapter
{
    public readonly Dictionary<object, WatchfulPartySnapshot> Parties = new Dictionary<object, WatchfulPartySnapshot>();
    public readonly List<FortSite> Forts = new List<FortSite>();
    public readonly Dictionary<string, FortStatus> FortStatuses = new Dictionary<string, FortStatus>();
    public readonly Dictionary<object, IReadOnlyList<WatchfulPartySnapshot>> Armies = new Dictionary<object, IReadOnlyList<WatchfulPartySnapshot>>();
    public Func<object, string, bool> AtWar = (_, _) => true;
    public double Hours { get; set; } = 1000.0;
    public bool ThrowOnRead;

    public int TryReadPartyCalls;
    public int GetFortSitesCalls;
    public readonly List<string> FortReads = new List<string>();
    public readonly List<string> WarChecks = new List<string>();
    public readonly List<object> Disorganized = new List<object>();
    public readonly List<(object Handle, int Kill, int Wound)> LossCalls = new List<(object, int, int)>();
    public readonly List<(object Handle, float Fraction)> FoodCalls = new List<(object, float)>();
    public readonly List<(object Handle, float Amount)> MoraleCalls = new List<(object, float)>();
    public readonly List<(object Handle, float Amount, float Floor)> CohesionCalls = new List<(object, float, float)>();

    /// <summary>What the engine reports applied; the default applies everything asked.</summary>
    public Func<object, int, int, (int, int)> LossResult = (_, kill, wound) => (kill, wound);

    /// <summary>The units a party's food stacks lose; the default is 7 per party.</summary>
    public Func<object, float, int> FoodResult = (_, _) => 7;

    public bool TryReadParty(object engineParty, out WatchfulPartySnapshot snapshot)
    {
        TryReadPartyCalls++;
        if (ThrowOnRead)
            throw new InvalidOperationException("engine read failed");
        return Parties.TryGetValue(engineParty, out snapshot);
    }

    public FortSite[] GetFortSites()
    {
        GetFortSitesCalls++;
        return Forts.ToArray();
    }

    public bool TryReadFort(string fortId, out FortStatus status)
    {
        FortReads.Add(fortId);
        return FortStatuses.TryGetValue(fortId, out status);
    }

    public bool AreAtWar(object partyHandle, string fortId)
    {
        WarChecks.Add(fortId);
        return AtWar(partyHandle, fortId);
    }

    public IReadOnlyList<WatchfulPartySnapshot> GetArmyMemberParties(object leaderHandle) =>
        Armies.TryGetValue(leaderHandle, out var members) ? members : new List<WatchfulPartySnapshot>();

    public void Disorganize(object handle) => Disorganized.Add(handle);

    public (int killed, int wounded) ApplyLosses(object handle, int kill, int wound)
    {
        LossCalls.Add((handle, kill, wound));
        return LossResult(handle, kill, wound);
    }

    public int RemoveFood(object handle, float fraction)
    {
        FoodCalls.Add((handle, fraction));
        return FoodResult(handle, fraction);
    }

    public void LowerMorale(object handle, float amount) => MoraleCalls.Add((handle, amount));

    public void LowerArmyCohesion(object leaderHandle, float amount, float floor) => CohesionCalls.Add((leaderHandle, amount, floor));

    public double CampaignHours => Hours;
}

internal sealed class FakeNotices : IWatchfulHoldsNoticeAdapter
{
    public readonly List<(string Fort, int Killed, int Wounded, int Food)> Harried = new List<(string, int, int, int)>();
    public int Horns;

    public void ShowHarried(string fortId, int killed, int wounded, int food) => Harried.Add((fortId, killed, wounded, food));

    public void PlayHorn() => Horns++;
}

internal sealed class FakeProvinces : IWatchfulProvinceLookup
{
    public bool Available;
    public string FiefId = string.Empty;
    public int Lookups;

    public bool TryGetProvinceFief(float x, float y, out string fiefId)
    {
        Lookups++;
        fiefId = Available ? FiefId : string.Empty;
        return Available;
    }
}

internal sealed class FakeSettings : IWatchfulHoldsSettings
{
    public bool Enabled { get; set; } = true;

    public bool HarassAiLords { get; set; } = true;
}

/// <summary>NextDouble returns <see cref="Roll"/>; Next returns <see cref="Extra"/> clamped into the asked range.</summary>
internal sealed class FakeRandom : IRandomSource
{
    public double Roll;
    public int Extra;
    public int DoubleCalls;
    public readonly List<(int Min, int MaxExclusive)> NextCalls = new List<(int, int)>();

    public double NextDouble()
    {
        DoubleCalls++;
        return Roll;
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        NextCalls.Add((minInclusive, maxExclusive));
        return Math.Max(minInclusive, Math.Min(maxExclusive - 1, Extra));
    }
}

/// <summary>
/// The default scene: a Gondor-cultured fort "minas" 3 map units from a moving 100-man party, 50 men in its garrison,
/// at war, no provinces, roll 0 (every chance above 0 triggers). A test changes only what it is about.
/// </summary>
internal sealed class WatchfulHoldsRig
{
    public const string PlayerHandle = "player";
    public const string AiHandle = "ai";
    public const string Fort = "minas";

    public readonly FakeWatchfulHoldsAdapter Adapter = new FakeWatchfulHoldsAdapter();
    public readonly FakeNotices Notices = new FakeNotices();
    public readonly FakeProvinces Provinces = new FakeProvinces();
    public readonly FakeSettings Settings = new FakeSettings();
    public readonly FakeRandom Random = new FakeRandom();
    public readonly WatchfulHoldsConfig Config = new WatchfulHoldsConfig();
    public readonly IEnlistmentStateQuery Enlistment = Substitute.For<IEnlistmentStateQuery>();
    public readonly IModLogger Logger = Substitute.For<IModLogger>();
    public readonly WatchfulHoldsService Sut;

    public WatchfulHoldsRig()
    {
        var config = Substitute.For<IWatchfulHoldsConfigProvider>();
        config.GetConfig().Returns(Config);
        Adapter.Parties[PlayerHandle] = Party(PlayerHandle, isMain: true);
        Adapter.Parties[AiHandle] = Party(AiHandle, isMain: false);
        AddFort(Fort, 3f, 0f);
        Sut = new WatchfulHoldsService(Adapter, Notices, Provinces, Settings, config, Enlistment, Random, Logger);
    }

    public static WatchfulPartySnapshot Party(string handle, bool isMain, int men = 100, float x = 0f, float y = 0f) =>
        new WatchfulPartySnapshot
        {
            Handle = handle,
            X = x,
            Y = y,
            IsMainParty = isMain,
            IsLordParty = true,
            IsMoving = true,
            CanGetDisorganized = true,
            MenCount = men,
        };

    public void AddFort(string id, float x, float y, string culture = "gondor", int garrison = 50, bool siege = false)
    {
        Adapter.Forts.Add(new FortSite(id, x, y));
        Adapter.FortStatuses[id] = new FortStatus
        {
            OwnerCultureId = culture,
            UnderSiege = siege,
            GarrisonMen = garrison,
        };
    }

    public WatchfulPartySnapshot Player
    {
        get => Adapter.Parties[PlayerHandle];
        set => Adapter.Parties[PlayerHandle] = value;
    }

    public WatchfulPartySnapshot Ai
    {
        get => Adapter.Parties[AiHandle];
        set => Adapter.Parties[AiHandle] = value;
    }

    public void TickPlayer() => Sut.OnHourlyTickParty(PlayerHandle);

    public void TickAi() => Sut.OnHourlyTickParty(AiHandle);

    /// <summary>True when the service did anything at all to any party.</summary>
    public bool AnythingHappened =>
        Adapter.Disorganized.Count > 0 || Adapter.LossCalls.Count > 0 || Adapter.FoodCalls.Count > 0
        || Adapter.MoraleCalls.Count > 0 || Adapter.CohesionCalls.Count > 0
        || Notices.Harried.Count > 0 || Notices.Horns > 0;
}
