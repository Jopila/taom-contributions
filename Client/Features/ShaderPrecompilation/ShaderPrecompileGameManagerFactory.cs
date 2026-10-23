using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation;
using TAOM.Features.ShaderPrecompilation.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Client.Features.ShaderPrecompilation;

/// <summary>Builds the per-item CustomBattle game manager of the shader walk (ADR-013); core's null factory builds none.</summary>
internal sealed class ShaderPrecompileGameManagerFactory : IShaderPrecompileGameManagerFactory
{
    private readonly IShaderPrecompilationService _service;
    private readonly IModLogger _logger;

    public ShaderPrecompileGameManagerFactory(IShaderPrecompilationService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public MBGameManager? Create(PrecompileItem item, int generation) =>
        new TaomShaderGameManager(item, generation, _service, _logger);
}
