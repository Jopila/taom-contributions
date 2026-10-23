using TAOM.Features.ShaderPrecompilation.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ShaderPrecompilation;

/// <summary>Builds the per-item game manager of the shader walk (ADR-013: it derives from a CustomBattle type, so it
/// lives in TAOM.Client). Returns MBGameManager because the CustomBattle assembly is absent headless. Null: the
/// runner skips the item.</summary>
public interface IShaderPrecompileGameManagerFactory
{
    MBGameManager? Create(PrecompileItem item, int generation);
}
