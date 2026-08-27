using System.Diagnostics;
using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;
using Xunit;
using Xunit.Abstractions;

namespace AgentMemoryEngine.Tests;

public class BenchmarkTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempBenchmarkFile;

    public BenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
        _tempBenchmarkFile = Path.Combine(Path.GetTempPath(), $"ame_bench_{Guid.NewGuid():N}.ame");
    }

    public void Dispose()
    {
        if (File.Exists(_tempBenchmarkFile))
        {
            try { File.Delete(_tempBenchmarkFile); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void FusedQuery_Over1000Memories_ExecutesInSubMillisecond()
    {
        const int dimension = 384;
        const int recordCount = 1000;
        var rng = new Random(42);

        using (var container = AmeContainer.Create(_tempBenchmarkFile, dimension))
        {
            // Seed 1,000 records
            float[] sampleVec = new float[dimension];
            for (int i = 0; i < recordCount; i++)
            {
                for (int d = 0; d < dimension; d++)
                {
                    sampleVec[d] = (float)(rng.NextDouble() * 2.0 - 1.0);
                }
                Quantizer.Normalize(sampleVec);

                container.AppendRecord(
                    tier: (AmeMemoryTier)((i % 6) + 1),
                    payload: $"Lesson #{i}: Debugging memory leak in subsystem {i % 10} | Cause: closure | Fix: weak ref",
                    embedding: sampleVec,
                    importance: (byte)(rng.Next(1, 100)),
                    confidence: 100,
                    decayRate: (byte)(i % 2 == 0 ? 128 : 0));
            }

            Assert.Equal((uint)recordCount, container.RecordCount);

            // Prepare random query vector
            float[] queryVec = new float[dimension];
            for (int d = 0; d < dimension; d++)
            {
                queryVec[d] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }
            Quantizer.Normalize(queryVec);

            // Warm-up query
            container.QueryFused(queryVec, topK: 5);

            // Benchmark 500 queries
            const int queryIterations = 500;
            var sw = Stopwatch.StartNew();

            for (int q = 0; q < queryIterations; q++)
            {
                var results = container.QueryFused(queryVec, topK: 5, minScore: 0.1f);
                Assert.NotEmpty(results);
            }

            sw.Stop();
            double avgLatencyMs = sw.Elapsed.TotalMilliseconds / queryIterations;
            _output.WriteLine($"[Benchmark] Average Fused Query Latency across {recordCount} records: {avgLatencyMs:F4} ms");

            // Sub-millisecond target check: must be < 1.5ms
            Assert.True(avgLatencyMs < 1.5, $"Query latency was {avgLatencyMs:F4} ms, expected < 1.5 ms");
        }
    }
}
