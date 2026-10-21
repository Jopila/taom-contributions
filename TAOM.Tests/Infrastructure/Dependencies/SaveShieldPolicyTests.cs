using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins SaveShield's swallow decision across the full input matrix.
///
/// SaveShield swallows exceptions from two very different chains, and co-op splits them apart:
///
/// - SAVE-LOAD (SaveManager.Load, LoadResult.InitializeObjects, MBSaveLoad.LoadSaveGameData, …).
///   Swallowing here leaves a PARTIALLY deserialised campaign that looks fine and keeps running.
///   In singleplayer that is a survivable degradation. Under a host-authoritative co-op mod the
///   host then treats that half-built campaign as authoritative and replicates it, so a visible
///   load failure is enormously better than an invisible one.
/// - MISSION-INIT (Mission.Initialize/SetMissionMode/SpawnTroop, MissionState.*). In singleplayer
///   a fault here is local, one broken battle, so the swallow stays. Under co-op a swallowed fault
///   leaves a half-initialised mission that the mission sync treats as real, so it is rethrown.
///
/// The <c>saveshield-swallow-disabled.flag</c> file dominates every other input; it is the blunt
/// "rethrow everything" escalation we reach for during triage.
/// </summary>
[TestClass]
public class SaveShieldPolicyTests
{
    [TestMethod]
    public void ShouldSwallow_SaveLoad_NoCoop_ReturnsTrue()
    {
        Assert.IsTrue(SaveShieldPolicy.ShouldSwallow("SAVE-LOAD", coopActive: false, swallowDisabledByFlag: false));
    }

    [TestMethod]
    public void ShouldSwallow_SaveLoad_CoopActive_ReturnsFalse()
    {
        Assert.IsFalse(SaveShieldPolicy.ShouldSwallow("SAVE-LOAD", coopActive: true, swallowDisabledByFlag: false));
    }

    [TestMethod]
    public void ShouldSwallow_MissionInit_NoCoop_ReturnsTrue()
    {
        Assert.IsTrue(SaveShieldPolicy.ShouldSwallow("MISSION-INIT", coopActive: false, swallowDisabledByFlag: false));
    }

    [TestMethod]
    public void ShouldSwallow_MissionInit_CoopActive_ReturnsFalse()
    {
        // A swallowed mission-init fault leaves a half-initialised mission that co-op's mission
        // sync would treat as real, so under co-op it surfaces.
        Assert.IsFalse(SaveShieldPolicy.ShouldSwallow("MISSION-INIT", coopActive: true, swallowDisabledByFlag: false));
    }

    [TestMethod]
    public void ShouldSwallow_MissionInitCategoryCasing_IsIgnored()
    {
        Assert.IsFalse(SaveShieldPolicy.ShouldSwallow("mission-init", coopActive: true, swallowDisabledByFlag: false));
    }

    [TestMethod]
    public void ShouldSwallow_FlagPresent_ReturnsFalseForEveryCategoryAndCoopState()
    {
        foreach (var category in new[] { "SAVE-LOAD", "MISSION-INIT", "UNKNOWN", null })
        {
            foreach (var coop in new[] { true, false })
            {
                Assert.IsFalse(
                    SaveShieldPolicy.ShouldSwallow(category, coop, swallowDisabledByFlag: true),
                    $"flag must dominate (category='{category}', coopActive={coop})");
            }
        }
    }

    [TestMethod]
    public void ShouldSwallow_UnknownCategory_CoopActive_ReturnsTrue()
    {
        // Only the save-load chain is escalated. An unrecognised category keeps today's behaviour
        // rather than silently becoming a CTD.
        Assert.IsTrue(SaveShieldPolicy.ShouldSwallow("SOMETHING-ELSE", coopActive: true, swallowDisabledByFlag: false));
    }

    [TestMethod]
    public void ShouldSwallow_NullCategory_ReturnsTrue()
    {
        Assert.IsTrue(SaveShieldPolicy.ShouldSwallow(null, coopActive: true, swallowDisabledByFlag: false));
    }

    [TestMethod]
    public void ShouldSwallow_SaveLoadCategoryCasing_IsIgnored()
    {
        Assert.IsFalse(SaveShieldPolicy.ShouldSwallow("save-load", coopActive: true, swallowDisabledByFlag: false));
    }
}
