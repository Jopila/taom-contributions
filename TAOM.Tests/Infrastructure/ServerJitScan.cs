using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The walk behind <see cref="ServerJitGateTests"/>: which methods of an assembly name a client-only type, keyed
/// by user-level method. A compiler-generated name (<c>&lt;Foo&gt;b__12_0</c>, <c>&lt;&gt;c__DisplayClass5_0</c>) renumbers
/// when unrelated code in the type changes, so closures, local functions and iterator or async state machines fold
/// into the method that declares them, and overloads merge. The client test is a parameter so a fixture can stand
/// in a loaded assembly for a missing one (<c>ServerJitFixtureTests</c>). The walk asks whether a referenced type comes
/// from a client assembly, not whether loading it needs one: a type from a present assembly with a client base or value
/// field, or a call into a present assembly whose callee body names a client type, is not flagged. Today
/// <c>ServerLoadGateTests</c> (core types) and <c>DependenciesClientIlTests</c> (TAOM.Dependencies) close those, and a
/// UIExtenderEx or MCM upgrade could reopen them.
/// </summary>
internal static class ServerJitScan
{
    internal const BindingFlags AllDeclared = BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>Any attribute with this name declared in the scanned assembly marks a class as client-only (B6a's is class-only).</summary>
    private const string MarkerName = "ClientOnlyAttribute";

    /// <summary>
    /// Every method and constructor (static constructors included) of <paramref name="types"/> that names a client
    /// type and is not exempt, as keys. A body that cannot be read goes to <paramref name="unreadable"/>.
    /// </summary>
    internal static SortedSet<string> Offenders(
        IEnumerable<Type> types,
        Func<Assembly, bool> isClient,
        out List<string> unreadable,
        out int scanned,
        out Dictionary<string, string> firstReference)
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        unreadable = new List<string>();
        firstReference = new Dictionary<string, string>(StringComparer.Ordinal);
        scanned = 0;

        foreach (var type in types)
        {
            var methods = type.GetMethods(AllDeclared).Cast<MethodBase>()
                .Concat(type.GetConstructors(AllDeclared));
            foreach (var method in methods)
            {
                scanned++;
                List<string> references;
                try
                {
                    references = IlClientReferences.ClientReferences(method, isClient);
                }
                catch (Exception ex)
                {
                    unreadable.Add(type.FullName + "." + method.Name + ": " + ex.GetType().Name + " " + ex.Message);
                    continue;
                }

                if (references.Count == 0 || IsExempt(method))
                    continue;
                var key = KeyOf(method);
                offenders.Add(key);
                if (!firstReference.ContainsKey(key))
                    firstReference[key] = references[0];
            }
        }
        return offenders;
    }

    /// <summary><c>&lt;owner type FullName&gt;::&lt;user method name&gt;</c>.</summary>
    internal static string KeyOf(MethodBase method)
    {
        var (owner, name) = Resolve(method);
        return owner.FullName + "::" + name;
    }

    /// <summary>
    /// A key is exempt when its owner type or any enclosing type carries the marker. Closures and state machines fold
    /// into their declaring method's key, so they share its owner.
    /// </summary>
    internal static bool IsExempt(MethodBase method)
    {
        var (owner, _) = Resolve(method);
        var scanned = owner.Assembly;
        for (var t = owner; t != null; t = t.DeclaringType)
            if (HasMarker(t, scanned))
                return true;
        return false;
    }

    internal static (List<string> Added, List<string> Stale) Compare(IEnumerable<string> live, IEnumerable<string> baseline)
    {
        var liveSet = new HashSet<string>(live, StringComparer.Ordinal);
        var baselineSet = new HashSet<string>(baseline, StringComparer.Ordinal);
        return (
            liveSet.Where(k => !baselineSet.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList(),
            baselineSet.Where(k => !liveSet.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList());
    }

    private static (Type Owner, string Name) Resolve(MethodBase method)
    {
        var type = method.DeclaringType!;
        string? fromStateMachine = null;
        while (type.IsNested && IsGenerated(type))
        {
            fromStateMachine ??= UserName(type.Name);
            type = type.DeclaringType!;
        }
        return (type, fromStateMachine ?? UserName(method.Name) ?? method.Name);
    }

    /// <summary>
    /// The attribute is not on every generated type (the state machine of an async lambda lacks it in this build), the
    /// angle bracket in the name is: C# identifiers cannot contain one.
    /// </summary>
    private static bool IsGenerated(Type type) =>
        type.Name.StartsWith("<", StringComparison.Ordinal) || type.IsDefined(typeof(CompilerGeneratedAttribute), false);

    /// <summary>The user-level name inside a compiler-generated one (<c>&lt;Items&gt;d__3</c>, <c>&lt;Outer&gt;g__Local|0_1</c>), else null.</summary>
    private static string? UserName(string generated)
    {
        if (generated.Length == 0 || generated[0] != '<')
            return null;
        var start = 0;
        while (start < generated.Length && generated[start] == '<')
            start++;
        var end = generated.IndexOf('>', start);
        return end > start ? generated.Substring(start, end - start) : null;
    }

    private static bool HasMarker(MemberInfo member, Assembly scanned) =>
        CustomAttributeData.GetCustomAttributes(member)
            .Any(a => a.AttributeType.Name == MarkerName && a.AttributeType.Assembly == scanned);
}
