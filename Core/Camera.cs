using System;
using Microsoft.Xna.Framework;

namespace TerraMap.Core;

 class Camera
{
    private float _zoom = 1.0f;

    private Vector2 _position = Vector2.Zero;

    public float MinZoom = 0.2f;
    public float MaxZoom = 5.0f;
    public float ZoomSensitivity = 0.001f; // Adjusts how fast you zoom

    public int Width = 1280;
    public int Height = 720;

    public Camera()
    {
        
    }

    public Camera(float minZoom, float maxZoom)
    {
        MinZoom = minZoom;
        MaxZoom = maxZoom;
    }

    
    public void SetDimensions(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public float PixelsPerUnit = 32f; // 1 Tile = 16 Pixels

    public Matrix GetTransformationMatrix()
    {
        return Matrix.CreateTranslation(-_position.X, -_position.Y, 0)
            * Matrix.CreateScale(PixelsPerUnit)
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

    public void SetPosition(Vector2 vec)
    {
        _position = vec;
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