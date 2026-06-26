using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TerraMap.Core;

namespace TerraMap.Tilemap;

public class Tile
{
    public TextureRegion Texture;
    public int Size;

    private float normalisedScale;

    private string tileType;

    public string Type { get {return tileType;}}

    public static readonly Tile EMPTY = new("empty", 0);

    public Tile(string type, int size = 16)
    {
        tileType = type;
        Size = size;
        normalisedScale = 1f/size;
    }

    public Tile(string type, TextureRegion texture, int size = 16)
    {
        tileType = type;
        Texture = texture;
        Size = size;
        normalisedScale = 1f/size;
    }

    protected virtual void DrawToScreen(SpriteBatch batch, TextureRegion texture, Vector2 position)
    {
        texture.Draw(
            batch,
            position,
            Color.White, 
            0f, 
            Vector2.Zero, 
            normalisedScale, 
            SpriteEffects.None, 
            0f
        );
    }

    public virtual void Draw(SpriteBatch batch, Vector2 position)
    {
        DrawToScreen(batch, Texture, position);
    }

    protected int hash(int n) 
    {
        // A modern bit-mixing avalanche step (MurmurHash3 style)
        uint h = (uint)n;
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return (int)h;
    }

    protected int get_tile_variation(int x, int y, int variation_count) 
    {
        // Combine coordinates using a bit-rotation/shift to preserve spatial entropy
        int combinedSeed = x ^ (y << 16) ^ (y >> 16);
        int seed = hash(combinedSeed);
        
        // Use bitwise masking if variation_count is a power of 2 (like 4) for extra speed,
        // or stick to absolute modulo for safety.
        return Math.Abs(seed) % variation_count;
    }

    public override string ToString()
    {
        return $"Tile({tileType}, {Size} px)";
    }

}