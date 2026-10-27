using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CharacterCreation.Hooks;

namespace TAOM.Tests.Features.CharacterCreation;

/// <summary>
/// Patch20's guard on vanilla <c>CharacterCreationNarrativeStageView.SpawnNonHumanNarrativeMenuCharacter</c>
/// is a prefix: it skips the spawn when vanilla would throw (v1.5.5 :204-215), instead of a finalizer that
/// swallowed every NRE in the method and left a half-built entity in the scene (NRE guard audit 2026-10-27).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class NarrativeMountSpawnGuardTests
{
    [TestMethod]
    public void CanSpawnMount_EverythingPresent_IsTrue() =>
        Assert.IsTrue(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, true, true, true));

    [TestMethod]
    public void CanSpawnMount_NoSpawnPoint_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(false, true, true, true));

    [TestMethod]
    public void CanSpawnMount_ItemIdMissing_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, false, true, true));

    [TestMethod]
    public void CanSpawnMount_NoHorseMonster_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, true, false, true));

    [TestMethod]
    public void CanSpawnMount_NoMountCreationKey_IsFalse() =>
        Assert.IsFalse(NarrativeHorseGuardPatchHelper.CanSpawnMount(true, true, true, false));

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePatch_IsAPrefixWithTheNamesHarmonyBinds_AndHasNoFinalizer()
    {
        var type = typeof(CharacterCreationNarrativeStageView_SpawnNonHuman_Patch);
        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.IsFalse(methods.Any(m => m.GetCustomAttribute<HarmonyFinalizer>() != null || m.Name == "Finalizer"),
            "A finalizer here swallowed foreign NREs; the guard is the prefix.");
        var prefix = methods.Single(m => m.Name == "Prefix");
        Assert.AreEqual(typeof(bool), prefix.ReturnType);
        CollectionAssert.AreEqual(new[] { "character", "____characterScene" },
            prefix.GetParameters().Select(p => p.Name).ToArray());
    }
}
