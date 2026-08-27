using AgentMemoryEngine.Core.Vector;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class BinaryQuantizationTests
{
    [Fact]
    public void BQ_Quantize1Bit_AndHammingDistance_ComputesCorrectly()
    {
        const int dimension = 384;
        float[] v1 = new float[dimension];
        float[] v2 = new float[dimension];

        // Identical vector signs
        for (int i = 0; i < dimension; i++)
        {
            v1[i] = i % 2 == 0 ? 0.5f : -0.5f;
            v2[i] = i % 2 == 0 ? 1.0f : -1.0f;
        }

        ulong[] b1 = new ulong[(dimension + 63) / 64];
        ulong[] b2 = new ulong[(dimension + 63) / 64];

        BinaryQuantizer.Quantize1Bit(v1, b1);
        BinaryQuantizer.Quantize1Bit(v2, b2);

        int dist = BinaryQuantizer.ComputeHammingDistance(b1, b2);
        float sim = BinaryQuantizer.ComputeHammingSimilarity(b1, b2, dimension);

        Assert.Equal(0, dist);
        Assert.Equal(1.0f, sim);

        // Opposite vector signs
        for (int i = 0; i < dimension; i++)
        {
            v2[i] = -v1[i];
        }

        BinaryQuantizer.Quantize1Bit(v2, b2);
        dist = BinaryQuantizer.ComputeHammingDistance(b1, b2);
        sim = BinaryQuantizer.ComputeHammingSimilarity(b1, b2, dimension);

        Assert.Equal(dimension, dist);
        Assert.Equal(0.0f, sim);
    }
}
