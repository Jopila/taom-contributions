namespace TAOM.Features.TelcharsMark;

/// <summary>Telchar's Mark MCM settings. Read once per inventory open, never on a hot path.</summary>
public interface ITelcharsMarkSettingsProvider
{
    /// <summary>Master switch: tints and the "show only better items" box.</summary>
    bool IsEnabled { get; }

    /// <summary>Whether the "show only better items" box starts ticked when the inventory opens.</summary>
    bool ShowOnlyUpgradesByDefault { get; }
}
