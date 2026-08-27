using AgentMemoryEngine.Core.BinaryLayout;

namespace AgentMemoryEngine.Core.Scoring;

/// <summary>
/// Configurable weights for Multi-factor Fused Cognitive Scoring.
/// </summary>
public record AmeScoringWeights
{
    public float WeightVector { get; init; } = 0.35f;
    public float WeightImportance { get; init; } = 0.15f;
    public float WeightRecency { get; init; } = 0.15f;
    public float WeightFrequency { get; init; } = 0.10f;
    public float WeightGraph { get; init; } = 0.15f;
    public float WeightConfidence { get; init; } = 0.10f;

    public static readonly AmeScoringWeights Default = new();
}

/// <summary>
/// Mathematical engine for calculating continuous Ebbinghaus decay and fused cognitive scores.
/// </summary>
public static class AmeScoringEngine
{
    /// <summary>
    /// Computes the Ebbinghaus memory retention score R(M, t) in the range [0.0, 1.0].
    /// </summary>
    /// <param name="record">The cognitive record</param>
    /// <param name="currentTimestampSeconds">Current unix epoch timestamp in seconds</param>
    /// <param name="baseRetentionHours">Base retention half-life in hours (default 72.0h)</param>
    public static float ComputeRetention(
        in AmeCognitiveRecord record,
        uint currentTimestampSeconds,
        double baseRetentionHours = AmeConstants.DefaultBaseRetentionHours)
    {
        // Semantic memory with DecayRate = 0 has zero decay (permanent retention)
        if (record.DecayRate == 0 || record.Tier == (byte)AmeMemoryTier.Semantic)
        {
            return 1.0f;
        }

        uint deltaSeconds = currentTimestampSeconds > record.LastAccessedTimestamp
            ? currentTimestampSeconds - record.LastAccessedTimestamp
            : 0;

        double deltaHours = deltaSeconds / 3600.0;

        // Frequency reinforcement: Access count extends retention half-life
        double freqBoost = 1.0 + 0.5 * Math.Log2(1.0 + record.AccessFrequency);
        
        // Decay steepness coefficient (higher decay_rate means faster decay)
        double decayFactor = (256.0 - record.DecayRate) / 128.0;
        if (decayFactor <= 0.01) decayFactor = 0.01;

        double tau = baseRetentionHours * freqBoost * decayFactor;

        double retention = Math.Exp(-deltaHours / tau);
        return (float)Math.Clamp(retention, 0.0, 1.0);
    }

    /// <summary>
    /// Computes the single-pass composite score for a memory record.
    /// </summary>
    public static float ComputeCompositeScore(
        float vectorSimilarity,
        in AmeCognitiveRecord record,
        uint currentTimestampSeconds,
        float graphProximity = 0.0f,
        AmeScoringWeights? weights = null)
    {
        var w = weights ?? AmeScoringWeights.Default;

        // Normalize inputs to [0.0, 1.0]
        float sim = Math.Clamp(vectorSimilarity, 0.0f, 1.0f);
        float importanceNorm = record.Importance / 100.0f;
        float confidenceNorm = record.Confidence / 100.0f;
        float recencyNorm = ComputeRetention(record, currentTimestampSeconds);
        
        // Frequency normalized via log scale (assuming typical max access = 100)
        float freqNorm = (float)(Math.Log2(1.0 + record.AccessFrequency) / Math.Log2(101.0));
        freqNorm = Math.Clamp(freqNorm, 0.0f, 1.0f);

        float graphNorm = Math.Clamp(graphProximity, 0.0f, 1.0f);

        float composite = (w.WeightVector * sim) +
                          (w.WeightImportance * importanceNorm) +
                          (w.WeightRecency * recencyNorm) +
                          (w.WeightFrequency * freqNorm) +
                          (w.WeightGraph * graphNorm) +
                          (w.WeightConfidence * confidenceNorm);

        return Math.Clamp(composite, 0.0f, 1.0f);
    }
}
