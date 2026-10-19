using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Bannerlord.UIExtenderEx.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.UI;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.ShadowfaxScreens;

/// <summary>
/// Pins the screens Shadowfax Screens makes instant. The vanilla movies are patched through
/// UIExtenderEx at load; TAOM's own prefabs carry the zero durations in their XML. That each patched
/// movie name exists in the installed game is checked by
/// <see cref="PrefabExtensionBindingTests"/>.
/// </summary>
[TestClass]
public class ShadowfaxScreensPrefabTests
{
    private static readonly string[] PatchedMovies =
    {
        "ClanScreen", "ClanMembers", "KingdomManagement", "FiefsPanel", "CharacterDeveloper",
        "Inventory", "InventoryEquippedItemControls", "InventoryItemTuple", "QuestsScreen",
    };

    private static readonly string[] VanillaGuiModules =
        { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle" };

    [TestMethod]
    public void ShadowfaxScreensPrefab_Attributes_TargetExactlyTheNineVanillaMovies()
    {
        var movies = typeof(ShadowfaxScreensPrefab)
            .GetCustomAttributes(typeof(PrefabExtensionAttribute), false)
            .Cast<PrefabExtensionAttribute>()
            .Select(a => a.Movie)
            .OrderBy(m => m)
            .ToArray();

        CollectionAssert.AreEqual(PatchedMovies.OrderBy(m => m).ToArray(), movies);
    }

    // The rewriter assumes two things about the vanilla data: each movie still has an animated
    // definition to zero, and no VisualState carries its own TransitionDuration, which
    // Widget.UpdateVisualDefinitions would take over the definition's.
    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    [TestCategory("LiveInstall")]
    public void MakeInstant_EachVanillaTargetMovie_LeavesNoAnimatedState()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game install unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var failures = new StringBuilder();
        foreach (var movie in PatchedMovies)
        {
            // GUI/Prefabs only: SandBox/GUI/Brushes also holds a CharacterDeveloper.xml and an Inventory.xml.
            var path = VanillaGuiModules
                .Select(m => Path.Combine(GameAssemblies.GameDir, "Modules", m, "GUI", "Prefabs"))
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, movie + ".xml", SearchOption.AllDirectories))
                .LastOrDefault();
            if (path == null)
            {
                failures.AppendLine($"{movie}: no vanilla prefab");
                continue;
            }

            var doc = new XmlDocument();
            doc.Load(path);
            if (InstantTransitionRewriter.MakeInstant(doc) == 0)
                failures.AppendLine($"{movie}: nothing left to make instant");

            foreach (XmlElement state in doc.SelectNodes("//VisualDefinition/VisualState[@TransitionDuration]")!)
                if (state.GetAttribute("TransitionDuration") != "0")
                    failures.AppendLine($"{movie}: state '{state.GetAttribute("State")}' keeps its own TransitionDuration");
        }

        Assert.AreEqual(0, failures.Length, failures.ToString());
    }

    [DataTestMethod]
    [DataRow("Main/_Module/GUI/PreFabs/Party/PartyScreen.xml")]
    [DataRow("Main/_Module/GUI/PreFabs/Party/PartyTroopTuple.xml")]
    [DataRow("Main/_Module/GUI/PreFabs/Party/PartyTroopTupleLeft.xml")]
    [DataRow("Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml")]
    public void TaomPrefab_AlreadyInstant_RewriterChangesNothing(string path)
    {
        var doc = new XmlDocument();
        doc.LoadXml(ReadSource(path));

        Assert.AreEqual(0, InstantTransitionRewriter.MakeInstant(doc), $"{path}: a VisualDefinition still animates");
    }
}
