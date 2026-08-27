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
        float querySumPrecomputed = 0.0f)
    {
        if (queryNormalized.Length != targetSq8.Length)
            throw new ArgumentException("Query and target vector dimensions must match.");

        fixed (float* qPtr = queryNormalized)
        fixed (sbyte* tPtr = targetSq8)
        {
            return CosineSimilaritySq8(qPtr, tPtr, queryNormalized.Length, targetScale, targetOffset);
        }
    }

    /// <summary>
    /// Direct pointer-based, 2x unrolled AVX2/FMA SIMD fused cosine similarity kernel.
    /// Eliminates Span boundary checks and fixed-pinning overhead for large-scale scans.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    public static float CosineSimilaritySq8(
        float* qPtr,
        sbyte* tPtr,
        int length,
        float targetScale,
        float targetOffset)
    {
        float dotSum = 0.0f;
        int i = 0;

        if (Avx2.IsSupported && length >= 16)
        {
            var acc0 = Vector256<float>.Zero;
            var acc1 = Vector256<float>.Zero;
            var vScale = Vector256.Create(targetScale);
            var vOffset = Vector256.Create(targetOffset);
            var v127 = Vector256.Create(127.0f);
            int simdEnd = length - (length % 16);

            for (; i < simdEnd; i += 16)
            {
                // Unroll 1: 0..7
                var q0 = Avx.LoadVector256(qPtr + i);
                var rawBytes0 = Vector128.Load(tPtr + i);
                var ints0 = Avx2.ConvertToVector256Int32(rawBytes0);
                var floats0 = Avx.ConvertToVector256Single(ints0);
                var targetDequant0 = Avx.Add(Avx.Multiply(Avx.Add(floats0, v127), vScale), vOffset);

                // Unroll 2: 8..15
                var q1 = Avx.LoadVector256(qPtr + i + 8);
                var rawBytes1 = Vector128.Load(tPtr + i + 8);
                var ints1 = Avx2.ConvertToVector256Int32(rawBytes1);
                var floats1 = Avx.ConvertToVector256Single(ints1);
                var targetDequant1 = Avx.Add(Avx.Multiply(Avx.Add(floats1, v127), vScale), vOffset);

                if (Fma.IsSupported)
                {
                    acc0 = Fma.MultiplyAdd(q0, targetDequant0, acc0);
                    acc1 = Fma.MultiplyAdd(q1, targetDequant1, acc1);
                }
                else
                {
                    acc0 = Avx.Add(acc0, Avx.Multiply(q0, targetDequant0));
                    acc1 = Avx.Add(acc1, Avx.Multiply(q1, targetDequant1));
                }
            }

            dotSum = Vector256.Sum(Avx.Add(acc0, acc1));
        }

        for (; i < length; i++)
        {
            float targetVal = (tPtr[i] + 127) * targetScale + targetOffset;
            dotSum += qPtr[i] * targetVal;
        }

        return Math.Clamp(dotSum, -1.0f, 1.0f);
    }
}
