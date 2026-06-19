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

        public Matrix GetTransformationMatrix()
        {
            return Matrix.CreateScale(new Vector3(_zoom, _zoom, 1)) * Matrix.CreateTranslation(_position.X, _position.Y, 0);;
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

    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;

    private Texture2D texture;

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


        if(Keyboard.GetState().IsKeyDown(Keys.S))
        {
            mainCamera.TranslateY(10 * -16f * (float) gameTime.ElapsedGameTime.TotalSeconds);
        }

        if(Keyboard.GetState().IsKeyDown(Keys.W))
        {
            mainCamera.TranslateY(10 * 16f * (float) gameTime.ElapsedGameTime.TotalSeconds);
        }

        if(Keyboard.GetState().IsKeyDown(Keys.A))
        {
            mainCamera.TranslateX(10 * 16f * (float) gameTime.ElapsedGameTime.TotalSeconds);
        }

        if(Keyboard.GetState().IsKeyDown(Keys.D))
        {
            mainCamera.TranslateX(10 * -16f * (float) gameTime.ElapsedGameTime.TotalSeconds);
        }

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
        GraphicsDevice.Clear(Color.CornflowerBlue);
        _spriteBatch.Begin(transformMatrix: mainCamera.GetTransformationMatrix(), samplerState: SamplerState.PointClamp);
        

        for (int i = 0; i < 200; i++)
        {
            for(int j = 0; j < 200; j++)
            {
                
            }
        }



        for (int i = (int) mainCamera.GetPosition().X / 16; i < Math.Clamp(((int) mainCamera.GetPosition().X + 1280 )/ 16, 0, tilemap.GetLength(0)); i++)
        {
            for (int j = (int) mainCamera.GetPosition().Y / 16; j < Math.Clamp(((int) mainCamera.GetPosition().Y + 720 )/ 16, 0, tilemap.GetLength(1)); j++)
            {
                int tileID = tilemap[i, j];

                _spriteBatch.Draw(texture, new Vector2(i * 16, j * 16), new Rectangle(160 + (int) tileOffsets[tileID].X, (int) tileOffsets[tileID].Y, 16, 16), Color.White);
            }
        }


        _spriteBatch.End();

        base.Draw(gameTime);
    }
}
