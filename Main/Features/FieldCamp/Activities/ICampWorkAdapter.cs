namespace TAOM.Features.FieldCamp.Activities;

/// <summary>
/// Boundary over the campaign for camp work (#860, ADR-007): the party facts a start or a payout reads, the ground around the
/// camp, and the writes a finished work makes (goods, tools, morale, wounds, a message). It reports and writes; it decides
/// nothing: who may work, what the ground yields and what an injury costs are <see cref="CampActivityService"/> and
/// <see cref="CampActivityRules"/>. Plain numbers go out and no engine object is kept. Main thread only.
/// </summary>
public interface ICampWorkAdapter
{
    /// <summary>One pass over the main party and the ground under it; a default value (nothing ready, no men) with no campaign.</summary>
    CampWorkFacts Read();

    /// <summary>The player's camp stands and its raise is done. Cheap: rest healing asks it on every healing roll.</summary>
    bool CampReady();

    /// <summary>Whether water lies within a few map units of the camp, sampled at eight compass points and two distances.</summary>
    bool WaterNearby();

    /// <summary>Adds the goods of this work (meat, hardwood or fish) to the party's stores; returns how many went in (0 if the item is missing).</summary>
    int AddGoods(CampActivity activity, int amount);

    /// <summary>Removes tools from the party's stores (at most what it holds).</summary>
    void TakeTools(int count);

    /// <summary>Takes morale from the party: <paramref name="cost"/> is a positive amount to subtract (the work costs morale).</summary>
    void LowerMorale(float cost);

    /// <summary>Wounds up to <paramref name="count"/> healthy regulars of the main party (never a hero); returns how many were wounded.</summary>
    int WoundRegulars(int count);

    /// <summary>A random number from 0 up to 1, from the engine's generator.</summary>
    float NextRandom();

    /// <summary>A line in the message log.</summary>
    void ShowMessage(string text);
}
