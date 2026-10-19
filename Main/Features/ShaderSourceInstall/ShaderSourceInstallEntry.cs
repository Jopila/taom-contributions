using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ModuleManager;

namespace TAOM.Features.ShaderSourceInstall;

/// <summary>
/// Engine boundary for <see cref="ShaderSourceInstallService"/>: the engine compiles shaders only from
/// <c>&lt;game&gt;\Shaders\Sources</c>, so the module's copies are installed there at the very start of
/// <c>OnSubModuleLoad</c>, before the engine's compile pass begins. Fail-open: never throws.
/// </summary>
public static class ShaderSourceInstallEntry
{
    private static string? _failedFolder;
    private static bool _warned;

    public static void Run(IModLogger logger)
    {
        try
        {
            var gameFolder = BasePath.Name + "Shaders/Sources";
            var moduleFolder = ModuleHelper.GetModuleFullPath("TAOM") + "Shaders/Sources";
            var result = new ShaderSourceInstallService(new ShaderSourceFileSystem()).Install(moduleFolder, gameFolder);

            logger.LogInfo($"[ShaderSourceInstall] copied {result.Copied.Count}, up to date {result.UpToDate}, failed {result.Failed.Count}");
            foreach (var failure in result.Failed)
                logger.LogWarning($"[ShaderSourceInstall] {failure.Name}: {failure.Message}");

            if (result.Failed.Count > 0) _failedFolder = gameFolder;
        }
        catch (Exception e)
        {
            logger.LogWarning($"[ShaderSourceInstall] not run: {e.GetType().Name}: {e.Message}");
        }
    }

    public static void ShowPendingWarning()
    {
        try
        {
            if (_warned || _failedFolder == null) return;
            _warned = true;
            var text = new TextObject(
                "{=taom_shader_source_install_failed}TAOM could not copy its bow shaders into {FOLDER}. Some bows may make their holder invisible. Run the game once as administrator, or copy Modules\\TAOM\\Shaders\\Sources into that folder.");
            text.SetTextVariable("FOLDER", _failedFolder);
            InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Red));
        }
        catch (Exception)
        {
            // A warning must never be the thing that breaks a load.
        }
    }
}
