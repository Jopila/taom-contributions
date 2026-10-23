using System;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The ADR-013 IL walk on a calli: its operand is a stand-alone signature the walk does not decode, and the JIT
/// still resolves every type in it, so a body holding one must fail the gate rather than read as clean. The body is
/// emitted, because TAOM.Tests does not allow the unsafe code a C# function pointer needs.
/// </summary>
[TestClass]
public class IlClientReferencesTests
{
    private static MethodInfo Emit(string name, Action<ILGenerator> body)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("IlClientReferencesFixture_" + name),
            AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("m").DefineType("Fixture", TypeAttributes.Public | TypeAttributes.Class);
        var method = type.DefineMethod(name, MethodAttributes.Public | MethodAttributes.Static, typeof(void), Type.EmptyTypes);
        body(method.GetILGenerator());
        return type.CreateType()!.GetMethod(name)!;
    }

    [TestMethod]
    public void ClientReferences_CalliWhoseSignatureNamesAType_FailsClosed()
    {
        // (delegate*<Uri>)0)(): the only place Uri appears is the calli signature.
        var method = Emit("Calli", il =>
        {
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Conv_I);
            il.EmitCalli(OpCodes.Calli, CallingConventions.Standard, typeof(Uri), Type.EmptyTypes, null);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
        });

        CollectionAssert.Contains(IlClientReferences.ClientReferences(method, a => a == typeof(Uri).Assembly),
            IlClientReferences.UndecodedCalliSignature);
    }

    [TestMethod]
    public void ClientReferences_BodyWithoutCalli_ReportsNothing()
    {
        var method = Emit("Plain", il =>
        {
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
        });

        Assert.AreEqual(0, IlClientReferences.ClientReferences(method, a => a == typeof(Uri).Assembly).Count);
    }
}
