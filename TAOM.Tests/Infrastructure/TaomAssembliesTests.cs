using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Composition;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure;

/// <summary>Pins that the shared scan list really includes the client, so a widened gate cannot quietly shrink back to core.</summary>
[TestClass]
public class TaomAssembliesTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void All_HoldsCoreAndTheClientAssembly()
    {
        CollectionAssert.AreEqual(new[] { "TAOM", "TAOM.Client" }, TaomAssemblies.All.Select(a => a.GetName().Name).ToArray());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void FindCallers_EnumeratesAMethodDeclaredInTheClientAssembly()
    {
        // Mutation note: with All reduced to core alone this finds nothing, because the only caller of
        // ClientComposition.set_Current is the TAOM.Client.ClientSubModule constructor.
        var callers = TaomAssemblies.FindCallers(
            m => m.DeclaringType == typeof(ClientComposition) && m.Name == "set_Current",
            out var unreadable, out var scanned);

        CollectionAssert.Contains(callers, typeof(ClientSubModule).FullName + "..ctor");
        Assert.IsTrue(scanned > 1000, "Only " + scanned + " bodies scanned across the TAOM assemblies.");
        Assert.AreEqual(0, unreadable.Count, string.Join("; ", unreadable));
    }
}
