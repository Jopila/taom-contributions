namespace TAOM.Adapters;

/// <summary>The Bonds of Battle lines shown in the message log, localized through TAOM's {=KEY} strings.</summary>
public interface IBondsNoticeAdapter
{
    void ShowFoughtTogether(string lordName);

    void ShowFoughtAgainst(string lordName);

    void ShowBanditGratitude(string notableName, string settlementName);

    void ShowHandover(string lordName);
}
