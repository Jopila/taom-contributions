using System;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.ShaderSourceInstall;

/// <summary>
/// Engine boundary for the install's player-facing warning. An inquiry, not a chat line: the initial
/// screen clears the chat log after the splash video (docs/reviews/lessons/localization-ui.md). The
/// button reuses vanilla's own "Ok" row, already translated. Never throws.
/// </summary>
public static class ShaderSourceInstallEntry
{
    public static void ShowFailureInquiry(IModLogger logger, string gameFolder)
    {
        try
        {
            var text = new TextObject(
                "{=taom_shader_source_install_failed}TAOM could not install its bow shaders into {FOLDER}. Some bows may make their holder invisible. Run the game once as administrator; if that does not help, reinstall TAOM. The TAOM log names the cause.");
            text.SetTextVariable("FOLDER", gameFolder);
            InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=taom_patch_apply_notice_title}TAOM").ToString(), text.ToString(), true, false,
                new TextObject("{=oHaWR73d}Ok").ToString(), string.Empty, null, null));
        }
        catch (Exception e)
        {
            logger.LogWarning($"[ShaderSourceInstall] warning not shown: {e.GetType().Name}: {e.Message}");
        }
    }
}
