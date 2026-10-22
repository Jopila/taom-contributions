using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Tests.Adapters;

/// <summary>ADR-013, B4a. The runtime sprite type is client-only, so the sprite table is a seam.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FrontEndResourceAdapterTests
{
    [TestMethod]
    public void RegisterSprite_DelegatesToTheSpriteTable()
    {
        var table = Substitute.For<IFrontEndSpriteTableAdapter>();
        var adapter = new FrontEndResourceAdapter(Substitute.For<IModLogger>(), table);
        var texture = new FrontEndTexture(new object(), 0);
        var nine = new NinePatch(1, 2, 3, 4);

        adapter.RegisterSprite("taom_x", texture, nine);

        table.Received(1).RegisterSprite("taom_x", texture, nine);
    }
}
