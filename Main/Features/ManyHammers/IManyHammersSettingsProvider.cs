namespace TAOM.Features.ManyHammers;

/// <summary>The two Many Hammers switches on the "Smithing" MCM group, read live at each smithy click or list rebuild.</summary>
public interface IManyHammersSettingsProvider
{
    /// <summary>Shift smelts or refines five at once and Ctrl a whole stack.</summary>
    bool BulkEnabled { get; }

    /// <summary>Locked inventory items are left out of the smelting list.</summary>
    bool HideLockedEnabled { get; }
}
