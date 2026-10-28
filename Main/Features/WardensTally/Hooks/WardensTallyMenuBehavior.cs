using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.WardensTally.Hooks;

/// <summary>Warden's Tally (#903): on the menu-opened event, which fires after the menu's init ran, adds the tally under vanilla's dungeon sentence.</summary>
public sealed class WardensTallyMenuBehavior : CampaignBehaviorBase
{
    private const string IntroVariable = "PRISONER_INTRODUCTION";

    private readonly WardensTallyMenuService _service;
    private readonly IModLogger _logger;
    private bool _faultLogged;

    public WardensTallyMenuBehavior(WardensTallyMenuService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    internal WardensTallyMenuService Service => _service;

    public override void RegisterEvents() =>
        CampaignEvents.GameMenuOpened.AddNonSerializedListener(this, OnMenuOpened);

    public override void SyncData(IDataStore dataStore)
    {
    }

    // The title is set only when vanilla's init set the intro variable instead of opening a location.
    internal void OnMenuOpened(MenuCallbackArgs args) =>
        Guard(() => Apply(args?.MenuTitle != null, args?.MenuContext?.GameMenu?.StringId, Settlement.CurrentSettlement?.StringId));

    internal void Guard(Action body)
    {
        try
        {
            body();
        }
        catch (Exception ex)
        {
            if (_faultLogged)
                return;

            _faultLogged = true;
            _logger.LogError($"[WardensTally] The dungeon menu text failed, keeping vanilla's: {ex}");
        }
    }

    internal void Apply(bool vanillaSetIntro, string? menuId, string? settlementId)
    {
        if (!vanillaSetIntro)
            return;

        var text = _service.BuildIntro(menuId, settlementId);
        // A plain string: the engine re-renders this menu's text every frame (GameMenuVM.IsMenuTextChanged), and a string is the cheapest to re-render.
        if (text != null)
            MBTextManager.SetTextVariable(IntroVariable, text.ToString());
    }
}
