namespace TAOM.Features.LordCourtesies.Domain;

/// <summary>One roster element: the character, how many stand in it and whether he is a hero.</summary>
public sealed class TroopCount
{
    public TroopCount(string characterId, int count, bool isHero = false)
    {
        CharacterId = characterId;
        Count = count;
        IsHero = isHero;
    }

    public string CharacterId { get; }

    public int Count { get; }

    public bool IsHero { get; }
}
