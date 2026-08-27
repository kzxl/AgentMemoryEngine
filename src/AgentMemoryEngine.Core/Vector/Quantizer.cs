using System.Numerics;

namespace AgentMemoryEngine.Core.Vector;

/// <summary>
/// Scalar Quantization (SQ8) engine for compressing FP32 embeddings into Int8 with minimal cosine error.
/// </summary>
public static class Quantizer
{
    /// <summary>
    /// Normalizes a float vector in-place to unit Euclidean length (L2 norm = 1.0).
    /// </summary>
    public static void Normalize(Span<float> vector)
    {
        float sumSquares = 0.0f;
        for (int i = 0; i < vector.Length; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        if (sumSquares <= 0.0f) return;

        float invNorm = 1.0f / MathF.Sqrt(sumSquares);
        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] *= invNorm;
        }
    }

    /// <summary>
    /// Quantizes an FP32 vector into Int8 (sbyte) values using per-vector MinMax scaling.
    /// </summary>
    public static void QuantizeSQ8(
        ReadOnlySpan<float> source,
        Span<sbyte> destination,
        out float scale,
        out float offset)
    {
        if (source.Length != destination.Length)
            throw new ArgumentException("Source and destination lengths must match.");

        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        for (int i = 0; i < source.Length; i++)
        {
            float v = source[i];
            if (v < minVal) minVal = v;
            if (v > maxVal) maxVal = v;
        }

        float range = maxVal - minVal;
        scale = range > 1e-7f ? range / 254.0f : 1.0f;
        offset = minVal;

        float invScale = 1.0f / scale;
        for (int i = 0; i < source.Length; i++)
        {
            int q = (int)MathF.Round((source[i] - offset) * invScale) - 127;
            destination[i] = (sbyte)Math.Clamp(q, -127, 127);
        }
    }

    /// <summary>
    /// Dequantizes an Int8 (sbyte) vector back into FP32 floats.
    /// </summary>
    public static void DequantizeSQ8(
        ReadOnlySpan<sbyte> source,
        Span<float> destination,
        float scale,
        float offset)
    {
        if (source.Length != destination.Length)
            throw new ArgumentException("Source and destination lengths must match.");

        for (int i = 0; i < source.Length; i++)
        {
            destination[i] = (source[i] + 127) * scale + offset;
        }
    }
}
