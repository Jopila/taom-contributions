using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ShaderPrecompilation;

/// <summary>
/// ADR-013, B4c. TaomShaderGameManager is a CustomBattle game manager, so it lives in TAOM.Client and the runner
/// builds it through a core factory. The walk is parked (SubModule.cs), but its wiring must stay valid so the
/// re-enable remains an uncomment.
/// </summary>
[TestClass]
public class ShaderPrecompilationWiringTests
{
    [TestMethod]
    public void NullShaderPrecompileGameManagerFactory_Create_ReturnsNull() =>
        Assert.IsNull(new NullShaderPrecompileGameManagerFactory().Create(null!, 1));

    [TestMethod]
    public void Runner_ValidatesOverTheFeatureRegistrations()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<IPathService>());
        container.RegisterInstance(Substitute.For<IObjectManagerAdapter>());
        ShaderPrecompilationIoC.RegisterShaderPrecompilationFeature(container);

        var errors = container.Validate(typeof(ShaderPrecompileRunner));

        Assert.AreEqual(0, errors.Length, string.Join("; ", errors.Select(e => e.Value.Message)));
        Assert.IsInstanceOfType(container.Resolve<IShaderPrecompileGameManagerFactory>(), typeof(NullShaderPrecompileGameManagerFactory));
    }

    [TestMethod]
    public void StartCurrentItem_BuildsTheManagerThroughTheFactory()
    {
        var src = RepoPaths.ReadSource("Main/Features/ShaderPrecompilation/ShaderPrecompileRunner.cs", stripComments: true);

        StringAssert.Contains(src, "_games.Create(item, gen)");
        Assert.IsFalse(src.Contains("new TaomShaderGameManager("), "the runner must not construct the client game manager");
        int nullBranch = src.IndexOf("if (manager == null)", System.StringComparison.Ordinal);
        Assert.IsTrue(nullBranch >= 0, "a null manager takes the skip path");
        var branch = src.Substring(nullBranch, 400);
        StringAssert.Contains(branch, "_abortedItems++;");
        StringAssert.Contains(branch, "BeginEnd();");
    }
}
