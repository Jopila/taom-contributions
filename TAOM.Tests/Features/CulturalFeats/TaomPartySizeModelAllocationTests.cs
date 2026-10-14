using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CulturalFeats.Models;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CulturalFeats;

/// <summary>
/// #765 M8 review E11: <c>GetPartyMemberSizeLimit</c> runs for every party on every roster change, worker threads
/// included. A lambda over its <c>party</c> parameter would allocate a closure and a delegate on every call, even with
/// no hunt; the Eye of Sauron's weighted-count delegate is built once in the constructor instead.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // resolves TaleWorlds member tokens in the method body
public class TaomPartySizeModelAllocationTests
{
    [TestMethod]
    public void GetPartyMemberSizeLimit_ConstructsNoDelegateOrClosure()
    {
        var method = typeof(TaomPartySizeModel).GetMethod(nameof(TaomPartySizeModel.GetPartyMemberSizeLimit))!;

        var constructed = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray())
            .OfType<ConstructorInfo>()
            .Select(c => c.DeclaringType!)
            .Where(t => typeof(Delegate).IsAssignableFrom(t) || t.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .ToList();

        Assert.AreEqual(0, constructed.Count,
            "GetPartyMemberSizeLimit allocates per call: " + string.Join(", ", constructed.Select(t => t.Name)));
    }
}
