using TAOM.Features.ShaderPrecompilation.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ShaderPrecompilation;

/// <summary>Core's default (ADR-013): no game manager. TAOM.Client replaces it on a rendering client.</summary>
public sealed class NullShaderPrecompileGameManagerFactory : IShaderPrecompileGameManagerFactory
{
    public MBGameManager? Create(PrecompileItem item, int generation) => null;
}
