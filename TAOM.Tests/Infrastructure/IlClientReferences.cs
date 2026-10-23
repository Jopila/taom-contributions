using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The token walk behind the ADR-013 IL gates: every type a method names in its signature, its IL operands
/// (call, field, type and ldtoken, a resolved generic method's own type arguments included), its locals and its
/// catch clauses, flattened through arrays and generic arguments. The JIT fails a method whose IL names an
/// unresolvable type before its first line runs, so a method a dedicated server compiles must name none of the
/// client-only assemblies. Plain reflection and <see cref="IlCallScanner"/>; no new package.
/// </summary>
internal static class IlClientReferences
{
    private static IEnumerable<Type> TypesOfSignature(MethodBase method)
    {
        if (method is MethodInfo mi)
            yield return mi.ReturnType;
        foreach (var p in method.GetParameters())
            yield return p.ParameterType;
    }

    internal static IEnumerable<Type> TypesReferencedBy(MethodBase method)
    {
        foreach (var t in TypesOfSignature(method))
            yield return t;

        var body = method.GetMethodBody();
        if (body == null)
            yield break;

        foreach (var member in IlCallScanner.ExtractReferencedMembers(method, body.GetILAsByteArray()!))
        {
            switch (member)
            {
                case Type t:
                    yield return t;
                    break;
                case FieldInfo f:
                    yield return f.DeclaringType!;
                    yield return f.FieldType;
                    break;
                case MethodInfo m:
                    yield return m.DeclaringType!;
                    foreach (var t in TypesOfSignature(m)) yield return t;
                    if (m.IsGenericMethod)
                        foreach (var a in m.GetGenericArguments()) yield return a;
                    break;
                case ConstructorInfo c:
                    yield return c.DeclaringType!;
                    foreach (var t in TypesOfSignature(c)) yield return t;
                    break;
            }
        }

        foreach (var local in body.LocalVariables)
            yield return local.LocalType;
        foreach (var clause in body.ExceptionHandlingClauses)
            if (clause.Flags == ExceptionHandlingClauseOptions.Clause)
                yield return clause.CatchType;
    }

    private static IEnumerable<Type> Flatten(Type t)
    {
        if (t.HasElementType)
        {
            foreach (var e in Flatten(t.GetElementType()!)) yield return e;
            yield break;
        }
        yield return t;
        if (t.IsGenericType)
            foreach (var a in t.GetGenericArguments())
                foreach (var e in Flatten(a)) yield return e;
    }

    /// <summary>
    /// What a calli instruction reports instead of its types. Its operand is a stand-alone signature, which this
    /// walk does not decode, and the JIT still resolves every type in it, so the gate fails closed on one.
    /// </summary>
    internal const string UndecodedCalliSignature = "calli (stand-alone signature not decoded; the gate fails closed)";

    /// <summary>
    /// What a call to a vararg method reports instead of its optional argument types. They sit only in the
    /// call-site signature, which resolving the member to a <see cref="MethodBase"/> drops, so the gate fails closed.
    /// </summary>
    internal const string UndecodedVarargCallSite = "vararg call (call-site argument types not decoded; the gate fails closed)";

    /// <summary>Full names of the types <paramref name="method"/> names from an assembly the predicate calls client-only.</summary>
    internal static List<string> ClientReferences(MethodBase method, Func<Assembly, bool> isClientAssembly)
    {
        var names = TypesReferencedBy(method).SelectMany(Flatten)
            .Where(t => isClientAssembly(t.Assembly))
            .Select(t => t.FullName ?? t.Name).Distinct().ToList();
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il != null && IlCallScanner.ExtractOpCodes(il).Contains(OpCodes.Calli))
            names.Add(UndecodedCalliSignature);
        if (il != null && IlCallScanner.ExtractReferencedMembers(method, il)
                .OfType<MethodBase>().Any(m => (m.CallingConvention & CallingConventions.VarArgs) != 0))
            names.Add(UndecodedVarargCallSite);
        return names;
    }

    /// <summary>Full names of the types <paramref name="method"/> names from a <see cref="ClientAssemblies"/> assembly.</summary>
    internal static List<string> ClientReferences(MethodBase method) =>
        ClientReferences(method, a => ClientAssemblies.IsClientOnly(a.GetName().Name ?? ""));
}
