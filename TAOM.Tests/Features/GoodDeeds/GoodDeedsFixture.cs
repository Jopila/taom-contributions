using System.Collections.Generic;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.GoodDeeds;
using TAOM.Features.GoodDeeds.Models;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.GoodDeeds;

/// <summary>
/// One service over faked adapters. Sides are read from the kingdom id: "k_free" is Free, "k_evil" is Evil, anything else (the
/// empty id included) is Neutral. Every grant lands in full unless a test says otherwise, and the calls are recorded in
/// <see cref="Grants"/> in the order made.
/// </summary>
internal sealed class GoodDeedsFixture
{
    internal static readonly GoodDeedsSettings Defaults = new(
        true, true, true, true, 5, 2, 2, 5, 1,
        new SideMultipliers(1f, 0.75f, 0.5f), new SideMultipliers(0f, 1f, 2f), new SideMultipliers(1f, 1f, 0f));

    internal static readonly DeedSide Free = new("k_free", "c_free");
    internal static readonly DeedSide Evil = new("k_evil", "c_evil");
    internal static readonly DeedSide Unaligned = new("k_other", "c_other");

    internal readonly IGoodDeedsSettingsProvider Settings = Substitute.For<IGoodDeedsSettingsProvider>();
    internal readonly IGoodDeedsHeroAdapter Heroes = Substitute.For<IGoodDeedsHeroAdapter>();
    internal readonly IGoodDeedsNoticeAdapter Notice = Substitute.For<IGoodDeedsNoticeAdapter>();
    internal readonly IAlignmentService Alignment = Substitute.For<IAlignmentService>();
    internal readonly IRelationGrantService GrantService = Substitute.For<IRelationGrantService>();
    internal readonly ICoopSessionProvider Coop = Substitute.For<ICoopSessionProvider>();
    internal readonly IDedicatedServerProvider Server = Substitute.For<IDedicatedServerProvider>();
    internal readonly IModLogger Logger = Substitute.For<IModLogger>();
    internal readonly List<(string HeroId, int Delta, string Reason)> Grants = new();

    internal GoodDeedsFixture()
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
        Service = new GoodDeedsService(Settings, Heroes, Notice, Alignment, GrantService, Coop, Server, Logger);
    }

    internal GoodDeedsService Service { get; }

    internal void Configure(GoodDeedsSettings settings) => Settings.Get().Returns(settings);

    internal static DeedHero Hero(string id, string? owner = null, bool alive = true, bool isPlayer = false) =>
        new(id, owner ?? id, alive, isPlayer);

    internal static ReleaseFacts Release(DeedSide player, DeedSide lordSide, DeedHero? lord = null, DeedHero? spouse = null,
        DeedHero? leader = null, bool isLord = true) =>
        new(player, lordSide, isLord, lord ?? Hero("lord"), spouse, leader);
}
