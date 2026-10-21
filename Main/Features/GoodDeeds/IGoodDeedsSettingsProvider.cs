namespace TAOM.Features.GoodDeeds;

/// <summary>The "Good Deeds" MCM group, clamped. Read live at each release, donation and tournament, none per frame.</summary>
public interface IGoodDeedsSettingsProvider
{
    GoodDeedsSettings Get();
}
