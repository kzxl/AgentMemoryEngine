using AgentMemoryEngine.Core.Vector;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class QuantizerAndSimdTests
{
    [Fact]
    public void Normalize_ProducesUnitLengthVector()
    {
        float[] v = [3.0f, 4.0f, 0.0f, 0.0f];
        Quantizer.Normalize(v);

        float norm = MathF.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2] + v[3] * v[3]);
        Assert.True(MathF.Abs(norm - 1.0f) < 1e-6f);
    }

    [Fact]
    public void SQ8Quantization_PreservesHighCosineFidelity()
    {
        const int dimension = 384;
        var rng = new Random(42);
        float[] original = new float[dimension];
        for (int i = 0; i < dimension; i++)
        {
            original[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }
        Quantizer.Normalize(original);

        sbyte[] quantized = new sbyte[dimension];
        Quantizer.QuantizeSQ8(original, quantized, out float scale, out float offset);

        float[] reconstructed = new float[dimension];
        Quantizer.DequantizeSQ8(quantized, reconstructed, scale, offset);
        Quantizer.Normalize(reconstructed);

        float cosineSimilarity = SimdVectorEngine.DotProduct(original, reconstructed);
        
        // Assert reconstruction cosine fidelity > 0.99 (error < 1%)
        Assert.True(cosineSimilarity > 0.99f, $"Reconstruction cosine similarity was {cosineSimilarity}, expected > 0.99");
    }

    [Fact]
    public void SimdDotProduct_MatchesScalarBaseline()
    {
        const int dimension = 384;
        var rng = new Random(100);
        float[] a = new float[dimension];
        float[] b = new float[dimension];
        for (int i = 0; i < dimension; i++)
        {
            a[i] = (float)rng.NextDouble();
            b[i] = (float)rng.NextDouble();
        }

        float simdResult = SimdVectorEngine.DotProduct(a, b);

        float scalarExpected = 0.0f;
        for (int i = 0; i < dimension; i++)
        {
            scalarExpected += a[i] * b[i];
        }

        Assert.True(MathF.Abs(simdResult - scalarExpected) < 1e-4f, $"SIMD {simdResult} vs Scalar {scalarExpected}");
    }
}
