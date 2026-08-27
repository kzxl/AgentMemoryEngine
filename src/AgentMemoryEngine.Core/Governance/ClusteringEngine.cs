using AgentMemoryEngine.Core.BinaryLayout;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;

namespace AgentMemoryEngine.Core.Governance;

/// <summary>
/// Result of a Semantic Induction operation from clustered memories.
/// </summary>
public record AmeInductionResult
{
    public uint ClusterIndex { get; init; }
    public uint SemanticRuleId { get; init; }
    public IReadOnlyList<uint> MemberEpisodeIds { get; init; } = [];
    public string SynthesizedRuleText { get; init; } = string.Empty;
}

/// <summary>
/// Density-Based Spatial Clustering (DBSCAN) and Semantic Rule Induction Engine.
/// Automatically detects recurring episodic patterns and elevates them into permanent Semantic rules.
/// </summary>
public sealed class ClusteringEngine
{
    private readonly AmeContainer _container;

    public ClusteringEngine(AmeContainer container)
    {
        _container = container;
    }

    /// <summary>
    /// Executes DBSCAN clustering on all Episodic memories and elevates clusters into Semantic rules.
    /// </summary>
    /// <param name="epsDistance">Maximum cosine distance threshold (default: 0.15, meaning similarity >= 0.85)</param>
    /// <param name="minPoints">Minimum episodes required to form a cluster (default: 3)</param>
    public IReadOnlyList<AmeInductionResult> InduceSemanticRules(float epsDistance = 0.15f, int minPoints = 3)
    {
        var episodicRecords = new List<(uint MemoryId, float[] Vector, string Payload)>();

        // 1. Collect all Episodic memory vectors
        for (uint id = 1; id <= _container.RecordCount; id++)
        {
            if (_container.TryGetRecord(id, out var rec, out var payload))
            {
                if (rec.Tier == (byte)AmeMemoryTier.Episodic)
                {
                    float[] vec = new float[_container.Dimension];
                    if (_container.TryGetVector(id, vec))
                    {
                        Quantizer.Normalize(vec);
                        episodicRecords.Add((id, vec, payload));
                    }
                }
            }
        }

        int n = episodicRecords.Count;
        if (n < minPoints) return Array.Empty<AmeInductionResult>();

        // 2. Compute Cosine Distance Matrix
        float[,] distMatrix = new float[n, n];
        for (int i = 0; i < n; i++)
        {
            distMatrix[i, i] = 0.0f;
            for (int j = i + 1; j < n; j++)
            {
                float sim = SimdVectorEngine.DotProduct(episodicRecords[i].Vector, episodicRecords[j].Vector);
                float dist = Math.Clamp(1.0f - sim, 0.0f, 2.0f);
                distMatrix[i, j] = dist;
                distMatrix[j, i] = dist;
            }
        }

        // 3. Run DBSCAN
        int[] labels = new int[n]; // 0: unvisited, -1: noise, >= 1: cluster ID
        int clusterId = 0;

        for (int i = 0; i < n; i++)
        {
            if (labels[i] != 0) continue;

            var neighbors = GetNeighbors(i, n, distMatrix, epsDistance);
            if (neighbors.Count < minPoints)
            {
                labels[i] = -1; // Noise
                continue;
            }

            clusterId++;
            labels[i] = clusterId;

            var queue = new Queue<int>(neighbors);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (labels[current] == -1) labels[current] = clusterId;
                if (labels[current] != 0) continue;

                labels[current] = clusterId;
                var currentNeighbors = GetNeighbors(current, n, distMatrix, epsDistance);
                if (currentNeighbors.Count >= minPoints)
                {
                    foreach (var neighbor in currentNeighbors)
                    {
                        if (labels[neighbor] <= 0) queue.Enqueue(neighbor);
                    }
                }
            }
        }

        // 4. Synthesize Semantic Rules for each cluster
        var results = new List<AmeInductionResult>();

        for (int c = 1; c <= clusterId; c++)
        {
            var clusterMembers = new List<(uint MemoryId, string Payload)>();
            for (int i = 0; i < n; i++)
            {
                if (labels[i] == c)
                {
                    clusterMembers.Add((episodicRecords[i].MemoryId, episodicRecords[i].Payload));
                }
            }

            if (clusterMembers.Count >= minPoints)
            {
                // Synthesize generalized rule
                string synthesizedRule = $"Synthesized Semantic Standard (Elevated from {clusterMembers.Count} recurring episodes): {clusterMembers[0].Payload.Split('|')[0].Trim()}";

                // Average embedding vector
                float[] clusterAvgVec = new float[_container.Dimension];
                for (int d = 0; d < _container.Dimension; d++)
                {
                    float sum = 0.0f;
                    foreach (var member in clusterMembers)
                    {
                        int index = episodicRecords.FindIndex(e => e.MemoryId == member.MemoryId);
                        sum += episodicRecords[index].Vector[d];
                    }
                    clusterAvgVec[d] = sum / clusterMembers.Count;
                }
                Quantizer.Normalize(clusterAvgVec);

                // Commit permanent Semantic Memory (DecayRate = 0)
                uint newRuleId = _container.AppendRecord(
                    AmeMemoryTier.Semantic,
                    synthesizedRule,
                    clusterAvgVec,
                    importance: 95,
                    confidence: 100,
                    decayRate: 0);

                // Create CSR Graph edges: [Episode] ──DerivedFrom──> [SemanticRule]
                var memberIds = new List<uint>();
                foreach (var member in clusterMembers)
                {
                    _container.AddRelationship(member.MemoryId, newRuleId, AmeEdgeType.DerivedFrom, weight: 90);
                    memberIds.Add(member.MemoryId);
                }

                results.Add(new AmeInductionResult
                {
                    ClusterIndex = (uint)c,
                    SemanticRuleId = newRuleId,
                    MemberEpisodeIds = memberIds,
                    SynthesizedRuleText = synthesizedRule
                });
            }
        }

        return results;
    }

    private static List<int> GetNeighbors(int pointIndex, int totalPoints, float[,] distMatrix, float eps)
    {
        var neighbors = new List<int>();
        for (int j = 0; j < totalPoints; j++)
        {
            if (distMatrix[pointIndex, j] <= eps)
            {
                neighbors.Add(j);
            }
        }
        return neighbors;
    }
}
