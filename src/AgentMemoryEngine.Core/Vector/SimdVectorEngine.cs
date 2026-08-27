using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace AgentMemoryEngine.Core.Vector;

/// <summary>
/// Hardware-accelerated SIMD vector computation engine.
/// Utilizes AVX2/FMA/Vector256/Vector128 when available with scalar fallback.
/// </summary>
public static unsafe class SimdVectorEngine
{
    /// <summary>
    /// Computes the dot product of two FP32 vectors using SIMD acceleration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have identical dimensions.");

        int length = a.Length;
        int i = 0;
        float sum = 0.0f;

        if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
        {
            var acc256 = Vector256<float>.Zero;
            int simdEnd = length - (length % Vector256<float>.Count);

            for (; i < simdEnd; i += Vector256<float>.Count)
            {
                var va = Vector256.LoadUnsafe(ref Unsafe.AsRef(in a[i]));
                var vb = Vector256.LoadUnsafe(ref Unsafe.AsRef(in b[i]));
                
                if (Fma.IsSupported)
                {
                    acc256 = Fma.MultiplyAdd(va, vb, acc256);
                }
                else
                {
                    acc256 = Vector256.Add(acc256, Vector256.Multiply(va, vb));
                }
            }

            sum = Vector256.Sum(acc256);
        }
        else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
        {
            var acc128 = Vector128<float>.Zero;
            int simdEnd = length - (length % Vector128<float>.Count);

            for (; i < simdEnd; i += Vector128<float>.Count)
            {
                var va = Vector128.LoadUnsafe(ref Unsafe.AsRef(in a[i]));
                var vb = Vector128.LoadUnsafe(ref Unsafe.AsRef(in b[i]));
                acc128 = Vector128.Add(acc128, Vector128.Multiply(va, vb));
            }

            sum = Vector128.Sum(acc128);
        }

        // Remainder loop
        for (; i < length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    /// <summary>
    /// Computes cosine similarity between an FP32 normalized query vector and an SQ8 quantized vector.
    /// Fast fused kernel with zero intermediate allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static float CosineSimilaritySq8(
        ReadOnlySpan<float> queryNormalized,
        ReadOnlySpan<sbyte> targetSq8,
        float targetScale,
        float targetOffset,
        float querySumPrecomputed)
    {
        if (queryNormalized.Length != targetSq8.Length)
            throw new ArgumentException("Query and target vector dimensions must match.");

        int length = queryNormalized.Length;
        float dotSum = 0.0f;
        int i = 0;

        if (Avx2.IsSupported && length >= 8)
        {
            var acc = Vector256<float>.Zero;
            var vScale = Vector256.Create(targetScale);
            var vOffset = Vector256.Create(targetOffset);
            var v127 = Vector256.Create(127.0f);
            int simdEnd = length - (length % 8);

            fixed (float* qPtr = queryNormalized)
            fixed (sbyte* tPtr = targetSq8)
            {
                for (; i < simdEnd; i += 8)
                {
                    // Load 8 float query values
                    var q = Avx.LoadVector256(qPtr + i);

                    // Load 8 sbytes -> widen to 8 int32 -> convert to 8 float32
                    var rawBytes = Vector128.Load(tPtr + i);
                    var ints = Avx2.ConvertToVector256Int32(rawBytes);
                    var floats = Avx.ConvertToVector256Single(ints);

                    // (sbyte + 127) * scale + offset
                    var targetDequant = Avx.Add(Avx.Multiply(Avx.Add(floats, v127), vScale), vOffset);

                    if (Fma.IsSupported)
                    {
                        acc = Fma.MultiplyAdd(q, targetDequant, acc);
                    }
                    else
                    {
                        acc = Avx.Add(acc, Avx.Multiply(q, targetDequant));
                    }
                }
            }

            dotSum = Vector256.Sum(acc);
        }

        for (; i < length; i++)
        {
            float targetVal = (targetSq8[i] + 127) * targetScale + targetOffset;
            dotSum += queryNormalized[i] * targetVal;
        }

        return Math.Clamp(dotSum, -1.0f, 1.0f);
    }
}
