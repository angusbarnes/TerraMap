using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TerraMap.WorldGen;

namespace TerraMap;

public class MainGame : Game
{

    public struct WorldBounds
    {
        public int MinX;
        public int MaxX;
        public int MinY;
        public int MaxY;
    }

    class Camera
    {
        private float _zoom = 1.0f;

        private Vector2 _position = Vector2.Zero;

        public float MinZoom = 0.2f;
        public float MaxZoom = 5.0f;
        public float ZoomSensitivity = 0.001f; // Adjusts how fast you zoom

        public int Width = 1280;
        public int Height = 720;
        public void SetDimensions(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public float PixelsPerUnit = 32f; // 1 Tile = 16 Pixels

        public Matrix GetTransformationMatrix()
        {
            // Convert float tile position into raw pixel space
            float rawPixelX = _position.X * PixelsPerUnit;
            float rawPixelY = _position.Y * PixelsPerUnit;

            // Snap to the nearest integer pixel
            float snappedPixelX = MathF.Round(rawPixelX);
            float snappedPixelY = MathF.Round(rawPixelY);

            // Corrected Chain: Scale to pixels FIRST, then translate by pixels
            return Matrix.CreateScale(PixelsPerUnit)
                    * Matrix.CreateTranslation(-snappedPixelX, -snappedPixelY, 0)
                    * Matrix.CreateScale(_zoom, _zoom, 1f)
                    * Matrix.CreateTranslation(Width / 2f, Height / 2f, 0);
            }

        public Vector2 ScreenSpaceToWorldCoords(Vector2 vec)
        {
            Matrix inverseMatrix = Matrix.Invert(GetTransformationMatrix());

            return Vector2.Transform(vec, inverseMatrix);
        }

        public WorldBounds GetVisibleWorldBounds()
        {
            Matrix inverseMatrix = Matrix.Invert(GetTransformationMatrix());

            Vector2 topLeftWorld = Vector2.Transform(Vector2.Zero, inverseMatrix);
            Vector2 bottomRightWorld = Vector2.Transform(new Vector2(Width, Height), inverseMatrix);

            return new WorldBounds
            {
                MinX = (int)MathF.Floor(topLeftWorld.X) - 1,
                MaxX = (int)MathF.Ceiling(bottomRightWorld.X) + 1,
                MinY = (int)MathF.Floor(topLeftWorld.Y) - 1,
                MaxY = (int)MathF.Ceiling(bottomRightWorld.Y) + 1
            };
        }

        public void SetZoom(float zoom)
        {
            _zoom = zoom;
            ClampZoom();
        }

        public void IncreaseZoom(float zoom)
        {
            _zoom += zoom;
            ClampZoom();
        }

        private void ClampZoom()
        {
            _zoom = MathHelper.Clamp(_zoom, MinZoom, MaxZoom);
        }

        public float GetZoom()
        {
            return _zoom;
        }

        public Vector2 GetPosition()
        {
            return _position;
        }

        public void Translate(Vector2 vector)
        {
            _position += vector;
        }

        public void Translate(float x, float y)
        {
            _position += new Vector2(x, y);
        }

        public void TranslateX(float x)
        {
            Translate(x, 0);
        }

        public void TranslateY(float y)
        {
            Translate(0, y);
        }
    }

    private FrameMetrics frameMetrics = new FrameMetrics(60);
    private GcMetrics _gcMetrics = new GcMetrics();

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private Texture2D texture;
    private SpriteFont debugFont;

    private Camera mainCamera = new();

    private bool TURBO_MODE = false;
    private bool MEM_MONITOR = true;

    // Input tracking state
    private MouseState _previousMouseState;
    public MainGame()
    {
        _graphics = new GraphicsDeviceManager(this);

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }


    const int MapWidth = 256;
    const int MapHeight = 256;
    Tile[,] tilemap = new Tile[MapWidth, MapHeight];
    protected override void Initialize()
    {
        // TODO: Add your initialization logic here
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.SynchronizeWithVerticalRetrace = !TURBO_MODE;
        IsFixedTimeStep = !TURBO_MODE;
        _graphics.ApplyChanges();

        mainCamera.SetDimensions(1280, 720);

        Debug.WriteLine("Generated 1000 x 1000 tilemap");

        base.Initialize();
        
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        texture = Content.Load<Texture2D>("tileset");
        debugFont = Content.Load<SpriteFont>("Arial");

        PerlinNoise noise = new(69);

        VariadicTile grassTile  = new("grass", [
            new TextureRegion(texture, 160, 0, 16, 16),
            new TextureRegion(texture, 176, 0, 16, 16),
            new TextureRegion(texture, 160, 16, 16, 16),
            new TextureRegion(texture, 176, 0, 16, 16)
        ]);

        Tile deepWaterTile = new("deep_water", new TextureRegion(texture, 48, 128, 32, 32), 32);
        Tile waterTile     = new("water",      new TextureRegion(texture, 48, 96, 32, 32), 32);
        Tile sandTile      = new("sand",       new TextureRegion(texture, 80, 128, 32, 32), 32);
        Tile desertTile    = new("desert",     new TextureRegion(texture, 80, 96, 32, 32), 32); // Dry land
        Tile forestTile    = new("forest",     new TextureRegion(texture, 48, 192, 32, 32), 32); // Wet land
        Tile stoneTile     = new("stone",      new TextureRegion(texture, 64, 48, 16, 16), 32); // High altitude
        Tile snowTile      = new("snow",       new TextureRegion(texture, 80, 160, 32, 32), 32); // Highest peak


        for (int i = 0; i < tilemap.GetLength(0); i++)
        {
            for (int j = 0; j < tilemap.GetLength(1); j++)
            {
                // ELEVATION MAP (Determines macro structures: oceans, lowlands, mountains)
                // Scale of 120.0f provides nicely sized continental landmasses.
                float elevation = noise.SamplefBm(i, j, octaves: 5, scale: 300.0f, persistence: 0.5f, lacunarity: 2.0f);

                // MOISTURE MAP (Determines local environments. Offset prevents identical features)
                // Scale is larger (200.0f) so climate changes more gradually than raw terrain.
                float moisture = noise.SamplefBm(i + 15000, j + 15000, octaves: 4, scale: 500.0f, persistence: 0.45f, lacunarity: 2.0f);

                // Deep Water Abyss
                if (elevation < 0.35f)
                {
                    tilemap[i, j] = deepWaterTile;
                }
                // Shallow Coastal Waters
                else if (elevation < 0.46f)
                {
                    tilemap[i, j] = waterTile;
                }
                // Beaches / Shoreline
                else if (elevation < 0.50f)
                {
                    // Only generate beaches if it isn't a super marshy/wet area
                    tilemap[i, j] = (moisture > 0.6f) ? forestTile : sandTile;
                }
                // Habitable Lowlands / Plains / Forests / Deserts
                else if (elevation < 0.75f)
                {
                    if (moisture < 0.35f)
                    {
                        tilemap[i, j] = desertTile; // Arid sand plains
                    }
                    else if (moisture < 0.65f)
                    {
                        tilemap[i, j] = grassTile;  // Standard temperate plains
                    }
                    else
                    {
                        tilemap[i, j] = forestTile; // Lush, dense vegetation
                    }
                }
                // Mountain Base / Rocky Ridges
                else if (elevation < 0.85f)
                {
                    // High altitude moisture creates snowy paths instead of raw rock
                    tilemap[i, j] = (moisture > 0.60f) ? snowTile : stoneTile;
                }
                // Alpine Mountain Peaks
                else
                {
                    tilemap[i, j] = snowTile;
                }
            }
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        MouseState currentMouseState = Mouse.GetState();
        Vector2 movement = Vector2.Zero;

        // Calculate how much the wheel moved since the last frame
        int scrollDelta = currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;

        if (scrollDelta != 0)
        {
            // Add delta multiplied by sensitivity to the zoom level
            mainCamera.IncreaseZoom(scrollDelta * mainCamera.ZoomSensitivity);
        }

        // Save the state for the next frame update
        _previousMouseState = currentMouseState;

        float xTranslate = 0;
        float yTranslate = 0;
        if(Keyboard.GetState().IsKeyDown(Keys.S))
        {
            yTranslate += 1;
        }

        if(Keyboard.GetState().IsKeyDown(Keys.W))
        {
            yTranslate += -1;
        }

        if(Keyboard.GetState().IsKeyDown(Keys.A))
        {
            xTranslate += -1;
        }

        if(Keyboard.GetState().IsKeyDown(Keys.D))
        {
            xTranslate += 1;
        }

        float baseSpeed = 5f;
        float scaledSpeed = 10f;

        if (xTranslate != 0 || yTranslate != 0)
        {
            float translationScaleFactor = (baseSpeed + scaledSpeed / mainCamera.GetZoom()) * (float) gameTime.ElapsedGameTime.TotalSeconds;
            movement.X = xTranslate;
            movement.Y = yTranslate;
            movement.Normalize();
            mainCamera.Translate(movement * translationScaleFactor);
        }

        base.Update(gameTime);
        if (MEM_MONITOR) _gcMetrics.Update();
    }

    Vector2[] tileOffsets = [
        new Vector2(0, 0),
        new Vector2(0, 16),
        new Vector2(16, 16),
        new Vector2(16, 0)
    ];

    public record IntPair(int X, int Y)
    {
        public static implicit operator IntPair(int[] array)
        {
            if (array.Length < 2) throw new Exception("Cannot convert array to IntPair as it has less than 2 values");
            return new IntPair(array[0],  array[1]);
        }

    };



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

    protected override void Draw(GameTime gameTime)
    {
        // Get the dynamic visibility box for this frame
        WorldBounds bounds = mainCamera.GetVisibleWorldBounds();

        // Clamp bounds to map dimensions (assuming a 1000x1000 map array layout)
        int startX = Math.Max(0, bounds.MinX);
        int endX = Math.Min(MapWidth - 1, bounds.MaxX);
        int startY = Math.Max(0, bounds.MinY);
        int endY = Math.Min(MapHeight - 1, bounds.MaxY);
        int drawnTiles = 0;
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _spriteBatch.Begin(transformMatrix: mainCamera.GetTransformationMatrix(), samplerState: SamplerState.PointClamp);
        
        Vector2 pos = new();
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                pos.X = x;
                pos.Y = y;
                tilemap[x, y].Draw(_spriteBatch, pos);

                //Console.WriteLine($"Attempting to draw {tilemap[x, y]} at x={x}, y={y}");
                drawnTiles +=1;
            }
        }
        _spriteBatch.End();

        frameMetrics.Update(gameTime.ElapsedGameTime);

        _spriteBatch.Begin();
        MouseState currentMouseState = Mouse.GetState();
        GraphicsMetrics metrics = _graphics.GraphicsDevice.Metrics;

        Vector2 worldMousePosition = mainCamera.ScreenSpaceToWorldCoords(currentMouseState.Position.ToVector2());

        float mouseWorldPosX = worldMousePosition.X;
        float mouseWorldPosY = worldMousePosition.Y;

        Tile hoveredTile = Tile.EMPTY;

        if ((int) mouseWorldPosX >= 0 && (int) mouseWorldPosY >= 0 &&(int) mouseWorldPosX < tilemap.GetLength(0) && (int) mouseWorldPosY < tilemap.GetLength(1))
        {
            hoveredTile = tilemap[(int) mouseWorldPosX, (int) mouseWorldPosY];
        }

        _spriteBatch.DrawString(debugFont, $"Camera Position: X={mainCamera.GetPosition().X:F2}, Y={mainCamera.GetPosition().Y:F2} | Screen Space Coords: X={currentMouseState.X:F2}, y={currentMouseState.Y:F2} | World Space Coords: X={mouseWorldPosX :F2}, Y={mouseWorldPosY:F2} | Zoom: {mainCamera.GetZoom():F3} | Hovered Tile: {hoveredTile}", Vector2.One * 5, Color.White);
        _spriteBatch.DrawString(debugFont, $"Resolution: {mainCamera.Width}x{mainCamera.Height} | Draw Count: {metrics.DrawCount} | Textures Count: {metrics.TextureCount} | Drawn Tiles: {drawnTiles} (Expected: {(mainCamera.Width * 1/mainCamera.GetZoom()/16) * (mainCamera.Height * 1/mainCamera.GetZoom()/16)})", Vector2.One * 5 + new Vector2(0, 16), Color.White);
        _spriteBatch.DrawString(debugFont, $"FPS: {frameMetrics.AverageFps:F0} ({frameMetrics.AverageFrameTimeMs:F2} ms, {frameMetrics.WorstFrameTimeMs:F2} ms)", Vector2.One * 5 + new Vector2(0, 32), Color.White);

        if (MEM_MONITOR)
        {
            _spriteBatch.DrawString(debugFont, $"Heap Memory:  {_gcMetrics.TotalMemoryMb:F2} MB", Vector2.One * 5 + new Vector2(0, 48), Color.White);
            _spriteBatch.DrawString(debugFont, $"Alloc Rate:   {_gcMetrics.AllocRateMbPerSec:F2} MB/s", Vector2.One * 5 + new Vector2(0, 64), Color.White);
            _spriteBatch.DrawString(debugFont, $"GC Gens (0/1/2): [{_gcMetrics.Gen0Collections}/{_gcMetrics.Gen1Collections}/{_gcMetrics.Gen2Collections}]", Vector2.One * 5 + new Vector2(0, 80), Color.White);
            _spriteBatch.DrawString(debugFont, $"GC Time Cost:  {_gcMetrics.GcPausePercentage:F2}%", Vector2.One * 5 + new Vector2(0, 96), Color.White);
        }
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
