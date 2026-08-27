using System.Numerics;
using System.Runtime.CompilerServices;

namespace AgentMemoryEngine.Core.Vector;

/// <summary>
/// 1-Bit Binary Quantization (BQ) Engine with Hardware PopCount Acceleration.
/// Compresses 384-dimensional float vectors into 48 bytes (6 ulongs) for 10x faster stage-1 filtering.
/// </summary>
public static class BinaryQuantizer
{
    /// <summary>
    /// Quantizes an FP32 vector into a 1-bit packed binary representation (1 bit per dimension).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static void Quantize1Bit(ReadOnlySpan<float> source, Span<ulong> destination)
    {
        int requiredUlongs = (source.Length + 63) / 64;
        if (destination.Length < requiredUlongs)
            throw new ArgumentException($"Destination must have at least {requiredUlongs} ulongs.");

        destination.Clear();

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] >= 0.0f)
            {
                int ulongIdx = i / 64;
                int bitIdx = i % 64;
                destination[ulongIdx] |= (1UL << bitIdx);
            }
        }
    }

    /// <summary>
    /// Computes the exact Hamming Distance between two 1-bit quantized vector bitfields using hardware PopCount.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static int ComputeHammingDistance(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Bitfields must have matching lengths.");

        int distance = 0;
        for (int i = 0; i < a.Length; i++)
        {
            ulong xor = a[i] ^ b[i];
            distance += BitOperations.PopCount(xor);
        }

        return distance;
    }

    /// <summary>
    /// Computes normalized Hamming Similarity in the range [0.0, 1.0].
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static float ComputeHammingSimilarity(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b, int totalDimensions)
    {
        int distance = ComputeHammingDistance(a, b);
        float similarity = 1.0f - ((float)distance / totalDimensions);
        return Math.Clamp(similarity, 0.0f, 1.0f);
    }
}
