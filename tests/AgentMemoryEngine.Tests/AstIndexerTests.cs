using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Indexer;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class AstIndexerTests : IDisposable
{
    private readonly string _tempDbPath;

    public AstIndexerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"ame_ast_{Guid.NewGuid():N}.ame");
    }

    public void Dispose()
    {
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void AstIndexer_ParsesAndWiresGraphToEpisodicMemory()
    {
        using (var container = AmeContainer.Create(_tempDbPath, dimension: 384))
        {
            // 1. Seed an Episodic lesson mentioning 'SalesController'
            float[] vec = Quantizer.CreateDeterministicVector("SalesController freeze fix", 384);
            uint epId = container.AppendRecord(
                AmeMemoryTier.Episodic,
                "SalesController freeze on WinForms | Invoked sync void | Use Task",
                vec);

            // 2. Index C# source file containing 'SalesController' class and 'LoadData' method
            string csCode = """
            namespace MyShop.Controllers;
            public class SalesController
            {
                public async Task LoadData() { }
            }
            """;

            var indexer = new AstIndexer(container);
            var symbolIds = indexer.IndexFile("src/MyShop/SalesController.cs", csCode);

            Assert.NotEmpty(symbolIds);

            // Verify Project memory records were created
            uint classSymbolId = symbolIds[0];
            bool ok = container.TryGetRecord(classSymbolId, out var classRec, out var classPayload);
            Assert.True(ok);
            Assert.Equal((byte)AmeMemoryTier.Project, classRec.Tier);
            Assert.Contains("SalesController", classPayload);

            // Verify CSR Graph Edge from Episodic lesson #1 -> Project Symbol #2
            var neighbors = container.Graph.GetNeighbors(epId);
            Assert.NotEmpty(neighbors.Targets.ToArray());
            Assert.Contains(classSymbolId, neighbors.Targets.ToArray());
        }
    }
}
