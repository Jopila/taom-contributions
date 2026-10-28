using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarChronicle.Effects;

namespace TAOM.Features.WardensTally;

/// <summary>Warden's Tally's seam to the dungeon menu (docs/features/wardens-tally.md): the intro text for a dungeon menu, or null to leave vanilla's.</summary>
public sealed class WardensTallyMenuService
{
    internal const string TownDungeonMenu = "town_keep_dungeon";
    internal const string CastleDungeonMenu = "castle_dungeon";

    private readonly IPrisonerEscapeAdapter _adapter;
    private readonly WardensTallyService _tally;
    private readonly IWarEffectService _effects;
    private readonly ICoopSessionProvider _coop;
    private readonly WardensTallySettingsProvider _settings;

    public WardensTallyMenuService(
        IPrisonerEscapeAdapter adapter, WardensTallyService tally, IWarEffectService effects,
        ICoopSessionProvider coop, WardensTallySettingsProvider settings)
    {
        _adapter = adapter;
        _tally = tally;
        _effects = effects;
        _coop = coop;
        _settings = settings;
    }

    /// <summary>The menu's new intro text, or null when vanilla's should stand.</summary>
    public TextObject? BuildIntro(string? menuId, string? settlementId)
    {
        if (menuId != TownDungeonMenu && menuId != CastleDungeonMenu)
            return null;
        if (!_settings.Enabled || string.IsNullOrEmpty(settlementId))
            return null;

        var reading = _adapter.ReadDungeon(settlementId!);
        var hostRolls = HostRolls();
        if (!hostRolls && reading != null)
        {
            foreach (var lord in reading.Lords)
            {
                if (lord?.Snapshot != null)
                    lord.Snapshot.PlayerHeld = false;
            }
        }

        var view = _tally.Build(reading, hostRolls ? kingdom => _effects.GetMultiplier(kingdom, WarEffectKind.PrisonerEscape) : null);
        return view == null ? null : WardensTallyTexts.Intro(view);
    }

    // A co-op client shows neither the boost (its effect registry is the join save's) nor the player-held halving (the server rolls without it).
    private bool HostRolls() => _coop.IsAuthority && CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient);
}
