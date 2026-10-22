using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Client;
using TAOM.Client.Adapters;
using TAOM.Client.Features.CustomBattles;
using TAOM.Composition;
using TAOM.Features.CustomBattles;

namespace TAOM.Tests.Composition;

/// <summary>ADR-013, B4a. What TAOM.Client replaces in core's container for the front-end types.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomClientCompositionTests
{
    [TestCleanup]
    public void Cleanup() => ClientComposition.Current = null;

    [TestMethod]
    public void RegisterServices_ReplacesTheThreeFrontEndSeams()
    {
        var container = new Container();
        container.Register<IFactionMapWidgetAdapter, NullFactionMapWidgetAdapter>(Reuse.Singleton);
        container.Register<IFrontEndSpriteTableAdapter, NullFrontEndSpriteTableAdapter>(Reuse.Singleton);
        container.Register<ICustomBattleFactionSelectionFactory, NullCustomBattleFactionSelectionFactory>(Reuse.Singleton);
        ClientComposition.Current = new TaomClientComposition();

        ClientComposition.Apply(container);

        Assert.IsInstanceOfType(container.Resolve<IFactionMapWidgetAdapter>(), typeof(FactionMapWidgetAdapter));
        Assert.IsInstanceOfType(container.Resolve<IFrontEndSpriteTableAdapter>(), typeof(FrontEndSpriteTableAdapter));
        Assert.IsInstanceOfType(container.Resolve<ICustomBattleFactionSelectionFactory>(), typeof(TaomFactionSelectionFactory));
        Assert.AreEqual(3, container.GetServiceRegistrations().Count(), "each seam is replaced, never appended");
    }
}
