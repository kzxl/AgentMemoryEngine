using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Governance;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class ConsolidationWorkerTests : IDisposable
{
    private readonly string _tempDbPath;

    public ConsolidationWorkerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ame_consolidation_{Guid.NewGuid():N}.ame");
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void ConsolidationSweep_ScansAndEvaluatesRetention()
    {
        using (var container = AmeContainer.Create(_tempDbPath, dimension: 384))
        {
            float[] vec = new float[384];
            vec[0] = 1.0f;
            Quantizer.Normalize(vec);

            // Add 1 Semantic record (never decays)
            container.AppendRecord(AmeMemoryTier.Semantic, "Coding standard invariant", vec, importance: 90, confidence: 100);

            // Add 1 fresh Episodic record
            container.AppendRecord(AmeMemoryTier.Episodic, "Fresh bug fix", vec, importance: 80, confidence: 100);

            var worker = new ConsolidationWorker(container);
            var report = worker.ExecuteSweep(evictionScoreThreshold: 0.15f, coldAgeDaysThreshold: 30);

            Assert.Equal(2u, report.TotalRecordsScanned);
            Assert.Equal(2u, report.ActiveRecordsRetained);
            Assert.Equal(0u, report.ColdRecordsPruned);
            Assert.True(report.SweepDurationMs >= 0.0);
        }
    }
}
