using System;

namespace TerraMap.WorldGen;


using System;

public class PerlinNoise
{
    public int Seed { get; private set; }
    private readonly int[] permutations = new int[512];

    private static readonly float[][] CORNER_GRADIENTS = [
        [1, 1], [-1, 1], [1, -1], [-1, -1]
    ];

    public PerlinNoise(int seed)
    {
        Seed = seed;
        Random rand = new Random(seed);

        for (int i = 0; i < 256; i++) { permutations[i] = i; }

        for (int i = 255; i > 0; i--)
        {
            int j = rand.Next(i + 1);
            int temp = permutations[i];
            permutations[i] = permutations[j];
            permutations[j] = temp;
        }

        for (int i = 0; i < 256; i++) { permutations[i + 256] = permutations[i]; }
    }

    /// <summary>
    /// Samples a single octave of 2D Perlin Noise at the given coordinates.
    /// Returns a value theoretically between -1.0 and 1.0, though typically closer to [-0.707, 0.707].
    /// </summary>
    public float Sample(float x, float y)
    {
        int floorX = (int)Math.Floor(x);
        int floorY = (int)Math.Floor(y);

        float Xf = x - floorX;
        float Yf = y - floorY;

        int Xi = floorX & 255;
        int Yi = floorY & 255;

        float u = Fade(Xf);
        float v = Fade(Yf);

        int hashTopLeft     = permutations[permutations[Xi] + Yi] & 3;
        int hashTopRight    = permutations[permutations[Xi + 1] + Yi] & 3;
        int hashBottomLeft  = permutations[permutations[Xi] + Yi + 1] & 3;
        int hashBottomRight = permutations[permutations[Xi + 1] + Yi + 1] & 3;

        float cTopLeft     = Grad(hashTopLeft, Xf, Yf);
        float cTopRight    = Grad(hashTopRight, Xf - 1, Yf);
        float cBottomLeft  = Grad(hashBottomLeft, Xf, Yf - 1);
        float cBottomRight = Grad(hashBottomRight, Xf - 1, Yf - 1);

        float top = float.Lerp(cTopLeft, cTopRight, u);
        float bottom = float.Lerp(cBottomLeft, cBottomRight, u);

        return float.Lerp(top, bottom, v);
    }

    /// <summary>
    /// Generates Fractal Brownian Motion (fBm) by stacking multiple octaves of Perlin Noise.
    /// This introduces organic, structural details ideal for landscapes and textures.
    /// </summary>
    /// <param name="x">The input X coordinate (e.g., world position).</param>
    /// <param name="y">The input Y coordinate (e.g., world position).</param>
    /// <param name="octaves">The number of noise layers to stack. Higher values add finer structural detail but cost more CPU cycles.</param>
    /// <param name="scale">The base zoom level of the noise. Larger numbers zoom out (more features packed together); smaller numbers zoom in.</param>
    /// <param name="persistence">Controls how quickly amplitude drops off with each octave. A value of 0.5 means each octave has half the height/influence of the previous one.</param>
    /// <param name="lacunarity">Controls how quickly frequency increases with each octave. A value of 2.0 means the frequency doubles every layer, introducing finer detail gaps.</param>
    /// <returns>A combined noise value normalized into a clean [0.0, 1.0] range.</returns>
    public float SamplefBm(float x, float y, float scale, int octaves = 4,  float persistence = 0.5f, float lacunarity = 2.0f)
    {
        // Guard against division by zero or negative scales
        if (scale <= 0.0f) scale = 0.0001f;

        float totalNoise = 0.0f;
        float maxPossibleAmplitude = 0.0f;
        
        float amplitude = 1.0f;
        float frequency = 1.0f;

        // Apply base scaling to the starting coordinates
        float sampleX = x / scale;
        float sampleY = y / scale;

        for (int i = 0; i < octaves; i++)
        {
            // Fetch noise and scale it by current amplitude loop
            float noiseVal = Sample(sampleX * frequency, sampleY * frequency);
            totalNoise += noiseVal * amplitude;

            // Track the maximum theoretical bounds so we can map the output perfectly later
            maxPossibleAmplitude += amplitude;

            // Scale up frequency (adding finer details) and drop down amplitude (diminishing its dominance)
            frequency *= lacunarity;
            amplitude *= persistence;
        }

        // Normalize the noise from its raw Perlin bounds back into a predictable [-1.0, 1.0] range
        float normalizedNoise = totalNoise / maxPossibleAmplitude;

        // Shift and scale from [-1.0, 1.0] to a production-safe [0.0, 1.0] range
        return (normalizedNoise + 1.0f) * 0.5f;
    }

    private static float Grad(int hash, float distX, float distY)
    {
        float[] gradient = CORNER_GRADIENTS[hash];
        return (distX * gradient[0]) + (distY * gradient[1]);
    }

    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }
}