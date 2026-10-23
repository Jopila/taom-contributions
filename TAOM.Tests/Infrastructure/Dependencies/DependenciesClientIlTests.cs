using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using TAOM.Tests.Migration;
using static TAOM.Tests.Infrastructure.RepoPaths;
using DepsSubModule = TAOM.Dependencies.SubModule;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// TAOM.Dependencies must construct on the dedicated server, where the Gauntlet layer does not resolve
/// when the module loads (<c>Bannerlord.UIExtenderEx.dll</c> itself resolves on the current staging, but
/// the Gauntlet assemblies its static init patches do not). The JIT fails a method whose IL names an
/// unresolvable type before its first line runs, so no method of the assembly may name one except the two
/// NoInlining helpers that only a client calls. The walk covers call, field, type and ldtoken operands
/// (a resolved generic method's own type arguments included), locals, catch clauses and signatures, over
/// every type of the assembly with compiler-generated ones. Plain reflection and
/// <see cref="IlCallScanner"/>; no new package.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class DependenciesClientIlTests
{
    private const string UiExtenderAssembly = "Bannerlord.UIExtenderEx";
    private static readonly string[] HelperNames = { "ConfigureGauntletPrefabs", "ForceUiExtenderStaticInit" };

    private const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static bool IsClientAssembly(Assembly a)
    {
        var name = a.GetName().Name ?? "";
        return name.Equals(UiExtenderAssembly, StringComparison.OrdinalIgnoreCase) || ClientAssemblies.IsClientOnly(name);
    }

    private static List<string> ClientReferences(MethodBase method) =>
        IlClientReferences.ClientReferences(method, IsClientAssembly);

    private static MethodBase Cctor => typeof(DepsSubModule).TypeInitializer!;

    private static MethodBase OnSubModuleLoad => typeof(DepsSubModule).GetMethod("OnSubModuleLoad", AllDeclared)!;

    private static MethodInfo Helper(string name) =>
        typeof(DepsSubModule).GetMethod(name, AllDeclared)
        ?? throw new AssertFailedException("Missing helper " + name);

    private static bool IsHelper(MethodBase m) =>
        m.DeclaringType == typeof(DepsSubModule) && HelperNames.Contains(m.Name);

    private const string ServerCheck = "call GameNetwork::get_IsDedicatedServer()";

    private static List<string> FingerprintOf(MethodBase method) =>
        IlCallScanner.Fingerprint(method, method.GetMethodBody()!.GetILAsByteArray()!).ToList();

    /// <summary>
    /// The helper call must follow the engine's server check and its branch: the check, the very next token a
    /// conditional branch, and the one helper call after both. Removing the <c>if</c> drops the check and the branch.
    /// </summary>
    private static void AssertHelperGuarded(MethodBase method, string helper)
    {
        var fp = FingerprintOf(method);
        var check = fp.IndexOf(ServerCheck);
        var call = fp.FindIndex(t => t.StartsWith("call ", StringComparison.Ordinal) && t.Contains("::" + helper + "("));
        var where = method.Name + " fingerprint: " + string.Join(" | ", fp);

        Assert.IsTrue(check >= 0, "no read of GameNetwork.IsDedicatedServer; " + where);
        Assert.IsTrue(check + 1 < fp.Count && (fp[check + 1] == "brtrue" || fp[check + 1] == "brfalse"),
            "the server check is not followed by a conditional branch; " + where);
        Assert.IsTrue(call > check + 1, helper + " is not reached after the server check and its branch; " + where);
        Assert.AreEqual(1, fp.Count(t => t.Contains("::" + helper + "(")), helper + " must be called exactly once; " + where);
    }

    [TestMethod]
    public void StaticConstructor_UiHelperCall_IsGuardedByTheServerCheck()
    {
        AssertHelperGuarded(Cctor, "ConfigureGauntletPrefabs");
    }

    [TestMethod]
    public void OnSubModuleLoad_UiHelperCall_IsGuardedByTheServerCheck()
    {
        AssertHelperGuarded(OnSubModuleLoad, "ForceUiExtenderStaticInit");
    }

    [TestMethod]
    public void UiHelpers_HoldTheClientReferences()
    {
        foreach (var name in HelperNames)
            Assert.IsTrue(ClientReferences(Helper(name)).Count > 0, name + " references no client type");
    }

    [TestMethod]
    public void UiHelpers_AreNoInlining()
    {
        foreach (var name in HelperNames)
            Assert.IsTrue(Helper(name).MethodImplementationFlags.HasFlag(MethodImplAttributes.NoInlining),
                name + " must be NoInlining so its client references never land in a caller");
    }

    [TestMethod]
    public void EveryMethodInTheAssemblyExceptTheHelpers_ReferencesNoClientType()
    {
        var methods = typeof(DepsSubModule).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(AllDeclared).Cast<MethodBase>().Concat(t.GetConstructors(AllDeclared)))
            .Where(m => !IsHelper(m))
            .ToList();
        Assert.IsTrue(methods.Count > 20, "the walk found only " + methods.Count + " methods");

        var offenders = methods
            .Select(m => (Name: m.DeclaringType!.FullName + "." + m.Name, Refs: ClientReferences(m)))
            .Where(x => x.Refs.Count > 0)
            .Select(x => x.Name + " -> " + string.Join(", ", x.Refs)).ToList();
        CollectionAssert.AreEqual(new List<string>(), offenders);
    }

    [TestMethod]
    public void Manifest_EverySubModuleButDependenciesIsTaggedClientOnly_AndDependenciesIsNot()
    {
        var path = RepoPath("Dependencies", "_Module", "SubModule.xml");
        var subs = XDocument.Load(path).Root!.Element("SubModules")!.Elements("SubModule")
            .ToDictionary(s => (string)s.Element("Name")!.Attribute("value")!);

        string? Tag(string sub, string key) => subs[sub].Element("Tags")?.Elements("Tag")
            .FirstOrDefault(t => (string?)t.Attribute("key") == key)?.Attribute("value")?.Value;

        Assert.IsTrue(subs.ContainsKey("TAOM.Dependencies"));
        Assert.IsTrue(subs.ContainsKey("UIExtenderEx") && subs.ContainsKey("Bannerlord Module Loader"));
        foreach (var name in subs.Keys.Where(n => n != "TAOM.Dependencies"))
        {
            Assert.AreEqual("none", Tag(name, "DedicatedServerType"), name);
            Assert.AreEqual("false", Tag(name, "IsNoRenderModeElement"), name);
        }
        Assert.IsNull(Tag("TAOM.Dependencies", "DedicatedServerType"));
        Assert.IsNull(Tag("TAOM.Dependencies", "IsNoRenderModeElement"));
    }
}
