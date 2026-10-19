namespace TAOM.Adapters;

/// <summary>
/// The Quest log's journal items, as the quest-log fast open reaches them (docs/features/shadowfax-screens.md). Items are
/// handed over as opaque objects (a <c>QuestItemVM</c> built from a finished quest's journal entry), so the defer service
/// holds no TaleWorlds type.
/// </summary>
public interface IQuestJournalAdapter
{
    /// <summary>True once <see cref="Bind"/> found every member and every guard patch is attached.</summary>
    bool IsBound { get; }

    /// <summary>
    /// Resolves the private <c>QuestItemVM.PopulateQuestLog</c> and checks that the four patches that keep a deferred item
    /// correct are attached. Returns null when bound, else why not; never throws.
    /// </summary>
    string? Bind();

    /// <summary>
    /// Builds a deferred item's stages the way its constructor would have: <c>PopulateQuestLog(entry, false)</c> for each
    /// journal entry through reflection (so other patches on it still run), then <c>UpdateIsUpdated</c> and
    /// <c>RefreshValues</c>. Throws if the item or the binding is wrong.
    /// </summary>
    void Rebuild(object item);

    /// <summary>The item's first or last journal entry (a <c>JournalLog</c>), or null when it has none.</summary>
    object? FirstOrLastEntry(object item, bool last);

    /// <summary>True when any of the item's journal entries is still in the campaign's unexamined quest logs.</summary>
    bool AnyUnexamined(object item);
}
