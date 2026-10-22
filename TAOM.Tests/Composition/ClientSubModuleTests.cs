using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Composition;

namespace TAOM.Tests.Composition;

/// <summary>The client submodule hands core its composition from its constructor (ADR-013).</summary>
[TestClass]
public class ClientSubModuleTests
{
    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Constructor_SetsTheClientCompositionSlot()
    {
        ClientComposition.Current = null;

        _ = new ClientSubModule();

        Assert.IsInstanceOfType(ClientComposition.Current, typeof(TaomClientComposition));
    }

    // Each move batch (B4a to B5) adds its rows here. This is the container round trip: DryIoc's
    // constructor selection fails at Register time, which no other test exercises for the client.
    [TestMethod]
    public void TaomClientComposition_RegisterServices_RegistersExactlyTheExpectedServices()
    {
        var container = new Container();
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        var expected = new System.Type[0];
        var actual = container.GetServiceRegistrations().Select(r => r.ServiceType).ToArray();
        CollectionAssert.AreEquivalent(expected, actual);
    }
}
