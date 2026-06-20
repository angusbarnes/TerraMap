using System;
using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

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

        public float PixelsPerUnit = 16f; // 1 Tile = 16 Pixels

        public Matrix GetTransformationMatrix()
        {
            return Matrix.CreateTranslation(-_position.X, -_position.Y, 0)
                * Matrix.CreateScale(PixelsPerUnit)
                * Matrix.CreateScale(_zoom, _zoom, 1f)
                * Matrix.CreateTranslation(Width / 2f, Height / 2f, 0);
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

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private Texture2D texture;
    private SpriteFont debugFont;

    private Camera mainCamera = new();

    private bool TURBO_MODE = false;

    // Input tracking state
    private MouseState _previousMouseState;
    public MainGame()
    {
        _graphics = new GraphicsDeviceManager(this);

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }


    const int MapWidth = 10000;
    const int MapHeight = 10000;
    int[,] tilemap = new int[MapWidth, MapHeight];
    protected override void Initialize()
    {
        // TODO: Add your initialization logic here
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.PreferredBackBufferWidth = 1280;
        _graphics.SynchronizeWithVerticalRetrace = !TURBO_MODE;
        IsFixedTimeStep = !TURBO_MODE;
        _graphics.ApplyChanges();

        mainCamera.SetDimensions(1280, 720);

        for (int i = 0; i < tilemap.GetLength(0); i++)
        {
            for (int j = 0; j < tilemap.GetLength(1); j++)
            {
                tilemap[i, j] = Random.Shared.Next(0, 4);
            }
        }

        Debug.WriteLine("Generated 1000 x 1000 tilemap");

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        texture = Content.Load<Texture2D>("tileset");
        debugFont = Content.Load<SpriteFont>("Arial");

        // TODO: use this.Content to load your game content here
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        MouseState currentMouseState = Mouse.GetState();

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

        float translationScaleFactor = 10 * 12f * (float) gameTime.ElapsedGameTime.TotalSeconds;
        mainCamera.Translate(xTranslate * translationScaleFactor, yTranslate * translationScaleFactor);
        // TODO: Add your update logic here
        base.Update(gameTime);
    }

    Vector2[] tileOffsets = [
        new Vector2(0, 0),
        new Vector2(0, 16),
        new Vector2(16, 16),
        new Vector2(16, 0)
    ];

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
        
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                int tileID = tilemap[x, y]; 
                Vector2 position = new Vector2(x, y);
                float normScale = 1f / 16f;
                _spriteBatch.Draw(texture, position, new Rectangle(160 + (int) tileOffsets[tileID].X, (int) tileOffsets[tileID].Y, 16, 16), Color.White, 0f, Vector2.Zero, normScale, SpriteEffects.None, 0f);
                
                drawnTiles +=1;
            }
        }


        _spriteBatch.End();

        frameMetrics.Update(gameTime.ElapsedGameTime);

        _spriteBatch.Begin();
        MouseState currentMouseState = Mouse.GetState();
        GraphicsMetrics metrics = _graphics.GraphicsDevice.Metrics;
        _spriteBatch.DrawString(debugFont, $"Camera Position: {mainCamera.GetPosition()} | Screen Space Coords: X={currentMouseState.X}, y={currentMouseState.Y} | World Space Coords: X={(currentMouseState.X + mainCamera.GetPosition().X) / mainCamera.GetZoom()}, Y={(currentMouseState.Y + mainCamera.GetPosition().Y) / mainCamera.GetZoom()} | Zoom: {mainCamera.GetZoom():F3}", Vector2.One * 5, Color.White);
        _spriteBatch.DrawString(debugFont, $"Resolution: {mainCamera.Width}x{mainCamera.Height} | Draw Count: {metrics.DrawCount} | Textures Count: {metrics.TextureCount} | Drawn Tiles: {drawnTiles} (Expected: {(mainCamera.Width * 1/mainCamera.GetZoom()/16) * (mainCamera.Height * 1/mainCamera.GetZoom()/16)})", Vector2.One * 5 + new Vector2(0, 16), Color.White);
        _spriteBatch.DrawString(debugFont, $"FPS: {frameMetrics.AverageFps:F0} ({frameMetrics.AverageFrameTimeMs:F2} ms, {frameMetrics.WorstFrameTimeMs:F2} ms)", Vector2.One * 5 + new Vector2(0, 32), Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
