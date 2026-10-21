namespace TAOM.Features.LongMemory;

/// <summary>The "Long Memory" MCM group, clamped. Read live at each raid or forced taking, none per frame.</summary>
public interface ILongMemorySettingsProvider
{
    LongMemorySettings Get();
}
