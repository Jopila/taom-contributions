using System.IO;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TaleWorlds.Library;

namespace TAOM.Features.ShaderSourceInstall;

/// <summary>
/// Installs the module's shader sources (#916). The engine compiles shaders only from
/// <c>&lt;game&gt;\Shaders\Sources</c>, so the copy runs at ProcessLoad (OnSubModuleLoad), skipped on a dedicated
/// server, which compiles nothing. A failure waits for MainMenu, the first point where an inquiry survives
/// the splash video. MainMenu runs once per process, so the warning needs no latch of its own.
/// </summary>
internal sealed class ShaderSourceInstallModule : TaomFeatureModule
{
    private string? _failedGameFolder;

    public override string Id => "ShaderSourceInstall";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IShaderSourceFileSystem, ShaderSourceFileSystem>(Reuse.Singleton);
        registrator.Register<ShaderSourceInstallService>(Reuse.Singleton);
    }

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.ProcessLoad)
            Install(resolver);
        else if (phase == ApplyPhase.MainMenu && _failedGameFolder != null)
            ShaderSourceInstallEntry.ShowFailureInquiry(resolver.Resolve<IModLogger>(), _failedGameFolder);
    }

    private void Install(IResolver resolver)
    {
        if (resolver.Resolve<IDedicatedServerProvider>().IsDedicatedServer) return;

        var logger = resolver.Resolve<IModLogger>();
        var moduleFolder = Path.GetFullPath(Path.Combine(resolver.Resolve<IPathService>().ModuleRootPath, "Shaders", "Sources"));
        var gameFolder = Path.GetFullPath(Path.Combine(BasePath.Name, "Shaders", "Sources"));
        var result = resolver.Resolve<ShaderSourceInstallService>().Install(moduleFolder, gameFolder);

        logger.LogInfo($"[ShaderSourceInstall] {moduleFolder} -> {gameFolder}: copied {result.Copied.Count}, up to date {result.UpToDate}, failed {result.Failed.Count}");
        foreach (var failure in result.Failed)
            logger.LogWarning($"[ShaderSourceInstall] {failure.Name}: {failure.Message}");

        if (result.Failed.Count > 0) _failedGameFolder = gameFolder;
    }
}
