using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// System.dll stands in for a client-only assembly (predicate: assembly name == "System"). Each fixture is the
// minimal shape of one detector in IlClientReferences or one key rule in ServerJitScan, so a detector that
// stops firing fails here, not silently in ServerJitGateTests.

namespace TAOM.Tests.Infrastructure.ServerJitFixtures
{
    public class NewObjOwner { public object M() => new Uri("http://a"); }

    public class StaticFieldOwner { public string M() => Uri.UriSchemeHttp; }

    public class TypeofOwner { public Type M() => typeof(Uri); }

    /// <summary>The local is the only mention: two loads keep the optimizer from eliding it.</summary>
    public class LocalOwner { public void M() { Uri? u = null; GC.KeepAlive(u); GC.KeepAlive(u); } }

    public class ReturnOwner { public Uri? M() => null; }

    public class CatchOwner
    {
        public void M()
        {
            try { Environment.GetEnvironmentVariable("x"); }
            catch (UriFormatException) { }
        }
    }

    public class ParameterOwner { public void M(Uri u) { } }

    public class ArrayParameterOwner { public void M(Uri[] a) { } }

    public class GenericParameterOwner { public void M(List<Uri> a) { } }

    /// <summary>Only the field's type is a client type: the field is declared here.</summary>
    public class FieldTypeOwner
    {
        public Uri? U;

        public bool M() => U is null;
    }

    /// <summary>Only the callee's declaring type is a client type: char in, bool out.</summary>
    public class CalleeDeclaringOwner { public bool M() => Uri.IsHexDigit('a'); }

    /// <summary>Only the callee's parameter is a client type: the callee is declared in a helper namespace.</summary>
    public class CalleeSignatureOwner { public void M() => ServerJitHelpers.UriSink.Take(null); }

    /// <summary>Only the constructor's parameter is a client type.</summary>
    public class CtorSignatureOwner { public object M() => new ServerJitHelpers.UriHolder(null); }

    /// <summary>Only the generic method's type argument is a client type.</summary>
    public class GenericArgumentOwner
    {
        private static void Touch<T>() { }

        public void M() => Touch<Uri>();
    }

    /// <summary>A non-capturing lambda lives in <c>&lt;&gt;c</c>; its key folds into <c>Outer</c>.</summary>
    public class LambdaOwner { public Func<int> Outer() => () => new Uri("http://a").Port; }

    /// <summary>A capturing lambda lives in a <c>&lt;&gt;c__DisplayClass</c> instance.</summary>
    public class ClosureOwner { public Func<int> Outer(int x) => () => x + new Uri("http://a").Port; }

    public class LocalFunctionOwner
    {
        public int Outer()
        {
            int Local() => new Uri("http://a").Port;
            return Local();
        }
    }

    public class IteratorOwner
    {
        public IEnumerable<int> Items()
        {
            yield return new Uri("http://a").Port;
        }
    }

    public class AsyncOwner
    {
        public async Task AsyncM()
        {
            await Task.Yield();
            var u = new Uri("http://a");
            GC.KeepAlive(u);
        }
    }

    /// <summary>An async lambda: the state machine sits inside the closure class, two generated levels deep.</summary>
    public class AsyncLambdaOwner
    {
        public Func<Task> Outer() => async () =>
        {
            await Task.Yield();
            GC.KeepAlive(new Uri("http://a"));
        };
    }

    /// <summary>The static constructor is a method too: a field initializer can name a client type.</summary>
    public class StaticInitOwner { public static readonly object Cached = new Uri("http://a"); }

    public class Clean { public int M() => 1; }
}

// Helpers sit outside the scanned fixture prefix, so only the caller can be an offender.
namespace TAOM.Tests.Infrastructure.ServerJitHelpers
{
    public static class UriSink { public static void Take(Uri? u) { } }

    public class UriHolder { public UriHolder(Uri? u) { } }
}

// The call-site signature of a vararg call carries the optional argument types, which reflection drops.
namespace TAOM.Tests.Infrastructure.ServerJitUnreadableFixtures
{
    public class VarargOwner
    {
        private static void Sink(__arglist) { }

        public void M() => Sink(__arglist((Uri?)null));
    }
}

namespace TAOM.Tests.Infrastructure.ServerJitMarkedFixtures
{
    /// <summary>Class-only, like B6a's <c>Main/ClientOnlyAttribute.cs</c>.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ClientOnlyAttribute : Attribute { }

    [ClientOnly]
    public class MarkedType
    {
        public object M() => new Uri("http://a");

        public class Nested { public object N() => new Uri("http://a"); }
    }

    /// <summary>Not marked: the control that proves the marked fixtures are exempt, not just undetected.</summary>
    public class UnmarkedControl { public object M() => new Uri("http://a"); }
}

namespace TAOM.Tests.Infrastructure
{
    [TestClass]
    public class ServerJitFixtureTests
    {
        private const string Plain = "TAOM.Tests.Infrastructure.ServerJitFixtures.";
        private const string Marked = "TAOM.Tests.Infrastructure.ServerJitMarkedFixtures.";
        private const string Unreadable = "TAOM.Tests.Infrastructure.ServerJitUnreadableFixtures.";

        private static bool StandIn(Assembly a) => a.GetName().Name == "System";

        private static List<Type> TypesIn(string ns) =>
            typeof(ServerJitFixtureTests).Assembly.GetTypes()
                .Where(t => (t.FullName ?? "").StartsWith(ns, StringComparison.Ordinal))
                .ToList();

        private static SortedSet<string> Scan(string ns, out List<string> unreadable, out int scanned) =>
            ServerJitScan.Offenders(TypesIn(ns), StandIn, out unreadable, out scanned, out _);

        [TestMethod]
        public void Offenders_Fixtures_FlagExactlyTheExpectedKeys()
        {
            var found = Scan(Plain, out var unreadable, out var scanned);

            var expected = new[]
            {
                "NewObjOwner::M", "StaticFieldOwner::M", "TypeofOwner::M", "LocalOwner::M", "ReturnOwner::M",
                "CatchOwner::M", "ParameterOwner::M", "ArrayParameterOwner::M", "GenericParameterOwner::M",
                "FieldTypeOwner::M", "CalleeDeclaringOwner::M", "CalleeSignatureOwner::M", "CtorSignatureOwner::M",
                "GenericArgumentOwner::M", "LambdaOwner::Outer", "ClosureOwner::Outer", "LocalFunctionOwner::Outer",
                "IteratorOwner::Items", "AsyncOwner::AsyncM", "AsyncLambdaOwner::Outer", "StaticInitOwner::.cctor",
            }.Select(k => Plain + k).OrderBy(k => k, StringComparer.Ordinal).ToList();

            var (missing, extra) = ServerJitScan.Compare(found, expected);
            Assert.AreEqual(0, missing.Count + extra.Count,
                "unexpected: " + string.Join(", ", missing) + " | not found: " + string.Join(", ", extra));
            CollectionAssert.AreEqual(new List<string>(), unreadable);
            Assert.IsTrue(scanned > expected.Count, "scanned " + scanned);
            Assert.IsFalse(found.Any(k => k.Contains('<')), "a compiler-generated name leaked into a key");
        }

        [TestMethod]
        public void Offenders_Fixtures_HonorClientOnlyOnTypesAndNestedTypes()
        {
            var found = Scan(Marked, out var unreadable, out _);

            var expected = new[] { Marked + "UnmarkedControl::M" }.ToList();

            CollectionAssert.AreEqual(expected, found.ToList());
            CollectionAssert.AreEqual(new List<string>(), unreadable);
        }

        [TestMethod]
        public void Offenders_VarargCallSite_FailsClosed()
        {
            var found = ServerJitScan.Offenders(TypesIn(Unreadable), StandIn, out var unreadable, out _, out var first);

            CollectionAssert.AreEqual(new[] { Unreadable + "VarargOwner::M" }, found.ToList(), "a vararg call site must not read as clean");
            Assert.AreEqual(IlClientReferences.UndecodedVarargCallSite, first[Unreadable + "VarargOwner::M"]);
            CollectionAssert.AreEqual(new List<string>(), unreadable);
        }

        [TestMethod]
        public void Offenders_CalliInstruction_FailsClosed()
        {
            // C# emits calli only for function pointers (unsafe code), so the fixture is built with Reflection.Emit.
            var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName("ServerJitCalliFixture"), System.Reflection.Emit.AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("m").DefineType("CalliOwner", TypeAttributes.Public);
            var il = type.DefineMethod("M", MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes)
                .GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldnull);
            il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0);
            il.Emit(System.Reflection.Emit.OpCodes.Conv_I);
            il.EmitCalli(System.Reflection.Emit.OpCodes.Calli, CallingConventions.Standard, typeof(void), new[] { typeof(Uri) }, null);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);

            var found = ServerJitScan.Offenders(new[] { type.CreateType()! }, StandIn, out var unreadable, out _, out var first);

            CollectionAssert.AreEqual(new[] { "CalliOwner::M" }, found.ToList(), "a calli must not read as clean");
            Assert.AreEqual(IlClientReferences.UndecodedCalliSignature, first["CalliOwner::M"]);
            CollectionAssert.AreEqual(new List<string>(), unreadable);
        }

        [TestMethod]
        public void Compare_ReportsAddedAndStale()
        {
            var (added, stale) = ServerJitScan.Compare(new[] { "a", "b" }, new[] { "b", "c" });

            CollectionAssert.AreEqual(new[] { "a" }, added);
            CollectionAssert.AreEqual(new[] { "c" }, stale);
        }
    }
}
