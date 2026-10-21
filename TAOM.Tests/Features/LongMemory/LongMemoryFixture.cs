using System;
using System.Collections.Generic;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.LongMemory;
using TAOM.Features.LongMemory.Models;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// One service over faked adapters. Sides are read from the kingdom id: "k_free" is Free, "k_evil" is Evil, anything else (the
/// empty id included) is Neutral. Every grant lands in full unless a test says otherwise, and the calls are recorded in
/// <see cref="Grants"/> in the order made.
/// </summary>
internal sealed class LongMemoryFixture
{
    internal static readonly LongMemorySettings Defaults = new(
        true, true, true, 50, 50, 20, 25, new SideMultipliers(1f, 0.5f, 0.1f));

    internal static readonly SideIds Free = new("k_free", "c_free");
    internal static readonly SideIds Evil = new("k_evil", "c_evil");
    internal static readonly SideIds Unaligned = new("k_other", "c_other");

    internal readonly ILongMemorySettingsProvider Settings = Substitute.For<ILongMemorySettingsProvider>();
    internal readonly ILongMemoryVillageAdapter Village = Substitute.For<ILongMemoryVillageAdapter>();
    internal readonly ILongMemoryNoticeAdapter Notice = Substitute.For<ILongMemoryNoticeAdapter>();
    internal readonly IAlignmentService Alignment = Substitute.For<IAlignmentService>();
    internal readonly IRelationGrantService GrantService = Substitute.For<IRelationGrantService>();
    internal readonly ICoopSessionProvider Coop = Substitute.For<ICoopSessionProvider>();
    internal readonly IDedicatedServerProvider Server = Substitute.For<IDedicatedServerProvider>();
    internal readonly IModLogger Logger = Substitute.For<IModLogger>();
    internal readonly List<(string HeroId, int Delta, string Reason)> Grants = new();

    internal LongMemoryFixture()
    {
        Settings.Get().Returns(Defaults);
        Alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(0) switch
        {
            "k_free" => FactionSide.Free,
            "k_evil" => FactionSide.Evil,
            _ => FactionSide.Neutral,
        });
        GrantService.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call =>
        {
            Grants.Add((call.ArgAt<string>(0), call.ArgAt<int>(1), call.ArgAt<string>(2)));
            return call.ArgAt<int>(1);
        });
        Service = new LongMemoryService(Settings, Notice, Alignment, GrantService, Coop, Server);
    }

    internal LongMemoryService Service { get; }

    /// <summary>How many times a reader handed to the service ran: the service reads only after every gate.</summary>
    internal int Reads { get; private set; }

    /// <summary>A reader for the service that answers the facts and counts each call.</summary>
    internal Func<VillageHostileActionFacts?> Read(VillageHostileActionFacts? facts) => () =>
    {
        Reads++;
        return facts;
    };

    internal void Configure(LongMemorySettings settings) => Settings.Get().Returns(settings);

    internal static RelationTarget Hero(string id, string? owner = null, bool alive = true, bool isPlayer = false) =>
        new(id, owner ?? id, alive, isPlayer);

    /// <summary>
    /// A village the player's party is raiding or forcing: a full raid by default, led by the player, owned by another clan of the
    /// player's own side, with a living owner clan leader and two notables.
    /// </summary>
    internal static VillageHostileActionFacts Facts(
        SideIds? player = null,
        SideIds? owner = null,
        bool isVillage = true,
        bool playerOwns = false,
        bool playerLeads = true,
        float damage = 1f,
        RelationTarget? ownerLeader = null,
        IReadOnlyList<RelationTarget>? notables = null,
        string name = "Dol Amroth Farm",
        bool noOwnerLeader = false) =>
        new(name, player ?? Free, owner ?? Free, isVillage, playerOwns, playerLeads, damage,
            noOwnerLeader ? null : ownerLeader ?? Hero("owner"),
            notables ?? new[] { Hero("n1"), Hero("n2") });
}
