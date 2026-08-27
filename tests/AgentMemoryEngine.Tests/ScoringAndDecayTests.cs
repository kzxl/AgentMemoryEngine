using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.BinaryLayout;
using AgentMemoryEngine.Core.Scoring;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class ScoringAndDecayTests
{
    [Fact]
    public void SemanticMemory_HasZeroDecay_AlwaysRetainsMaxScore()
    {
        uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var record = new AmeCognitiveRecord
        {
            Tier = (byte)AmeMemoryTier.Semantic,
            DecayRate = 0,
            LastAccessedTimestamp = now - (365 * 24 * 3600), // 1 year ago
            AccessFrequency = 1
        };

        float retention = AmeScoringEngine.ComputeRetention(record, now);
        Assert.Equal(1.0f, retention);
    }

    [Fact]
    public void EpisodicMemory_DecaysOverTimeMonotonically()
    {
        uint now = 1_000_000;
        var record = new AmeCognitiveRecord
        {
            Tier = (byte)AmeMemoryTier.Episodic,
            DecayRate = 128,
            LastAccessedTimestamp = now,
            AccessFrequency = 1
        };

        float r0 = AmeScoringEngine.ComputeRetention(record, now);
        float r1Day = AmeScoringEngine.ComputeRetention(record, now + 86400);
        float r3Days = AmeScoringEngine.ComputeRetention(record, now + (3 * 86400));
        float r7Days = AmeScoringEngine.ComputeRetention(record, now + (7 * 86400));

        Assert.Equal(1.0f, r0);
        Assert.True(r1Day < r0, "Retention must decay after 1 day");
        Assert.True(r3Days < r1Day, "Retention must decay further after 3 days");
        Assert.True(r7Days < r3Days, "Retention must decay further after 7 days");
    }

    [Fact]
    public void FrequentAccess_ReinforcesMemoryRetention()
    {
        uint now = 1_000_000;
        uint checkTime = now + (3 * 86400); // 3 days later

        var singleAccess = new AmeCognitiveRecord
        {
            Tier = (byte)AmeMemoryTier.Episodic,
            DecayRate = 128,
            LastAccessedTimestamp = now,
            AccessFrequency = 1
        };

        var reinforcedAccess = new AmeCognitiveRecord
        {
            Tier = (byte)AmeMemoryTier.Episodic,
            DecayRate = 128,
            LastAccessedTimestamp = now,
            AccessFrequency = 10 // Accessed 10 times
        };

        float rSingle = AmeScoringEngine.ComputeRetention(singleAccess, checkTime);
        float rReinforced = AmeScoringEngine.ComputeRetention(reinforcedAccess, checkTime);

        Assert.True(rReinforced > rSingle, $"Reinforced retention ({rReinforced}) should be higher than single ({rSingle})");
    }
}
