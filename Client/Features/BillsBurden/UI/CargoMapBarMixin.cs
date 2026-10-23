using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.BillsBurden.UI;

/// <summary>
/// Adds a cargo-fill row to the map bar's secondary row, right after the troop wage item. The value is
/// recomputed every refresh: capacity also moves with perks, prisoners, healthy count and career
/// passives, which no roster stamp would cover. TotalWeightCarried is engine-cached.
/// </summary>
[ViewModelMixin("Refresh")]
internal sealed class CargoMapBarMixin : BaseViewModelMixin<MapInfoVM>
{
    // Also the BrushLayer name in MapBar.Right.Icons: the icon widget looks the layer up by this id.
    internal const string ItemId = "taom_cargo";
    private const string AnchorItemId = "troop_wage";

    private readonly IModLogger _logger;
    // Keyed per exception type: both members run per refresh / per hover, so an unlimited log would spam.
    private readonly HashSet<string> _refreshFailuresLogged = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _tooltipFailuresLogged = new HashSet<string>(StringComparer.Ordinal);
    private readonly MapInfoItemVM _item;

    public CargoMapBarMixin(MapInfoVM viewModel) : base(viewModel)
    {
        _logger = IoC.Resolve<IModLogger>();
        _item = new MapInfoItemVM(ItemId, GetTooltipProperties);
    }

    public override void OnRefresh()
    {
        try
        {
            OnRefreshCore();
        }
        catch (Exception ex)
        {
            if (!_refreshFailuresLogged.Add(ex.GetType().FullName ?? "(unknown)")) return;
            _logger?.LogError($"[BillsBurden] map-bar mixin OnRefresh threw. Root cause: {ex}");
        }
    }

    private void OnRefreshCore()
    {
        var party = Campaign.Current?.MainParty;
        if (party == null) return;

        var fill = CargoFillCalculator.Compute(party.TotalWeightCarried, party.InventoryCapacity);

        InsertItemOnce();

        // The engine setters skip unchanged values. Text is formatted each refresh so it follows a language switch.
        _item.Value = FillText(fill);
        _item.IntValue = fill.Percent;
        _item.HasWarning = fill.HasWarning;
    }

    private void InsertItemOnce()
    {
        var items = ViewModel?.SecondaryInfoItems;
        if (items == null || items.Contains(_item)) return;

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].ItemId == AnchorItemId)
            {
                items.Insert(i + 1, _item);
                return;
            }
        }

        items.Add(_item);
    }

    private List<TooltipProperty> GetTooltipProperties()
    {
        try
        {
            return GetTooltipPropertiesCore();
        }
        catch (Exception ex)
        {
            if (_tooltipFailuresLogged.Add(ex.GetType().FullName ?? "(unknown)"))
                _logger?.LogError($"[BillsBurden] map-bar tooltip callback threw. Root cause: {ex}");

            // Empty list, never a rethrow: the engine's hover dispatch calls this.
            return new List<TooltipProperty>();
        }
    }

    private List<TooltipProperty> GetTooltipPropertiesCore()
    {
        var result = new List<TooltipProperty>();
        var party = Campaign.Current?.MainParty;
        if (party == null) return result;

        var weight = party.TotalWeightCarried;
        var capacity = party.InventoryCapacity;
        var fill = CargoFillCalculator.Compute(weight, capacity);

        // WEIGHT uses the inventory screen's own format, so both show the same figure.
        var fillValue = new TextObject("{=taom_cargo_tt_fill_value}{WEIGHT} / {CAPACITY} ({PERCENT})")
            .SetTextVariable("WEIGHT", weight.ToString("#0"))
            .SetTextVariable("CAPACITY", capacity)
            .SetTextVariable("PERCENT", FillText(fill))
            .ToString();
        result.Add(new TooltipProperty(new TextObject("{=taom_cargo_tt_fill}Inventory fill").ToString(), fillValue, 0,
            onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.Title));

        result.Add(Separator());
        result.AddRange(CampaignUIHelper.GetPartyInventoryWeightTooltip(party));
        result.Add(Separator());
        result.AddRange(CampaignUIHelper.GetPartyInventoryCapacityTooltip(party));
        return result;
    }

    private static TooltipProperty Separator() =>
        new TooltipProperty("", "", 0, onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.DefaultSeperator);

    private static string FillText(CargoFill fill) => fill.IsValid
        ? GameTexts.FindText("str_NUMBER_percent").SetTextVariable("NUMBER", fill.Percent).ToString()
        : "-";
}
