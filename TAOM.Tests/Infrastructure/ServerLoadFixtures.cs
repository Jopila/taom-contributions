using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.ServerLoadFixtures;

// System.dll stands in for a client-only assembly (predicate: name == "System"). Each type below is the minimal
// shape of one detector in ServerLoadScan, so a detector that stops firing fails here, not silently in the gate.
// Shape numbers match ClientShape.

public class GenericArgBase : List<Uri> { }

public class EnumField { public HttpStatusCode F; }

public class StaticEnumField { public static ListSortDirection F; }

public class NullableEnumField { public HttpStatusCode? F; }

public class KvpField { public KeyValuePair<Uri, int> F; }

/// <summary>Reference-typed fields never fail GetTypes: no shape.</summary>
public class RefFieldsOnly
{
    public Uri A = null!;
    public List<Uri> B = null!;
    public HttpStatusCode[] C = null!;
}

public class TypeConstraint<T> where T : Uri { }

public class MethodConstraint { public void M<T>() where T : Uri { } }

public class CrossAssemblyOverride : XmlUrlResolver
{
    public override Uri ResolveUri(Uri baseUri, string relativeUri) => baseUri;
}

/// <summary>The base declaration is in this assembly, so naming Uri in the override is no cross-assembly shape.</summary>
public class LocalBase { public virtual void M(Uri u) { } }

public class LocalOverride : LocalBase { public override void M(Uri u) { } }

/// <summary>Overrides across assemblies without naming the stand-in type: no shape.</summary>
public class CleanCrossAssemblyOverride : XmlUrlResolver
{
    public override string ToString() => "";
}

[TestClass]
public class ServerLoadFixtureTests
{
    private static bool StandIn(string assemblyName) => assemblyName == "System";

    private static List<Type> Fixtures() =>
        typeof(ServerLoadFixtureTests).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ServerLoadFixtureTests).Namespace && !t.IsNested
                        && !t.IsDefined(typeof(TestClassAttribute), false))
            .ToList();

    [TestMethod]
    public void FindClientDependentTypes_Fixtures_FlagsExactlyTheEightWithTheirShape()
    {
        var found = ServerLoadScan.FindClientDependentTypes(Fixtures(), StandIn)
            .ToDictionary(kv => kv.Key.Name, kv => (kv.Value.Assembly, kv.Value.Shape), StringComparer.Ordinal);

        var expected = new Dictionary<string, (string, ClientShape)>(StringComparer.Ordinal)
        {
            ["GenericArgBase"] = ("System", ClientShape.Hierarchy),
            ["EnumField"] = ("System", ClientShape.ValueField),
            ["StaticEnumField"] = ("System", ClientShape.ValueField),
            ["NullableEnumField"] = ("System", ClientShape.ValueField),
            ["KvpField"] = ("System", ClientShape.ValueField),
            ["TypeConstraint`1"] = ("System", ClientShape.Constraint),
            ["MethodConstraint"] = ("System", ClientShape.Constraint),
            ["CrossAssemblyOverride"] = ("System", ClientShape.Override),
        };

        CollectionAssert.AreEquivalent(expected.Keys.ToList(), found.Keys.ToList());
        foreach (var kv in expected)
            Assert.AreEqual(kv.Value, found[kv.Key], kv.Key);
    }

    [TestMethod]
    public void FindClientDependentTypes_Fixtures_CoverEveryNegativeAndPositive()
    {
        var names = Fixtures().Select(t => t.Name).ToList();
        foreach (var n in new[] { "RefFieldsOnly", "LocalBase", "LocalOverride", "CleanCrossAssemblyOverride" })
            Assert.IsTrue(names.Contains(n), "negative fixture missing from the namespace scan: " + n);
        Assert.AreEqual(12, names.Count, string.Join(", ", names));
    }
}
