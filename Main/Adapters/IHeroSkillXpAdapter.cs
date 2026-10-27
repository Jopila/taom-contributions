namespace TAOM.Adapters;

/// <summary>Skill XP + skill reads for the enlistment reward spine. Skill ids are engine StringIds (note: smithing = "Crafting").</summary>
public interface IHeroSkillXpAdapter
{
    bool AddSkillXp(string heroId, string skillId, float xp);

    /// <summary>
    /// <see cref="AddSkillXp"/>, returning the experience that landed: the developer's total after the call less the total before.
    /// The engine credits the ask times the hero's XP multiplier and the skill's learning rate, so a notice shows this, never the
    /// ask. 0 when nothing was credited (an unresolvable hero or skill, or an ask that is not above 0).
    /// </summary>
    float AddSkillXpLanded(string heroId, string skillId, float xp);

    /// <summary>Current skill VALUE (not xp), or 0 when unresolvable.</summary>
    int GetSkillValue(string heroId, string skillId);
}
