namespace TAOM.Features.LordBanners;

/// <summary>The validated banner map. Loaded once per process (<c>Reuse.Singleton</c>):
/// a JSON edit needs a full game restart.</summary>
public interface ILordBannerCulturesConfigProvider
{
    LordBannerMap GetMap();
}
