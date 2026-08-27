using System.Diagnostics;
using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;

namespace AgentMemoryEngine.Cli;

/// <summary>
/// High-throughput scalability and latency benchmark runner for 100MB+ dataset testing.
/// </summary>
public static class BenchmarkRunner
{
    public static void Run(string dbPath = "benchmark_100mb.ame", int recordCount = 50000, ushort dimension = 384, int queryCount = 100)
    {
        Console.WriteLine("\n==================================================================");
        Console.WriteLine($"  🚀 AME LARGE-SCALE BENCHMARK (Target: ~100MB Dataset)");
        Console.WriteLine($"  Records: {recordCount:N0} | Dimension: {dimension} | Queries: {queryCount}");
        Console.WriteLine("==================================================================\n");

        if (File.Exists(dbPath)) File.Delete(dbPath);

        long targetCapacity = 100L * 1024 * 1024; // 100MB exact

        // Phase 1: Ingestion / Generation Benchmark
        Console.WriteLine($"[Phase 1] Generating and Ingesting {recordCount:N0} cognitive memories into a {targetCapacity / (1024 * 1024)}MB container...");
        var swGen = Stopwatch.StartNew();

        var initialMemory = GC.GetTotalMemory(true);
        var rng = new Random(42);

        using (var container = AmeContainer.Create(dbPath, dimension: dimension, initialCapacity: targetCapacity))
        {
            // Seed base vectors
            float[] tempVec = new float[dimension];

            string[] errorTemplates = [
                "Deadlock on transaction commit in SalesOrderController | Missing index on OrderId | Added IX_Order_Status",
                "GridControl UI freeze on background thread | Sync delegate call | Use RunAfterShown with Task.Run",
                "React state desync on high frequency WebSocket events | Mutated state directly | Use Redux action queue",
                "Memory leak in long running subagent | Unsubscribed event handlers | Converted to WeakReference",
                "Cache stampede on product catalog query | Missing distributed lock | Implemented Redis mutex lock",
                "High latency on foreign key join in InventoryService | Clustered index scan | Rebuilt index with INCLUDE"
            ];

            for (int i = 1; i <= recordCount; i++)
            {
                // Generate pseudo embedding
                for (int d = 0; d < dimension; d++)
                {
                    tempVec[d] = (float)(rng.NextDouble() * 2.0 - 1.0);
                }
                Quantizer.Normalize(tempVec);

                string template = errorTemplates[i % errorTemplates.Length];
                string payload = $"[Memory #{i:D6}] {template} | TraceId: {Guid.NewGuid():N} | Details: Stress test payload padding {i}";

                byte tier = (byte)(i % 5 + 1);
                byte importance = (byte)(rng.Next(50, 100));
                byte confidence = (byte)(rng.Next(80, 100));
                byte decayRate = (byte)(tier == (byte)AmeMemoryTier.Semantic ? 0 : 128);

                container.AppendRecord(
                    (AmeMemoryTier)tier,
                    payload,
                    tempVec,
                    importance: importance,
                    confidence: confidence,
                    decayRate: decayRate
                );

                if (i % 10000 == 0)
                {
                    Console.WriteLine($"  -> Ingested {i:N0} / {recordCount:N0} records ({(double)i / recordCount * 100:F0}%)...");
                }
            }

            swGen.Stop();
            long fileSizeBytes = new FileInfo(dbPath).Length;
            double fileSizeMb = (double)fileSizeBytes / (1024 * 1024);
            double genSpeed = recordCount / swGen.Elapsed.TotalSeconds;

            Console.WriteLine($"\n✅ Ingestion Complete in {swGen.Elapsed.TotalSeconds:F2}s ({genSpeed:N0} records/sec).");
            Console.WriteLine($"📁 File Size on Disk: {fileSizeMb:F2} MB ({fileSizeBytes:N0} bytes)");

            // Phase 2: Query Latency Benchmark
            Console.WriteLine($"\n[Phase 2] Executing {queryCount} Single-Pass Fused Queries across {recordCount:N0} records...");

            float[] queryVec = new float[dimension];
            for (int d = 0; d < dimension; d++) queryVec[d] = (float)(rng.NextDouble() * 2.0 - 1.0);
            Quantizer.Normalize(queryVec);

            // Warm-up query
            for (int w = 0; w < 5; w++)
            {
                container.QueryFused(queryVec, topK: 10, minScore: 0.05f);
            }

            var latencies = new List<double>(queryCount);
            var swTotalQueries = Stopwatch.StartNew();

            for (int q = 0; q < queryCount; q++)
            {
                // Randomize query slightly
                queryVec[q % dimension] += 0.01f;
                Quantizer.Normalize(queryVec);

                var swQ = Stopwatch.StartNew();
                var results = container.QueryFused(queryVec, topK: 10, minScore: 0.05f);
                swQ.Stop();

                latencies.Add(swQ.Elapsed.TotalMilliseconds);
            }

            swTotalQueries.Stop();

            latencies.Sort();
            double avgLatency = latencies.Average();
            double minLatency = latencies.First();
            double maxLatency = latencies.Last();
            double p50Latency = latencies[(int)(queryCount * 0.50)];
            double p95Latency = latencies[(int)(queryCount * 0.95)];
            double p99Latency = latencies[(int)(queryCount * 0.99)];
            double qps = queryCount / swTotalQueries.Elapsed.TotalSeconds;

            long finalMemory = GC.GetTotalMemory(false);
            double memoryUsedMb = (double)(finalMemory - initialMemory) / (1024 * 1024);
            if (memoryUsedMb < 0) memoryUsedMb = 0;

            Console.WriteLine("\n==================================================================");
            Console.WriteLine("  📊 BENCHMARK RESULTS SUMMARY");
            Console.WriteLine("==================================================================");
            Console.WriteLine($"  Dataset Size:            {fileSizeMb:F2} MB ({recordCount:N0} records)");
            Console.WriteLine($"  Throughput:              {qps:F1} Queries / Sec (QPS)");
            Console.WriteLine($"  Average Query Latency:   {avgLatency:F4} ms");
            Console.WriteLine($"  Min Query Latency:       {minLatency:F4} ms");
            Console.WriteLine($"  Median (P50) Latency:    {p50Latency:F4} ms");
            Console.WriteLine($"  95th Percentile (P95):   {p95Latency:F4} ms");
            Console.WriteLine($"  99th Percentile (P99):   {p99Latency:F4} ms");
            Console.WriteLine($"  Managed Heap Overhead:   ~{memoryUsedMb:F2} MB");
            Console.WriteLine("==================================================================\n");
        }
    }
}
