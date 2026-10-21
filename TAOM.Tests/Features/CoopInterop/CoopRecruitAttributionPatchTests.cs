using System;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.Hooks;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Patch113 brackets Coop's HandleOnRecruitmentDone. Run through real Harmony on a local fake with the
/// same shape (instance, string first argument), as the harmony rules require for a prefix/finalizer pair.
/// </summary>
[TestClass]
public class CoopRecruitAttributionPatchTests
{
    public class FakeRosterInterface
    {
        public static string? SeenDuring;
        public static bool Throw;

        public void HandleOnRecruitmentDone(string mobilePartyId, int[] cart)
        {
            SeenDuring = RecruitAttributionScope.Current;
            if (Throw) throw new InvalidOperationException("recruit failed");
        }
    }

    private Harmony _harmony = null!;

    [TestInitialize]
    public void Setup()
    {
        RecruitAttributionScope.Current = null;
        FakeRosterInterface.SeenDuring = null;
        FakeRosterInterface.Throw = false;
        _harmony = new Harmony("taom.tests.patch113." + Guid.NewGuid());
        _harmony.Patch(
            AccessTools.Method(typeof(FakeRosterInterface), nameof(FakeRosterInterface.HandleOnRecruitmentDone)),
            prefix: new HarmonyMethod(typeof(CoopRecruitAttributionPatch), nameof(CoopRecruitAttributionPatch.Prefix)),
            finalizer: new HarmonyMethod(typeof(CoopRecruitAttributionPatch), nameof(CoopRecruitAttributionPatch.Finalizer)));
    }

    [TestCleanup]
    public void Cleanup()
    {
        _harmony.UnpatchAll(_harmony.Id);
        RecruitAttributionScope.Current = null;
    }

    [TestMethod]
    public void Scope_SetDuringTheCall_RestoredAfterReturn()
    {
        new FakeRosterInterface().HandleOnRecruitmentDone("MobileParty_guest_party", Array.Empty<int>());

        Assert.AreEqual("guest_party", FakeRosterInterface.SeenDuring);
        Assert.IsNull(RecruitAttributionScope.Current);
    }

    // Coop keys a party "MobileParty_" + StringId and the client sends that key; the scope holds the engine id.
    [TestMethod]
    public void ToPartyStringId_StripsOnlyCoopsPartyPrefix()
    {
        Assert.AreEqual("Player1", CoopRecruitAttributionPatch.ToPartyStringId("MobileParty_Player1"));
        Assert.AreEqual("Created_7", CoopRecruitAttributionPatch.ToPartyStringId("MobileParty_Created_7"));
        Assert.AreEqual("lord_1_1_party", CoopRecruitAttributionPatch.ToPartyStringId("lord_1_1_party"));
        Assert.IsNull(CoopRecruitAttributionPatch.ToPartyStringId(null));
        Assert.AreEqual("", CoopRecruitAttributionPatch.ToPartyStringId("MobileParty_"));
    }

    [TestMethod]
    public void Scope_TheCallThrows_RestoredAndTheExceptionStillPropagates()
    {
        FakeRosterInterface.Throw = true;

        Assert.ThrowsException<InvalidOperationException>(
            () => new FakeRosterInterface().HandleOnRecruitmentDone("MobileParty_guest_party", Array.Empty<int>()));

        Assert.AreEqual("guest_party", FakeRosterInterface.SeenDuring);
        Assert.IsNull(RecruitAttributionScope.Current);
    }

    [TestMethod]
    public void Scope_NestedCall_RestoresTheOuterValue()
    {
        RecruitAttributionScope.Current = "outer";

        new FakeRosterInterface().HandleOnRecruitmentDone("inner", Array.Empty<int>());

        Assert.AreEqual("inner", FakeRosterInterface.SeenDuring);
        Assert.AreEqual("outer", RecruitAttributionScope.Current);
    }

    [TestMethod]
    public void Prepare_CoopTypeAbsent_SkipsThePatch()
    {
        Assert.IsFalse(CoopRecruitAttributionPatch.Prepare());
    }
}
