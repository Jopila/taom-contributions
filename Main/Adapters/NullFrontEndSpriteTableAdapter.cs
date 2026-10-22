namespace TAOM.Adapters;

/// <summary>No client sprite table (a dedicated server, or a client where TAOM.Client did not load): a sprite is never registered.</summary>
public sealed class NullFrontEndSpriteTableAdapter : IFrontEndSpriteTableAdapter
{
    public void RegisterSprite(string spriteName, FrontEndTexture texture, NinePatch? ninePatch)
    {
    }
}
