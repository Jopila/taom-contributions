namespace TAOM.Features.HardQuestions.Domain;

/// <summary>One worn or carried item of a lord, with the set and slot it sits in.</summary>
public sealed class LordEquipmentPiece
{
    public LordEquipmentPiece(LordEquipmentSet set, int slot, string itemId, string? modifierId)
    {
        Set = set;
        Slot = slot;
        ItemId = itemId;
        ModifierId = modifierId;
    }

    public LordEquipmentSet Set { get; }

    public int Slot { get; }

    public string ItemId { get; }

    public string? ModifierId { get; }
}
