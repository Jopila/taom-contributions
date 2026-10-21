using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongMemory;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// Long Memory takes a forced taking whose villagers give way from the selected menu option, because the engine fires no map event
/// for it. The two option ids are strings the engine owns (<c>VillageHostileActionCampaignBehavior.AddGameMenus</c>), so a rename in
/// a Bannerlord update would leave the listener waiting for an id that never comes and the loss would stop with every other gate
/// green. This reads the installed engine's own registration and fails when either pinned id is gone, or no longer belongs to its
/// no-resist menu.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class LongMemoryBindingTests
{
    private const string BehaviorType = "TaleWorlds.CampaignSystem.CampaignBehaviors.VillageHostileActionCampaignBehavior";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static List<CodeInstruction> AddGameMenusIl()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName(BehaviorType);
        Assert.IsNotNull(type, BehaviorType + " did not resolve: the two no-resist option ids cannot be checked.");
        var method = AccessTools.Method(type, "AddGameMenus");
        Assert.IsNotNull(method, "AddGameMenus is gone or ambiguous: the two no-resist option ids cannot be checked.");
        return PatchProcessor.GetOriginalInstructions((MethodBase)method).ToList();
    }

    private static bool IsString(CodeInstruction ci, string value) => ci.opcode == OpCodes.Ldstr && ci.operand is string s && s == value;

    [DataTestMethod]
    [DataRow(LongMemoryRules.SuppliesNoResistOptionId, "force_supplies_no_resist")]
    [DataRow(LongMemoryRules.VolunteersNoResistOptionId, "force_volunteers_no_resist")]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void AddGameMenus_RegistersThePinnedOptionId_OnItsNoResistMenu(string optionId, string menuId)
    {
        var il = AddGameMenusIl();

        // AddGameMenuOption(menuId, optionId, text, ...): the menu id and the option id are consecutive string constants.
        var found = Enumerable.Range(0, il.Count - 1).Any(i => IsString(il[i], menuId) && IsString(il[i + 1], optionId));

        Assert.IsTrue(found, "the engine no longer registers option '" + optionId + "' on menu '" + menuId
            + "' in AddGameMenus: the forced-taking loss for villagers who give way would silently stop (update LongMemoryRules).");
    }
}
