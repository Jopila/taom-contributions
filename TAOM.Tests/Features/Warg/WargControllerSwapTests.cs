using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Warg;
using TAOM.Features.Warg.BehaviorTreeElements;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// ADR-013, B4c. The rage-mode controller is a MissionView in TAOM.Client; the Warg code in core finds it through
/// <c>IAutonomousMovementController</c> and the first behavior implementing it (the engine's [DefaultView]
/// instance, the one the class lookup returned before the move).
/// </summary>
[TestClass]
public class WargControllerSwapTests
{
    private static readonly string[] CoreFiles =
    {
        "Main/Features/Warg/WargMissionBehavior.cs",
        "Main/Features/Warg/WargRiderHandManager.cs",
        "Main/Features/Warg/BehaviorTreeElements/FinishRageMode.cs",
        "Main/Features/Warg/BehaviorTreeElements/PrepareOnFirstCall.cs",
    };

    private const string Lookup = "GetMissionBehavior<IAutonomousMovementController>()";

    [TestMethod]
    public void CoreWargCode_FindsTheControllerThroughTheInterface()
    {
        foreach (var file in CoreFiles)
        {
            // The one log line that keeps the old class name is a string literal (boot check 4 greps it), not a type use.
            var src = RepoPaths.ReadSource(file, stripComments: true)
                .Replace("\"[Warg] AutonomousMovementPlayerController not found\"", "\"\"");
            Assert.IsFalse(src.Contains("AutonomousMovementPlayerController"), file + " names the client class");
            Assert.IsFalse(src.Contains("MissionMainAgentController"), file + " names vanilla's View controller");
            Assert.IsFalse(src.Contains("MountAndBlade.View"), file + " uses the View namespace");
            StringAssert.Contains(src, Lookup, file);
        }
    }

    [TestMethod]
    public void SwitchMainAgentController_HandsTheSwapToTheFirstController()
    {
        var src = RepoPaths.ReadSource("Main/Features/Warg/WargMissionBehavior.cs", stripComments: true);
        var squashed = Regex.Replace(src, @"\s+", " ");

        StringAssert.Contains(squashed,
            "Mission.Current.GetMissionBehavior<IAutonomousMovementController>()?.SwitchMainAgentController(switchToSpecialController)");
    }

    [TestMethod]
    public void ClientController_SwitchesVanillasControllerOffBeforeItselfOn()
    {
        var src = RepoPaths.ReadSource("Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs", stripComments: true);
        StringAssert.Contains(src, "GetMissionBehavior<MissionMainAgentController>()");

        int method = src.IndexOf("public void SwitchMainAgentController(bool switchToSpecialController)", StringComparison.Ordinal);
        Assert.IsTrue(method >= 0, "the swap method moved with the controller");
        // Whitespace collapsed, so each branch must hold both calls, adjacent and in order: a deleted or reversed
        // call fails (Codex B4c review F2: an IndexOf comparison passed with a call missing).
        var body = Regex.Replace(src.Substring(method), @"\s+", " ");
        StringAssert.Contains(body, "if (switchToSpecialController) { baseController.Disable(); Enable(); }",
            "rage: vanilla off, then the special on");
        StringAssert.Contains(body, "else { baseController.Enable(); Disable(); }",
            "end: vanilla on, then the special off");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void CoreWargMethods_ReferenceNothingInTheViewAssembly()
    {
        GameAssemblies.EnsureLoaded();
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var viewAssembly = System.Reflection.Assembly.Load("TaleWorlds.MountAndBlade.View");
        var roots = new[] { typeof(WargMissionBehavior), typeof(WargRiderHandManager), typeof(FinishRageMode), typeof(PrepareOnFirstCall) };

        var offenders = new List<string>();
        foreach (var root in roots)
        foreach (var type in new[] { root }.Concat(root.GetNestedTypes(all)))
        foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
        {
            var il = method.GetMethodBody()?.GetILAsByteArray();
            if (il == null) continue;
            foreach (var member in IlCallScanner.ExtractReferencedMembers(method, il))
            {
                var declaring = member as Type ?? member.DeclaringType;
                if (declaring != null && declaring.Assembly == viewAssembly)
                    offenders.Add(type.Name + "." + method.Name + " -> " + declaring.FullName);
            }
        }

        CollectionAssert.AreEqual(new string[0], offenders.Distinct().ToList(),
            "core Warg code must not touch TaleWorlds.MountAndBlade.View (ADR-013)");
    }
}
