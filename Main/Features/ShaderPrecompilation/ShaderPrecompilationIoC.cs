using DryIoc;
using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation.Hooks;

namespace TAOM.Features.ShaderPrecompilation;

public static class ShaderPrecompilationIoC
{
    public static void RegisterShaderPrecompilationFeature(IContainer container)
    {
        container.Register<IShaderPrecompilationService, ShaderPrecompilationService>(Reuse.Singleton);
        container.Register<IPrecompileSceneProvider, PrecompileSceneProvider>(Reuse.Singleton);
        container.Register<IShaderPrecompileCrashGuard, ShaderPrecompileCrashGuard>(Reuse.Singleton);
        // ADR-013: the game manager is a CustomBattle type, client-only; TAOM.Client replaces this null factory.
        container.Register<IShaderPrecompileGameManagerFactory, NullShaderPrecompileGameManagerFactory>(Reuse.Singleton);
        container.Register<ShaderPrecompileRunner>(Reuse.Singleton);
    }

    public static void InitializeHooks(IModLogger logger, ShaderPrecompileRunner runner)
    {
        LoadingScreen_ShaderProgress_Patch.Initialize(logger, runner);
    }
}
