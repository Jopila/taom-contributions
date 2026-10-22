namespace TAOM.Adapters;

/// <summary>
/// Adds a runtime texture to the engine sprite table as a named Gauntlet sprite (ADR-013). The sprite type is
/// client-only, so the implementation lives in TAOM.Client; core NullFrontEndSpriteTableAdapter registers nothing.
/// </summary>
public interface IFrontEndSpriteTableAdapter
{
    void RegisterSprite(string spriteName, FrontEndTexture texture, NinePatch? ninePatch);
}
