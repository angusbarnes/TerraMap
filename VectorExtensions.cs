using Microsoft.Xna.Framework;

namespace TerraMap;    
public static class VectorExtensions
{
    public static Vector2 SafelyNormalized(this Vector2 value)
    {
        float denominator = System.MathF.Sqrt(value.X * value.X + value.Y * value.Y);

        //TODO: Review if this is a suitable discriminant
        if (denominator <= 0.000001)
        {
            value.X = 0;
            value.Y = 0;
        } else
        {
            float num = 1f / denominator;
            value.X *= num;
            value.Y *= num;
        }
        return value;
    }
}