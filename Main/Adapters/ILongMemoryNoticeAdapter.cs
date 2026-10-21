namespace TAOM.Adapters;

/// <summary>The Long Memory lines shown in the message log, localized through TAOM {=KEY} strings.</summary>
public interface ILongMemoryNoticeAdapter
{
    /// <summary>One line: the people of the village will remember the raid. Gender-neutral by design.</summary>
    void ShowRaid(string villageName);

    /// <summary>One line: the people of the village will remember what was taken from them by force. Gender-neutral by design.</summary>
    void ShowCoercion(string villageName);
}
