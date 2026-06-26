using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TerraMap.Core;
using TerraMap.Profiling;
using TerraMap.Tilemap;
using TerraMap.WorldGen;

namespace TerraMap;

public class MainGame : Game
{
    private FrameMetrics frameMetrics = new FrameMetrics(120);
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

        TargetElapsedTime = TimeSpan.FromSeconds(1d / 180d);
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

        VariadicTile grassTile = new("grass", [
            new TextureRegion(texture, 160, 0, 16, 16),
            new TextureRegion(texture, 176, 0, 16, 16),
            new TextureRegion(texture, 160, 16, 16, 16),
            new TextureRegion(texture, 176, 0, 16, 16)
        ]);

        Tile deepWaterTile = new("deep_water", new TextureRegion(texture, 48, 128, 32, 32), 32);
        Tile waterTile = new("water", new TextureRegion(texture, 48, 96, 32, 32), 32);
        Tile sandTile = new("sand", new TextureRegion(texture, 80, 128, 32, 32), 32);
        Tile desertTile = new("desert", new TextureRegion(texture, 80, 96, 32, 32), 32); // Dry land
        Tile forestTile = new("forest", new TextureRegion(texture, 48, 192, 32, 32), 32); // Wet land
        Tile stoneTile = new("stone", new TextureRegion(texture, 64, 48, 16, 16), 32); // High altitude
        Tile snowTile = new("snow", new TextureRegion(texture, 80, 160, 32, 32), 32); // Highest peak


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
        if (Keyboard.GetState().IsKeyDown(Keys.S) || Keyboard.GetState().IsKeyDown(Keys.Down))
        {
            yTranslate += 1;
        }

        if (Keyboard.GetState().IsKeyDown(Keys.W) || Keyboard.GetState().IsKeyDown(Keys.Up))
        {
            yTranslate += -1;
        }

        if (Keyboard.GetState().IsKeyDown(Keys.A) || Keyboard.GetState().IsKeyDown(Keys.Left))
        {
            xTranslate += -1;
        }

        if (Keyboard.GetState().IsKeyDown(Keys.D) || Keyboard.GetState().IsKeyDown(Keys.Right))
        {
            xTranslate += 1;
        }

        float baseSpeed = 5f;
        float scaledSpeed = 10f;

        if (xTranslate != 0 || yTranslate != 0)
        {
            float translationScaleFactor = (baseSpeed + scaledSpeed / mainCamera.GetZoom()) * (float)gameTime.ElapsedGameTime.TotalSeconds;
            movement.X = xTranslate;
            movement.Y = yTranslate;
            movement.Normalize();
            mainCamera.Translate(movement * translationScaleFactor);
        }

        base.Update(gameTime);
        if (MEM_MONITOR) _gcMetrics.Update();
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
        GraphicsDevice.Clear(Color.Red);
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
                drawnTiles += 1;
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

        if ((int)mouseWorldPosX >= 0 && (int)mouseWorldPosY >= 0 && (int)mouseWorldPosX < tilemap.GetLength(0) && (int)mouseWorldPosY < tilemap.GetLength(1))
        {
            hoveredTile = tilemap[(int)mouseWorldPosX, (int)mouseWorldPosY];
        }

        _spriteBatch.DrawString(debugFont, $"Camera Position: X={mainCamera.GetPosition().X:F2}, Y={mainCamera.GetPosition().Y:F2} | Screen Space Coords: X={currentMouseState.X:F2}, y={currentMouseState.Y:F2} | World Space Coords: X={mouseWorldPosX:F2}, Y={mouseWorldPosY:F2} | Zoom: {mainCamera.GetZoom():F3} | Hovered Tile: {hoveredTile}", Vector2.One * 5, Color.White);
        _spriteBatch.DrawString(debugFont, $"Resolution: {mainCamera.Width}x{mainCamera.Height} | Draw Count: {metrics.DrawCount} | Textures Count: {metrics.TextureCount} | Drawn Tiles: {drawnTiles} (Expected: {(mainCamera.Width * 1 / mainCamera.GetZoom() / mainCamera.PixelsPerUnit) * (mainCamera.Height * 1 / mainCamera.GetZoom() / mainCamera.PixelsPerUnit)})", Vector2.One * 5 + new Vector2(0, 16), Color.White);
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
