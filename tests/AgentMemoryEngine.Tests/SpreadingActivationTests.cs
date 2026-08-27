using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Graph;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class SpreadingActivationTests
{
    [Fact]
    public void SpreadingActivation_PropagatesEnergyAcrossMultiHopChain()
    {
        var graph = new CsrGraph();

        // Build chain: 1 -> 2 (weight: 100) and 2 -> 3 (weight: 100)
        graph.AddEdge(1, 2, AmeEdgeType.DependsOn, weight: 100);
        graph.AddEdge(2, 3, AmeEdgeType.FollowedBy, weight: 100);

        var activations = graph.ComputeSpreadingActivation(new uint[] { 1 }, maxHops: 2, decayFactor: 0.8f);

        Assert.True(activations.ContainsKey(1));
        Assert.True(activations.ContainsKey(2));
        Assert.True(activations.ContainsKey(3));

        Assert.Equal(1.0f, activations[1]);
        Assert.Equal(0.8f, activations[2], precision: 2);
        Assert.Equal(0.64f, activations[3], precision: 2);
    }
}
