namespace TAOM.Features.HardQuestions.Domain;

/// <summary>A companion of the player and the traits that make him judge a hard deed.</summary>
public sealed class CompanionRef
{
    public CompanionRef(string id, int mercyLevel, int honorLevel)
    {
        Id = id;
        MercyLevel = mercyLevel;
        HonorLevel = honorLevel;
    }

    public string Id { get; }

    public int MercyLevel { get; }

    public int HonorLevel { get; }
}
