using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace TerraMap;

public class MainGame : Game
{

    class Camera
    {
        private float _zoom = 1.0f;

        private Vector2 _position = Vector2.Zero;

        public float MinZoom = 0.1f;
        public float MaxZoom = 5.0f;
        public float ZoomSensitivity = 0.001f; // Adjusts how fast you zoom

        public int Width = 1280;
        public int Height = 720;
        public void SetDimensions(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public Matrix GetTransformationMatrix()
        {
            return Matrix.CreateScale(new Vector3(_zoom, _zoom, 1)) * Matrix.CreateTranslation(-_position.X, -_position.Y, 0);;
        }

        public Rectangle GetFrustum()
        {
            // TODO: reduce GC pressure and type casting needed
            return new Rectangle((int) _position.X, (int) _position.Y, Width, Height);
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

    private FrameMetrics frameMetrics = new FrameMetrics(120);

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private Texture2D texture;
    private SpriteFont debugFont;

    private Camera mainCamera = new();

    // Input tracking state
    private MouseState _previousMouseState;
    public MainGame()
    {
        _graphics = new GraphicsDeviceManager(this);

        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }


    int[,] tilemap = new int[1000, 1000];
    protected override void Initialize()
    {
        // TODO: Add your initialization logic here
        _graphics.PreferredBackBufferHeight = 720;
        _graphics.PreferredBackBufferWidth = 1280;
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
        int drawnTiles = 0;
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _spriteBatch.Begin(transformMatrix: mainCamera.GetTransformationMatrix(), samplerState: SamplerState.PointClamp);
        
        for (int i = (int)Math.Clamp(mainCamera.GetPosition().X / 16 * 1/mainCamera.GetZoom(), 0, 1000000); i < Math.Clamp(((int) (mainCamera.GetPosition().X + 1280) * 1/mainCamera.GetZoom())/ 16 + 1, 0, tilemap.GetLength(0)); i++)
        {
            for (int j = (int)Math.Clamp( mainCamera.GetPosition().Y / 16 * 1/mainCamera.GetZoom(), 0, 1000000); j < Math.Clamp(((int) (mainCamera.GetPosition().Y + 720) * 1/mainCamera.GetZoom())/ 16 + 1, 0, tilemap.GetLength(1)); j++)
            {
                int tileID = tilemap[i, j];

                _spriteBatch.Draw(texture, new Vector2(i * 16, j * 16), new Rectangle(160 + (int) tileOffsets[tileID].X, (int) tileOffsets[tileID].Y, 16, 16), Color.White);
                drawnTiles +=1;
            }
        }


        _spriteBatch.End();

        frameMetrics.Update(gameTime.ElapsedGameTime);

        _spriteBatch.Begin();
        MouseState currentMouseState = Mouse.GetState();
        GraphicsMetrics metrics = _graphics.GraphicsDevice.Metrics;
        _spriteBatch.DrawString(debugFont, $"Camera Position: {mainCamera.GetPosition()} | Screen Space Coords: X={currentMouseState.X}, y={currentMouseState.Y} | World Space Coords: X={(currentMouseState.X + mainCamera.GetPosition().X) / mainCamera.GetZoom()}, Y={(currentMouseState.Y + mainCamera.GetPosition().Y) / mainCamera.GetZoom()}", Vector2.One * 5, Color.White);
        _spriteBatch.DrawString(debugFont, $"Resolution: {mainCamera.Width}x{mainCamera.Height} | Draw Count: {metrics.DrawCount} | Textures Count: {metrics.TextureCount} | Drawn Tiles: {drawnTiles} (Expected: {(mainCamera.Width * 1/mainCamera.GetZoom()/16) * (mainCamera.Height * 1/mainCamera.GetZoom()/16)})", Vector2.One * 5 + new Vector2(0, 16), Color.White);
        _spriteBatch.DrawString(debugFont, $"FPS: {frameMetrics.AverageFps:F0} ({frameMetrics.AverageFrameTimeMs:F2} ms, {frameMetrics.WorstFrameTimeMs:F2} ms)", Vector2.One * 5 + new Vector2(0, 32), Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
