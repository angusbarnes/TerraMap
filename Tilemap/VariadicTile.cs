using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraMap.Core;

namespace TerraMap.Tilemap;

public class VariadicTile : Tile
{

    private TextureRegion[] textureVariants;
    public VariadicTile(string type, TextureRegion[] variants) : base(type)
    {
        textureVariants = variants;
    }

    public override void Draw(SpriteBatch batch, Vector2 position)
    {
        int x = (int) position.X;
        int y = (int) position.Y;
        int variant = get_tile_variation(x, y, textureVariants.Length);
        DrawToScreen(
            batch,
            textureVariants[variant],
            position
        );
    }
}