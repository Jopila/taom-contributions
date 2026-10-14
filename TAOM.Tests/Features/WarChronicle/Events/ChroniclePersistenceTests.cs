using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.WarChronicle;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarChronicle.Rally;
using TAOM.Features.WarOfTheRingMomentum;
using static TAOM.Tests.Features.WarChronicle.Events.ChronicleFixture;

namespace TAOM.Tests.Features.WarChronicle.Events;

/// <summary>
/// The chronicle through the War Chronicle payload: the <c>chronicle</c> section is written by
/// <see cref="WarChronicleStateService"/> and restored over the constructor reset; the army focus is not
/// saved and comes back from the Active rows at session launch.
/// </summary>
[TestClass]
public class ChroniclePersistenceTests
{
    private static WarChronicleStateService StateFor(ChronicleFixture f) =>
        new WarChronicleStateService(f.Effects, new WarBaselineService(), new RallyTierStore(), f.Logger, f.Store);

    [TestMethod]
    public void SaveAndLoadMidWindow_KeepsTheFixedWindowRestoresTheFocusAndResolvesOnTheOldCloseDay()
    {
        var before = new ChronicleFixture();
        before.Events.Add(SiegeDef());
        var sut = before.CreateService();
        before.World.Day = 0;
        sut.LaunchChronicle();
        before.World.Day = 49;
        sut.RunChronicleDay();
        before.World.Besiegers["town_V2"] = "isengard";
        sut.OnTargetBesieged("town_V2");
        var chunks = StateFor(before).CaptureChunks();

        // A new process-lifetime view of the next session: fresh singletons, the save restored over them.
        var after = new ChronicleFixture();
        after.Events.Add(SiegeDef());
        after.Settings.ArmyFocusMultiplier.Returns(1.4f);
        StateFor(after).RestoreFromChunks(chunks);
        var reloaded = after.CreateService();
        after.World.Day = 55;
        reloaded.LaunchChronicle();

        var record = after.Store.Get("hornburg")!;
        Assert.AreEqual(ChronicleEventState.Active, record.State);
        Assert.AreEqual(69d, record.CloseDay, "the window is stored and fixed");
        Assert.IsTrue(record.Assaulted);
        Assert.AreEqual(1.4f, after.Focus.GetMultiplier(ArmyTargetingMission.Besieger, "town_V2", "isengard"), 0.0001f);

        after.World.Day = 69;
        reloaded.RunChronicleDay();
        Assert.AreEqual(ChronicleEventState.Held, after.StateOf("hornburg"));
    }

    [TestMethod]
    public void ANewCampaignAfterAResolvedChain_StartsEveryEventPendingAgain()
    {
        var f = new ChronicleFixture();
        f.Events.Add(Narrative("fellowship", offset: 1));
        f.Events.Add(SiegeDef());
        var state = StateFor(f);
        var sut = f.CreateService();
        f.World.Day = 0;
        sut.LaunchChronicle();
        f.World.Day = 49;
        sut.RunChronicleDay();
        f.World.Owners["town_V2"] = "isengard";
        sut.OnTargetOwnerChanged("town_V2");
        Assert.AreEqual(ChronicleEventState.Fell, f.StateOf("hornburg"));

        // Campaign B in the same process: the behavior's constructor resets, no save is loaded.
        state.ResetForNewSession();
        f.World.Owners["town_V2"] = "vlandia";
        f.World.Day = 0;
        sut.LaunchChronicle();

        Assert.AreEqual(ChronicleEventState.Pending, f.StateOf("fellowship"));
        Assert.AreEqual(ChronicleEventState.Pending, f.StateOf("hornburg"));
        f.World.Day = 49;
        sut.RunChronicleDay();
        Assert.AreEqual(ChronicleEventState.Active, f.StateOf("hornburg"), "campaign B gets its own chain");
    }

    [TestMethod]
    public void ASaveWithoutTheChronicleSection_AdoptsEveryEventAtLaunch()
    {
        var f = new ChronicleFixture();
        f.Events.Add(Narrative("fellowship", offset: 20));
        f.Events.Add(SiegeDef());
        f.Store.Set(new ChronicleEventRecord("stale", ChronicleEventState.Held));
        StateFor(f).RestoreFromChunks(new List<string> { "{\"v\":1,\"effects\":[]}" });
        var sut = f.CreateService();

        f.World.Day = 30;
        sut.LaunchChronicle();

        Assert.IsNull(f.Store.Get("stale"), "the restore reset the store first");
        Assert.AreEqual(ChronicleEventState.Skipped, f.StateOf("fellowship"), "its day had passed: no retroactive effect");
        Assert.AreEqual(ChronicleEventState.Pending, f.StateOf("hornburg"));
    }

    [TestMethod]
    public void ACorruptChronicleSection_ResetsAloneAndKeepsTheEffects()
    {
        var f = new ChronicleFixture();
        f.Effects.Apply(new TAOM.Features.WarChronicle.Effects.WarEffect("console", "vlandia",
            TAOM.Features.WarChronicle.Effects.WarEffectKind.VolunteerRate, 0.1f, 99999d));
        var chunks = StateFor(f).CaptureChunks();
        var json = string.Concat(chunks).Replace("\"chronicle\":[]", "\"chronicle\":[{\"id\":\"hornburg\",\"s\":42}]");

        var g = new ChronicleFixture();
        StateFor(g).RestoreFromChunks(MomentumSyncChunker.Split(json));

        Assert.AreEqual(0, g.Store.Records.Count);
        Assert.AreEqual(1, g.Effects.Snapshot().Count);
        Assert.IsTrue(g.Warnings.Any(w => w.Contains("hornburg")));
    }

    [TestMethod]
    public void ResetForNewSession_OnTheStateService_EmptiesTheChronicleStore()
    {
        var f = new ChronicleFixture();
        f.Store.Set(new ChronicleEventRecord("hornburg", ChronicleEventState.Active) { OpenDay = 1, CloseDay = 2 });

        StateFor(f).ResetForNewSession();

        Assert.AreEqual(0, f.Store.Records.Count);
    }
}
