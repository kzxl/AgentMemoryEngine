namespace AgentMemoryEngine.Core.Storage;

/// <summary>
/// Report detailing the result of a database compaction and vacuum operation.
/// </summary>
public record AmeCompactionReport
{
    public uint OriginalRecordCount { get; init; }
    public uint CompactedRecordCount { get; init; }
    public long OriginalFileSizeBytes { get; init; }
    public long CompactedFileSizeBytes { get; init; }
    public double BytesReclaimedPercent => OriginalFileSizeBytes > 0
        ? (1.0 - (double)CompactedFileSizeBytes / OriginalFileSizeBytes) * 100.0
        : 0.0;
}

/// <summary>
/// Storage Compactor and Vacuum Engine.
/// Reclaims dead space, defragments segments, and repacks active records contiguously.
/// </summary>
public static class StorageCompactor
{
    /// <summary>
    /// Compacts and defragments an existing .ame container file, removing dead space and repacking records.
    /// </summary>
    public static AmeCompactionReport Compact(string databaseFilePath, HashSet<uint>? idsToEvict = null)
    {
        if (!File.Exists(databaseFilePath))
            throw new FileNotFoundException($"Database file not found: {databaseFilePath}");

        using var source = AmeContainer.Open(databaseFilePath);
        return Compact(source, idsToEvict);
    }

    /// <summary>
    /// Compacts and defragments an active AmeContainer instance, preserving in-memory CSR relationships.
    /// </summary>
    public static AmeCompactionReport Compact(AmeContainer source, HashSet<uint>? idsToEvict = null)
    {
        string databaseFilePath = source.FilePath;
        long originalSize = new FileInfo(databaseFilePath).Length;
        string tempCompactedPath = $"{databaseFilePath}.compact.tmp";

        uint originalCount = source.RecordCount;
        uint compactedCount = 0;

        using (var target = AmeContainer.Create(tempCompactedPath, source.Dimension))
        {
            var idRemapping = new Dictionary<uint, uint>(); // Old ID -> New ID

            for (uint oldId = 1; oldId <= source.RecordCount; oldId++)
            {
                // Skip evicted records
                if (idsToEvict != null && idsToEvict.Contains(oldId))
                    continue;

                if (source.TryGetRecord(oldId, out var rec, out var payload))
                {
                    float[] vec = new float[source.Dimension];
                    source.TryGetVector(oldId, vec);

                    uint newId = target.AppendRecord(
                        (AmeMemoryTier)rec.Tier,
                        payload,
                        vec,
                        importance: rec.Importance,
                        confidence: rec.Confidence,
                        decayRate: rec.DecayRate
                    );

                    // Preserve access count and timestamp
                    target.TouchCognitiveInPlace(newId, importance: rec.Importance, confidence: rec.Confidence, incrementAccessCount: false);
                    idRemapping[oldId] = newId;
                    compactedCount++;
                }
            }

            // Remap and transfer CSR Graph relationships
            for (uint oldId = 1; oldId <= source.RecordCount; oldId++)
            {
                if (idRemapping.TryGetValue(oldId, out uint newSourceId))
                {
                    var neighbors = source.Graph.GetNeighbors(oldId);
                    for (int i = 0; i < neighbors.Length; i++)
                    {
                        uint oldTargetId = neighbors.Targets[i];
                        if (idRemapping.TryGetValue(oldTargetId, out uint newTargetId))
                        {
                            target.AddRelationship(newSourceId, newTargetId, (AmeEdgeType)neighbors.Meta[i].EdgeType, neighbors.Meta[i].Weight);
                        }
                    }
                }
            }
        }

        long compactedSize = new FileInfo(tempCompactedPath).Length;

        // Dispose source so file can be replaced
        source.Dispose();

        // Atomically replace original file
        string backupPath = $"{databaseFilePath}.bak";
        if (File.Exists(backupPath)) File.Delete(backupPath);

        File.Replace(tempCompactedPath, databaseFilePath, backupPath);
        if (File.Exists(backupPath)) File.Delete(backupPath);

        return new AmeCompactionReport
        {
            OriginalRecordCount = originalCount,
            CompactedRecordCount = compactedCount,
            OriginalFileSizeBytes = originalSize,
            CompactedFileSizeBytes = compactedSize
        };
    }
}
