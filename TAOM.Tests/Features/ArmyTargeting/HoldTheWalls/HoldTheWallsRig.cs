using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.ArmyTargeting.HoldTheWalls;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>A hand-written engine for the service: forts holding armies by leader id, a clock, and a log of every AI write.</summary>
internal sealed class FakeFortHoldAdapter : IFortHoldAdapter
{
    public double Hours { get; set; } = 1000.0;

    public double CampaignHours => Hours;

    /// <summary>Leader id to its reading; <see cref="ReadArmiesIn"/> lists the ones filed under a fort.</summary>
    public Dictionary<string, FortArmyReading> Armies { get; } = new Dictionary<string, FortArmyReading>();

    public Dictionary<string, List<string>> Forts { get; } = new Dictionary<string, List<string>>();

    /// <summary>"hold:leader:hours" and "resume:leader", in call order.</summary>
    public List<string> Writes { get; } = new List<string>();

    public int Reads { get; private set; }

    /// <summary>Leader ids whose read throws.</summary>
    public HashSet<string> Throwing { get; } = new HashSet<string>();

    /// <summary>Fort ids whose <see cref="ReadArmiesIn"/> throws.</summary>
    public HashSet<string> ThrowingForts { get; } = new HashSet<string>();

    /// <summary>Leader ids whose pause or resume write throws.</summary>
    public HashSet<string> ThrowingWrites { get; } = new HashSet<string>();

    public void Put(string fortId, FortArmyReading reading)
    {
        Armies[reading.LeaderPartyId] = reading;
        if (!Forts.TryGetValue(fortId, out var ids))
            Forts[fortId] = ids = new List<string>();
        if (!ids.Contains(reading.LeaderPartyId))
            ids.Add(reading.LeaderPartyId);
    }

    public IReadOnlyList<FortArmyReading> ReadArmiesIn(string fortId)
    {
        Reads++;
        if (ThrowingForts.Contains(fortId))
            throw new InvalidOperationException("boom " + fortId);
        return Forts.TryGetValue(fortId, out var ids) ? ids.Select(i => Armies[i]).ToList() : new List<FortArmyReading>();
    }

    public FortArmyReading ReadArmy(string leaderPartyId, string fortId)
    {
        Reads++;
        if (Throwing.Contains(leaderPartyId))
            throw new InvalidOperationException("boom " + leaderPartyId);
        return Armies.TryGetValue(leaderPartyId, out var reading) ? reading : FortArmyReading.Missing;
    }

    public void HoldForHours(string leaderPartyId, int hours)
    {
        if (ThrowingWrites.Contains(leaderPartyId))
            throw new InvalidOperationException("write boom " + leaderPartyId);
        Writes.Add($"hold:{leaderPartyId}:{hours}");
    }

    public void ResumeAi(string leaderPartyId)
    {
        if (ThrowingWrites.Contains(leaderPartyId))
            throw new InvalidOperationException("write boom " + leaderPartyId);
        Writes.Add($"resume:{leaderPartyId}");
    }
}

/// <summary>The service over the fake engine, with the switches a test flips.</summary>
internal sealed class HoldTheWallsRig
{
    public FakeFortHoldAdapter Adapter { get; } = new FakeFortHoldAdapter();

    public ArmyTargetingConfig Config { get; } = new ArmyTargetingConfig();

    public IArmyTargetingConfigProvider ConfigProvider { get; } = Substitute.For<IArmyTargetingConfigProvider>();

    public IArmyTargetingSettingsProvider Settings { get; } = Substitute.For<IArmyTargetingSettingsProvider>();

    public IAlignmentService Alignment { get; } = Substitute.For<IAlignmentService>();

    public ICoopSessionProvider Coop { get; } = Substitute.For<ICoopSessionProvider>();

    public IDedicatedServerProvider Dedicated { get; } = Substitute.For<IDedicatedServerProvider>();

    public IModLogger Logger { get; } = Substitute.For<IModLogger>();

    /// <summary>Realm id to side; any realm not listed is Free.</summary>
    public Dictionary<string, FactionSide> SideOfRealm { get; } = new Dictionary<string, FactionSide>
    {
        ["mordor"] = FactionSide.Evil,
        ["umbar"] = FactionSide.Neutral,
    };

    public List<string> DebugLines { get; } = new List<string>();

    public HoldTheWallsService Service { get; }

    public HoldTheWallsRig()
    {
        ConfigProvider.GetConfig().Returns(Config);
        Settings.EnableArmyStrategicIntelligence.Returns(true);
        Settings.HoldCapturedForts.Returns(true);
        Alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>())
            .Returns(call => SideOfRealm.TryGetValue(call.ArgAt<string>(0), out var side) ? side : FactionSide.Free);
        Logger.When(l => l.LogDebug(Arg.Any<string>())).Do(call => DebugLines.Add(call.ArgAt<string>(0)));
        Service = new HoldTheWallsService(Adapter, ConfigProvider, Settings, Alignment, Coop, Dedicated, Logger);
    }

    public CapturedFortHoldConfig Hold => Config.CapturedFortHold;

    /// <summary>A Gondor army leader inside the fortress with a healthy army: the case that holds.</summary>
    public static FortArmyReading Army(string leaderId = "lord_1", string realm = "empire_w") => new FortArmyReading
    {
        Exists = true,
        LeaderPartyId = leaderId,
        InFort = true,
        SameRealmAsFort = true,
        AiPaused = true,
        Cohesion = 80f,
        LowestFoodDays = 5f,
        Side = new SideIds(realm, "empire"),
    };

    /// <summary>Puts <paramref name="reading"/> in the fortress and records a siege capture of it at the current hour.</summary>
    public FortArmyReading Capture(string fortId = "town_A", FortArmyReading? reading = null)
    {
        reading ??= Army();
        Adapter.Put(fortId, reading);
        Service.OnSettlementOwnerChanged(fortId, isFortification: true, bySiege: true);
        return reading;
    }

    /// <summary>Advances the clock and runs one hourly pass.</summary>
    public void Tick(double hoursLater = 1.0)
    {
        Adapter.Hours += hoursLater;
        Service.OnHourlyTick();
    }

    public void CloseTheGate() => Coop.IsSessionActive.Returns(true);
}
