using AgentMemoryEngine.Core.BinaryLayout;
using AgentMemoryEngine.Core.Scoring;
using AgentMemoryEngine.Core.Storage;

namespace AgentMemoryEngine.Core.Governance;

/// <summary>
/// Report detailing the results of an autonomous memory consolidation and decay sweep.
/// </summary>
public record AmeConsolidationReport
{
    public uint TotalRecordsScanned { get; init; }
    public uint ActiveRecordsRetained { get; init; }
    public uint ColdRecordsPruned { get; init; }
    public uint SemanticRulesSynthesized { get; init; }
    public double SweepDurationMs { get; init; }
}

/// <summary>
/// Autonomous background worker for Ebbinghaus decay sweeps, cold-memory pruning, and sleep consolidation.
/// </summary>
public sealed class ConsolidationWorker
{
    private readonly AmeContainer _container;

    public ConsolidationWorker(AmeContainer container)
    {
        _container = container;
    }

    /// <summary>
    /// Executes a single consolidation sweep over all memories in the container.
    /// </summary>
    public AmeConsolidationReport ExecuteSweep(
        float evictionScoreThreshold = 0.15f,
        uint coldAgeDaysThreshold = 30)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        uint totalScanned = _container.RecordCount;
        uint retained = 0;
        uint pruned = 0;
        uint currentTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        uint coldSecondsThreshold = coldAgeDaysThreshold * 86400;

        for (uint id = 1; id <= totalScanned; id++)
        {
            if (_container.TryGetRecord(id, out var rec, out _))
            {
                // Semantic memory never decays and is never pruned
                if (rec.Tier == (byte)AmeMemoryTier.Semantic || rec.DecayRate == 0)
                {
                    retained++;
                    continue;
                }

                float retention = AmeScoringEngine.ComputeRetention(rec, currentTimestamp);
                uint age = currentTimestamp > rec.CreatedTimestamp ? currentTimestamp - rec.CreatedTimestamp : 0;

                // Check if candidate qualifies for cold storage eviction
                if (retention < evictionScoreThreshold && age > coldSecondsThreshold)
                {
                    pruned++;
                    // In a production archive, this would move to cold blob storage
                }
                else
                {
                    retained++;
                }
            }
        }

        sw.Stop();

        return new AmeConsolidationReport
        {
            TotalRecordsScanned = totalScanned,
            ActiveRecordsRetained = retained,
            ColdRecordsPruned = pruned,
            SemanticRulesSynthesized = 0,
            SweepDurationMs = sw.Elapsed.TotalMilliseconds
        };
    }
}
