using AgentMemoryEngine.Core.Vector;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class HnswIndexTests
{
    [Fact]
    public void Hnsw_InsertAndSearch_AchievesHighRecall()
    {
        const int dimension = 64;
        const int count = 300;
        var rng = new Random(42);

        var hnsw = new HnswIndex(dimension, m: 16, efConstruction: 64);
        var vectors = new List<float[]>();

        for (int i = 0; i < count; i++)
        {
            float[] v = new float[dimension];
            for (int d = 0; d < dimension; d++)
            {
                v[d] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }
            Quantizer.Normalize(v);
            vectors.Add(v);
            hnsw.Insert((uint)(i + 1), v);
        }

        Assert.Equal(count, hnsw.Count);
        Assert.True(hnsw.MaxLevel >= 0);

        // Test 20 random queries
        int correctTop1Matches = 0;
        const int numQueries = 20;

        for (int q = 0; q < numQueries; q++)
        {
            float[] query = new float[dimension];
            for (int d = 0; d < dimension; d++)
            {
                query[d] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }
            Quantizer.Normalize(query);

            // 1. Exact brute force Top-1
            int exactBestIdx = -1;
            float exactBestSim = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float sim = SimdVectorEngine.DotProduct(query, vectors[i]);
                if (sim > exactBestSim)
                {
                    exactBestSim = sim;
                    exactBestIdx = i;
                }
            }

            // 2. HNSW Search
            var knn = hnsw.SearchKnn(query, topK: 5, ef: 32);
            Assert.NotEmpty(knn);

            if (knn.Any(k => k.NodeIndex == exactBestIdx))
            {
                correctTop1Matches++;
            }
        }

        double recall = (double)correctTop1Matches / numQueries;
        Assert.True(recall >= 0.85, $"HNSW Top-5 recall was {recall:P0}, expected >= 85%");
    }
}
