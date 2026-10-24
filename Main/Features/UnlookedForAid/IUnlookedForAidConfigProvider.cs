namespace TAOM.Features.UnlookedForAid;

public interface IUnlookedForAidConfigProvider
{
    /// <summary>
    /// How strong a lord must be, against the whole siege camp, to stop fleeing it. A finite number from 0 up to but not
    /// including 1: a lord as strong as the camp would not flee it under vanilla's own rule anyway.
    /// </summary>
    float MinStrengthRatio { get; }
}
