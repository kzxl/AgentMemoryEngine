using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class ContainerIntegrationTests : IDisposable
{
    private readonly string _tempFilePath;

    public ContainerIntegrationTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"ame_test_{Guid.NewGuid():N}.ame");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
        {
            try { File.Delete(_tempFilePath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void FullLifecycle_CreateAppendQueryMutateReopen_Succeeds()
    {
        const int dimension = 384;

        // 1. Create container
        using (var container = AmeContainer.Create(_tempFilePath, dimension))
        {
            Assert.Equal(0u, container.RecordCount);
            Assert.Equal((ushort)dimension, container.Dimension);

            // Generate mock embeddings
            float[] vec1 = new float[dimension];
            vec1[0] = 1.0f; // Feature vector pointing along axis 0
            Quantizer.Normalize(vec1);

            float[] vec2 = new float[dimension];
            vec2[1] = 1.0f; // Feature vector pointing along axis 1
            Quantizer.Normalize(vec2);

            // 2. Append memories
            uint id1 = container.AppendRecord(
                AmeMemoryTier.Episodic,
                "GridControl freeze after RunAfterShown | Invoked sync void | Use async Task",
                vec1,
                importance: 80,
                confidence: 100);

            uint id2 = container.AppendRecord(
                AmeMemoryTier.Semantic,
                "Coding Standard: All controller methods must return Task and log exceptions",
                vec2,
                importance: 90,
                confidence: 100);

            Assert.Equal(1u, id1);
            Assert.Equal(2u, id2);
            Assert.Equal(2u, container.RecordCount);

            // 3. Query Fused Search with query close to vec1
            float[] queryVec = new float[dimension];
            queryVec[0] = 0.95f;
            queryVec[1] = 0.05f;
            Quantizer.Normalize(queryVec);

            var searchResults = container.QueryFused(queryVec, topK: 5, minScore: 0.1f);
            Assert.NotEmpty(searchResults);
            Assert.Equal(id1, searchResults[0].MemoryId);
            Assert.Contains("GridControl freeze", searchResults[0].Payload);

            // 4. In-Place Mutation
            bool touchSuccess = container.TouchCognitiveInPlace(id1, importance: 95, confidence: 100, incrementAccessCount: true);
            Assert.True(touchSuccess);

            // Validate mutated record
            bool getSuccess = container.TryGetRecord(id1, out var record1, out var payload1);
            Assert.True(getSuccess);
            Assert.Equal(95, record1.Importance);
            Assert.Equal(2u, record1.AccessFrequency);
            Assert.Equal("GridControl freeze after RunAfterShown | Invoked sync void | Use async Task", payload1);
        }

        // 5. Re-open container and verify persistence
        using (var reloadedContainer = AmeContainer.Open(_tempFilePath))
        {
            Assert.Equal(2u, reloadedContainer.RecordCount);
            bool getSuccess = reloadedContainer.TryGetRecord(1, out var rec, out var payload);
            Assert.True(getSuccess);
            Assert.Equal(95, rec.Importance);
            Assert.Equal(2u, rec.AccessFrequency);
            Assert.Contains("GridControl", payload);
        }
    }
}
