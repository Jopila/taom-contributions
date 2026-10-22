using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.TwoDimension;
using TAOM.Adapters;
using NativeTexture = TaleWorlds.Engine.Texture;
using TwoDimensionTexture = TaleWorlds.TwoDimension.Texture;

namespace TAOM.Client.Adapters;

/// <summary>
/// Adds a module PNG's engine texture to <see cref="UIResourceManager"/>'s sprite table as a Gauntlet sprite
/// (ADR-013). Kysaro's TAOM_FactionUI technique (decompiled, #704): the engine texture is wrapped as a
/// Gauntlet texture under a sprite name the prefabs and brushes reference. The sprite type is client-only, so
/// core reaches this through <see cref="IFrontEndSpriteTableAdapter"/>.
/// </summary>
internal sealed class FrontEndSpriteTableAdapter : IFrontEndSpriteTableAdapter
{
    public void RegisterSprite(string spriteName, FrontEndTexture texture, NinePatch? ninePatch)
    {
        var sprites = UIResourceManager.SpriteData?.Sprites;
        if (sprites == null || texture.Native is not NativeTexture engineTexture)
            return;

        var gauntletTexture = new TwoDimensionTexture(new EngineTexture(engineTexture));
        var nine = ninePatch is { } n
            ? new SpriteNinePatchParameters(n.Left, n.Right, n.Top, n.Bottom)
            : SpriteNinePatchParameters.Empty;
        sprites[spriteName] = new FrontEndRuntimeSprite(spriteName, gauntletTexture, nine);
    }
}
