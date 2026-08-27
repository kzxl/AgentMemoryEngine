using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Governance;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class ClusteringEngineTests : IDisposable
{
    private readonly string _tempDbPath;

    public ClusteringEngineTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ame_clustering_{Guid.NewGuid():N}.ame");
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void DBSCAN_InducesSemanticRule_AndCreatesDerivedGraphEdges()
    {
        using (var container = AmeContainer.Create(_tempDbPath, dimension: 384))
        {
            // Seed 3 similar episodic lessons
            float[] vec1 = new float[384]; vec1[0] = 1.0f; Quantizer.Normalize(vec1);
            float[] vec2 = new float[384]; vec2[0] = 0.98f; vec2[1] = 0.02f; Quantizer.Normalize(vec2);
            float[] vec3 = new float[384]; vec3[0] = 0.96f; vec3[1] = 0.04f; Quantizer.Normalize(vec3);

            uint id1 = container.AppendRecord(AmeMemoryTier.Episodic, "GridControl freeze #1 | Sync void | Use async Task", vec1);
            uint id2 = container.AppendRecord(AmeMemoryTier.Episodic, "GridControl freeze #2 | Sync delegate | Use async Task", vec2);
            uint id3 = container.AppendRecord(AmeMemoryTier.Episodic, "GridControl freeze #3 | Blocking call | Use async Task", vec3);

            var clustering = new ClusteringEngine(container);
            var results = clustering.InduceSemanticRules(epsDistance: 0.3f, minPoints: 3);

            Assert.Single(results);
            var rule = results[0];
            Assert.Equal(3, rule.MemberEpisodeIds.Count);
            Assert.True(rule.SemanticRuleId > 3);

            // Verify newly created Semantic rule exists in container
            bool exists = container.TryGetRecord(rule.SemanticRuleId, out var semRec, out var semPayload);
            Assert.True(exists);
            Assert.Equal((byte)AmeMemoryTier.Semantic, semRec.Tier);
            Assert.Equal(0, semRec.DecayRate); // Permanent zero decay
            Assert.Contains("Synthesized Semantic Standard", semPayload);

            // Verify CSR Graph edges (Episode -> Semantic Rule)
            var neighbors1 = container.Graph.GetNeighbors(id1);
            Assert.NotEmpty(neighbors1.Targets.ToArray());
            Assert.Contains(rule.SemanticRuleId, neighbors1.Targets.ToArray());
        }
    }
}
