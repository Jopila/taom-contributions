using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;

namespace TAOM.Features.CrownGuard.Cheats;

/// <summary>
/// `taom.play_coronation [kingdom]` opens the become-king coronation scene for a kingdom on demand. The scene is the
/// only place Crown Guard (#825) shows, and vanilla plays it only after the player wins a king election, so checking
/// each kingdom culture's two guards would otherwise take a whole election per culture.
///
/// Tier A: display only. The kingdom's own ruler stands in the crowned spot, so nobody joins a kingdom or is crowned,
/// and the scene reads only that ruler's look and clan and <c>NewLeaderHero.Clan.Kingdom</c>. No argument means the
/// player's kingdom. An eliminated kingdom keeps its ruling clan while that clan has left it, and the scene would then
/// read a null kingdom outside Patch56's net, so it is refused. The report names the guard troop the scene draws, read
/// from the same patched <c>GetBodyguardOfCulture</c> call the scene makes, not from Crown Guard's map.
/// </summary>
public static class CrownGuardCheats
{
    private const string Usage =
        "Format is \"taom.play_coronation [KingdomName/KingdomId]\".\n"
        + "Opens the become-king coronation scene for a kingdom, its ruler in the crowned spot,\n"
        + "to check the two guards of its culture.\n"
        + "No argument means your own kingdom. Display only: nobody joins or is crowned.";

    [CommandLineFunctionality.CommandLineArgumentFunction("play_coronation", "taom")]
    public static string PlayCoronation(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            Kingdom? kingdom = null;
            if (args.Count == 0)
                kingdom = Hero.MainHero?.Clan?.Kingdom;
            else if (!CampaignCheats.TryGetObject(CampaignCheats.ConcatenateString(args), out kingdom, out var error))
                return error + "\n" + Usage;
            if (kingdom == null)
                return "Your clan is in no kingdom. Name one: \"taom.play_coronation <KingdomName>\".";

            var name = kingdom.Name?.ToString() ?? kingdom.StringId;
            var ruler = kingdom.Leader;
            if (ruler == null || ruler.Clan?.Kingdom != kingdom)
                return $"{name} has no ruler in it (an eliminated kingdom), so there is nobody to crown.";

            var guardTroopId = CampaignSceneNotificationHelper.GetBodyguardOfCulture(kingdom.Culture).Character?.StringId;
            MBInformationManager.ShowSceneNotification(new BecomeKingSceneNotificationItem(ruler));
            return FormatShown(name, kingdom.Culture.StringId, guardTroopId);
        });

    internal static string FormatShown(string kingdomName, string cultureId, string? guardTroopId) =>
        $"[CrownGuard] Showing the coronation scene for {kingdomName}: culture {cultureId}, "
        + $"guard troop {guardTroopId ?? "none (the scene leaves both guard spots empty)"}.";
}
