using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class StorageCompactorTests : IDisposable
{
    private readonly string _tempDbPath;

    public StorageCompactorTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ame_compactor_{Guid.NewGuid():N}.ame");
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void StorageCompactor_CompactsAndEvictsSelectedRecords()
    {
        // 1. Create and populate container with 4 records and 1 relationship
        var container = AmeContainer.Create(_tempDbPath, dimension: 384);
        float[] vec = new float[384]; vec[0] = 1.0f; Quantizer.Normalize(vec);

        uint id1 = container.AppendRecord(AmeMemoryTier.Episodic, "Record 1 (Keep)", vec);
        uint id2 = container.AppendRecord(AmeMemoryTier.Episodic, "Record 2 (Evict)", vec);
        uint id3 = container.AppendRecord(AmeMemoryTier.Episodic, "Record 3 (Keep)", vec);
        uint id4 = container.AppendRecord(AmeMemoryTier.Episodic, "Record 4 (Evict)", vec);

        // Connect Record 1 -> Record 3
        container.AddRelationship(id1, id3, AmeEdgeType.FollowedBy, weight: 80);

        // 2. Compact and evict records 2 and 4
        var report = StorageCompactor.Compact(container, idsToEvict: [2, 4]);

        Assert.Equal(4u, report.OriginalRecordCount);
        Assert.Equal(2u, report.CompactedRecordCount);

        // 3. Re-open container and verify integrity
        using (var compacted = AmeContainer.Open(_tempDbPath))
        {
            Assert.Equal(2u, compacted.RecordCount);

            bool ok1 = compacted.TryGetRecord(1, out var rec1, out string p1);
            bool ok2 = compacted.TryGetRecord(2, out var rec2, out string p2);

            Assert.True(ok1);
            Assert.True(ok2);
            Assert.Contains("Record 1", p1);
            Assert.Contains("Record 3", p2);
            Assert.Equal(1u, rec1.MemoryId);
            Assert.Equal(2u, rec2.MemoryId);
        }
    }
}
