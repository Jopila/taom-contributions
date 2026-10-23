using System;

namespace TAOM;

/// <summary>
/// Marks a TAOM.dll Harmony patch class that a dedicated server must never index (ADR-013): its target, a patch method
/// signature, or what its TargetMethod returns lives in an assembly a server cannot load when TAOM boots, or in a
/// submodule a server never loads. PatchCategoryIndex.Build leaves a marked class out on a server before it reads the
/// class attributes; on a client it changes nothing. ServerLoadGateTests.PatchClassesReferencingClientAssemblies_AreClientOnly
/// requires the marker on exactly those classes. Public because the CLR resolves an attribute's constructor against the
/// decorated type's own assembly, so the emitted probe assemblies in PatchCategoryIndexTests cannot carry an internal one.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ClientOnlyAttribute : Attribute
{
}
