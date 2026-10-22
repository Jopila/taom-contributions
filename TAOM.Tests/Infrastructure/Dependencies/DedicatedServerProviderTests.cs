using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// The dedicated-server answer is the engine's own build constant, <c>GameNetwork.IsDedicatedServer</c> (the
/// server DLL returns true, the client DLL false), read by TAOM.Dependencies directly and by TAOM.dll through
/// <see cref="DedicatedServerProvider"/>, which is the test seam. Reflection only, so it runs on reference assemblies.
/// </summary>
[TestClass]
public class DedicatedServerProviderTests
{
    [TestMethod]
    public void Getter_CallsGameNetworkIsDedicatedServer()
    {
        var getter = typeof(DedicatedServerProvider).GetProperty(nameof(DedicatedServerProvider.IsDedicatedServer))!
            .GetMethod!;
        var expected = typeof(GameNetwork).GetProperty(nameof(GameNetwork.IsDedicatedServer))!.GetMethod!;

        var calls = IlCallScanner.ExtractCalledMethods(getter, getter.GetMethodBody()!.GetILAsByteArray()!).ToList();

        Assert.IsTrue(calls.Any(c => IlCallScanner.SameMethod(c, expected)),
            "DedicatedServerProvider.IsDedicatedServer must read GameNetwork.IsDedicatedServer; called: "
            + string.Join(", ", calls.Select(c => c.DeclaringType?.Name + "." + c.Name)));
    }
}
