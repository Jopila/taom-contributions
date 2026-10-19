namespace TAOM.Features.VoiceAccents;

/// <summary>The validated voice map. Loaded once per process (<c>Reuse.Singleton</c>):
/// a JSON edit needs a full game restart.</summary>
public interface IVoiceAccentsConfigProvider
{
    VoiceAccentMap GetMap();
}
