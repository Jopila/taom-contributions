namespace TAOM.Features.HallOfFire;

/// <summary>The validated Hall of Fire map. Loaded once per process (<c>Reuse.Singleton</c>):
/// a JSON edit needs a full game restart.</summary>
public interface IHallOfFireConfigProvider
{
    HallOfFireMap GetMap();
}
