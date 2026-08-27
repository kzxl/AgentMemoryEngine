using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;

namespace AgentMemoryEngine.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
        {
            PrintUsage();
            return 0;
        }

        string command = args[0].ToLowerInvariant();

        try
        {
            switch (command)
            {
                case "init":
                    return HandleInit(args);

                case "query":
                    return HandleQuery(args);

                case "post":
                    return HandlePost(args);

                case "touch":
                    return HandleTouch(args);

                case "inspect":
                    return HandleInspect(args);

                case "mcp":
                    return await HandleMcpAsync(args);

                case "ipc":
                    return await HandleIpcAsync(args);

                case "index":
                    return HandleIndex(args);

                case "studio":
                case "ui":
                case "serve":
                    return await HandleStudioAsync(args);

                default:
                    Console.Error.WriteLine($"[Error] Unknown command '{command}'. Use --help for usage.");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Fatal Error] {ex.Message}");
            return 1;
        }
    }

    private static int HandleInit(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("[Usage] ame init <database_path.ame> [--dim 384]");
            return 1;
        }

        string dbPath = args[1];
        ushort dimension = 384;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--dim" && i + 1 < args.Length)
            {
                dimension = ushort.Parse(args[++i]);
            }
        }

        using var container = AmeContainer.Create(dbPath, dimension);
        Console.WriteLine($"[AME] Initialized database container: {dbPath} (Dimension: {dimension}, SQ8 Quantized).");
        return 0;
    }

    private static int HandleQuery(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("[Usage] ame query <database_path.ame> \"<query text>\" [--top 5] [--min-score 0.1]");
            return 1;
        }

        string dbPath = args[1];
        string queryText = args[2];
        uint topK = 5;
        float minScore = 0.1f;

        for (int i = 3; i < args.Length; i++)
        {
            if (args[i] == "--top" && i + 1 < args.Length) topK = uint.Parse(args[++i]);
            if (args[i] == "--min-score" && i + 1 < args.Length) minScore = float.Parse(args[++i]);
        }

        using var container = AmeContainer.Open(dbPath);
        float[] queryVec = McpServer.CreateDeterministicVector(queryText, container.Dimension);

        var results = container.QueryFused(queryVec, topK: topK, minScore: minScore);

        Console.WriteLine($"--- AME Fused Retrieval: Found {results.Count} matches ---");
        foreach (var r in results)
        {
            Console.WriteLine($"[ID: #{r.MemoryId} | Tier: {r.Tier} | Score: {r.CompositeScore:F3} (Sim: {r.VectorSimilarity:F2}, Retention: {r.RecencyRetention:P0}) | Freq: {r.AccessFrequency}]");
            Console.WriteLine($"  ➔ {r.Payload}\n");
        }

        return 0;
    }

    private static int HandlePost(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("[Usage] ame post <database_path.ame> \"[Symptom] | [Cause] | [Fix]\" [--tier Episodic] [--importance 80] [--confidence 100]");
            return 1;
        }

        string dbPath = args[1];
        string payload = args[2];
        AmeMemoryTier tier = AmeMemoryTier.Episodic;
        byte importance = 80;
        byte confidence = 100;

        for (int i = 3; i < args.Length; i++)
        {
            if (args[i] == "--tier" && i + 1 < args.Length) tier = Enum.Parse<AmeMemoryTier>(args[++i], true);
            if (args[i] == "--importance" && i + 1 < args.Length) importance = byte.Parse(args[++i]);
            if (args[i] == "--confidence" && i + 1 < args.Length) confidence = byte.Parse(args[++i]);
        }

        using var container = AmeContainer.Open(dbPath);
        float[] embedding = McpServer.CreateDeterministicVector(payload, container.Dimension);

        uint id = container.AppendRecord(
            tier,
            payload,
            embedding,
            importance: importance,
            confidence: confidence,
            decayRate: (byte)(tier == AmeMemoryTier.Semantic ? 0 : 128));

        Console.WriteLine($"[AME] Successfully harvested memory #{id} into tier [{tier}].");
        return 0;
    }

    private static int HandleTouch(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("[Usage] ame touch <database_path.ame> <memory_id> [--importance <1-100>] [--confidence <0-100>]");
            return 1;
        }

        string dbPath = args[1];
        uint memoryId = uint.Parse(args[2]);
        byte? importance = null;
        byte? confidence = null;

        for (int i = 3; i < args.Length; i++)
        {
            if (args[i] == "--importance" && i + 1 < args.Length) importance = byte.Parse(args[++i]);
            if (args[i] == "--confidence" && i + 1 < args.Length) confidence = byte.Parse(args[++i]);
        }

        using var container = AmeContainer.Open(dbPath);
        bool success = container.TouchCognitiveInPlace(memoryId, importance, confidence, incrementAccessCount: true);

        if (success)
        {
            Console.WriteLine($"[AME] Successfully updated cognitive record #{memoryId} in-place.");
            return 0;
        }

        Console.Error.WriteLine($"[Error] Memory ID #{memoryId} not found.");
        return 1;
    }

    private static int HandleInspect(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("[Usage] ame inspect <database_path.ame>");
            return 1;
        }

        string dbPath = args[1];
        using var container = AmeContainer.Open(dbPath);

        Console.WriteLine("=================================================");
        Console.WriteLine($"  AGENT MEMORY ENGINE (.ame) INSPECTION REPORT");
        Console.WriteLine("=================================================");
        Console.WriteLine($"  File Path:       {Path.GetFullPath(dbPath)}");
        Console.WriteLine($"  Total Records:   {container.RecordCount}");
        Console.WriteLine($"  Vector Dimension:{container.Dimension} (SQ8 Quantized)");
        Console.WriteLine($"  Graph Nodes:     {container.Graph.NodeCount}");
        Console.WriteLine($"  Graph Edges:     {container.Graph.EdgeCount}");
        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine("  Memory Records Summary:");

        for (uint id = 1; id <= container.RecordCount; id++)
        {
            if (container.TryGetRecord(id, out var rec, out var payload))
            {
                Console.WriteLine($"  [#{id:D3} | Tier: {(AmeMemoryTier)rec.Tier,-10} | Imp: {rec.Importance,3} | Conf: {rec.Confidence,3}% | Freq: {rec.AccessFrequency,3}] {payload}");
            }
        }
        Console.WriteLine("=================================================");

        return 0;
    }

    private static async Task<int> HandleMcpAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("[Usage] ame mcp <database_path.ame>");
            return 1;
        }

        string dbPath = args[1];
        using var container = File.Exists(dbPath) ? AmeContainer.Open(dbPath) : AmeContainer.Create(dbPath);

        var mcpServer = new McpServer(container);
        await mcpServer.RunStdioAsync();
        return 0;
    }

    private static async Task<int> HandleStudioAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("[Usage] ame studio <database_path.ame> [--port 8989]");
            return 1;
        }

        string dbPath = args[1];
        int port = 8989;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length)
            {
                port = int.Parse(args[++i]);
            }
        }

        using var container = File.Exists(dbPath) ? AmeContainer.Open(dbPath) : AmeContainer.Create(dbPath);
        var studio = new StudioServer(container, port);
        await studio.StartAsync();
        return 0;
    }

    private static async Task<int> HandleIpcAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("[Usage] ame ipc <database_path.ame> [--pipe ame_pipe]");
            return 1;
        }

        string dbPath = args[1];
        string pipeName = "ame_pipe";

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--pipe" && i + 1 < args.Length)
            {
                pipeName = args[++i];
            }
        }

        using var container = File.Exists(dbPath) ? AmeContainer.Open(dbPath) : AmeContainer.Create(dbPath);
        var ipc = new IpcServer(container, pipeName);
        await ipc.StartAsync();
        return 0;
    }

    private static int HandleIndex(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("[Usage] ame index <database_path.ame> <source_dir_or_file>");
            return 1;
        }

        string dbPath = args[1];
        string targetPath = args[2];

        using var container = File.Exists(dbPath) ? AmeContainer.Open(dbPath) : AmeContainer.Create(dbPath);
        var indexer = new AgentMemoryEngine.Core.Indexer.AstIndexer(container);

        var files = Directory.Exists(targetPath)
            ? Directory.GetFiles(targetPath, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".cs") || f.EndsWith(".ts") || f.EndsWith(".py") || f.EndsWith(".sql") || f.EndsWith(".js"))
                .ToArray()
            : [targetPath];

        int totalSymbols = 0;
        foreach (var file in files)
        {
            try
            {
                string text = File.ReadAllText(file);
                var created = indexer.IndexFile(file, text);
                totalSymbols += created.Count;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Warning] Failed to index {file}: {ex.Message}");
            }
        }

        Console.WriteLine($"[AME AST] Indexed {files.Length} files and extracted {totalSymbols} symbols into Project Memory.");
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"
Agent Memory Engine (AME) CLI - High-Performance Cognitive DB

Usage:
  ame init <database.ame> [--dim 384]
  ame query <database.ame> ""<query text>"" [--top 5] [--min-score 0.1]
  ame post <database.ame> ""<symptom> | <cause> | <fix>"" [--tier Episodic] [--importance 80] [--confidence 100]
  ame touch <database.ame> <memory_id> [--importance 90] [--confidence 100]
  ame index <database.ame> <source_dir_or_file>
  ame inspect <database.ame>
  ame mcp <database.ame>
  ame ipc <database.ame> [--pipe ame_pipe]
  ame studio <database.ame> [--port 8989]
");
    }
}
