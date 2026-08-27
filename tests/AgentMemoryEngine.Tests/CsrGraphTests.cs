using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Graph;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class CsrGraphTests
{
    [Fact]
    public void AddEdge_AndGetNeighbors_ReturnsExactSlice()
    {
        var graph = new CsrGraph();

        // Node 1 connects to Node 2 (weight 90) and Node 3 (weight 80)
        graph.AddEdge(1, 2, AmeEdgeType.DependsOn, weight: 90);
        graph.AddEdge(1, 3, AmeEdgeType.FixesBugIn, weight: 80);

        var neighbors = graph.GetNeighbors(1);

        Assert.Equal(2, neighbors.Length);
        Assert.Equal(2u, neighbors.Targets[0]);
        Assert.Equal(3u, neighbors.Targets[1]);
        Assert.Equal(90, neighbors.Meta[0].Weight);
        Assert.Equal(80, neighbors.Meta[1].Weight);
    }

    [Fact]
    public void ComputeProximity_1HopDirectMatch_CalculatesCorrectScore()
    {
        var graph = new CsrGraph();

        // Memory Node 1 ──FixesBugIn──> Code Symbol 100 (Weight: 100)
        graph.AddEdge(1, 100, AmeEdgeType.FixesBugIn, weight: 100);

        uint[] activeWorkingSymbols = [100];
        float proximity = graph.ComputeProximity(1, activeWorkingSymbols);

        Assert.Equal(1.0f, proximity);
    }

    [Fact]
    public void ComputeProximity_2HopAttenuatedMatch_AppliesHopDecay()
    {
        var graph = new CsrGraph();

        // Node 1 ──DependsOn──> Node 2 (weight: 100)
        // Node 2 ──DependsOn──> Symbol 50 (weight: 80)
        graph.AddEdge(1, 2, AmeEdgeType.DependsOn, weight: 100);
        graph.AddEdge(2, 50, AmeEdgeType.DependsOn, weight: 80);

        uint[] activeWorkingSymbols = [50];
        float proximity = graph.ComputeProximity(1, activeWorkingSymbols, hopAttenuation: 0.7f, maxHops: 2);

        // Expected: 1.0 * 0.8 * 0.7 = 0.56
        Assert.True(MathF.Abs(proximity - 0.56f) < 1e-4f, $"Proximity was {proximity}, expected ~0.56");
    }
}
