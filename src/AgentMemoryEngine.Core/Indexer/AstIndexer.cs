using System.Text.RegularExpressions;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;

namespace AgentMemoryEngine.Core.Indexer;

/// <summary>
/// Extracted codebase symbol item.
/// </summary>
public record AmeCodeSymbol(
    string Name,
    string Kind, // Class, Interface, Function, Method, Table
    string FilePath,
    int LineNumber,
    string Signature
);

/// <summary>
/// Codebase AST & Symbol Indexer for auto-populating Project Memory.
/// Parses source files and establishes CSR graph relationships between codebase symbols and cognitive memories.
/// </summary>
public sealed class AstIndexer
{
    private readonly AmeContainer _container;

    // Fast multi-language regex parsers
    private static readonly Regex CSharpClassRegex = new(@"\b(?:public|internal|private)?\s*(?:class|interface|record|struct)\s+([A-Za-z0-9_]+)", RegexOptions.Compiled);
    private static readonly Regex CSharpMethodRegex = new(@"\b(?:public|private|protected|internal)?\s*(?:async\s+)?(?:Task<[A-Za-z0-9_<>]+>|Task|void|[A-Za-z0-9_]+)\s+([A-Za-z0-9_]+)\s*\(", RegexOptions.Compiled);
    private static readonly Regex TsFunctionRegex = new(@"\b(?:export\s+)?(?:function|class|interface)\s+([A-Za-z0-9_]+)", RegexOptions.Compiled);
    private static readonly Regex PythonDefRegex = new(@"^\s*(?:def|class)\s+([A-Za-z0-9_]+)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex SqlTableRegex = new(@"CREATE\s+TABLE\s+(?:\[dbo\]\.)?\[?([A-Za-z0-9_]+)\]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AstIndexer(AmeContainer container)
    {
        _container = container;
    }

    /// <summary>
    /// Scans source text content and extracts all code declarations.
    /// </summary>
    public IReadOnlyList<AmeCodeSymbol> ParseContent(string content, string filePath)
    {
        var symbols = new List<AmeCodeSymbol>();
        string ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext == ".cs")
        {
            foreach (Match m in CSharpClassRegex.Matches(content))
            {
                symbols.Add(new AmeCodeSymbol(m.Groups[1].Value, "Class/Type", filePath, 0, m.Value));
            }
            foreach (Match m in CSharpMethodRegex.Matches(content))
            {
                symbols.Add(new AmeCodeSymbol(m.Groups[1].Value, "Method", filePath, 0, m.Value));
            }
        }
        else if (ext == ".ts" || ext == ".js" || ext == ".tsx")
        {
            foreach (Match m in TsFunctionRegex.Matches(content))
            {
                symbols.Add(new AmeCodeSymbol(m.Groups[1].Value, "Function/Class", filePath, 0, m.Value));
            }
        }
        else if (ext == ".py")
        {
            foreach (Match m in PythonDefRegex.Matches(content))
            {
                symbols.Add(new AmeCodeSymbol(m.Groups[1].Value, "Def/Class", filePath, 0, m.Value));
            }
        }
        else if (ext == ".sql")
        {
            foreach (Match m in SqlTableRegex.Matches(content))
            {
                symbols.Add(new AmeCodeSymbol(m.Groups[1].Value, "Table", filePath, 0, m.Value));
            }
        }

        return symbols;
    }

    /// <summary>
    /// Indexes a codebase file, storing symbols into Project Memory and wiring CSR graph links to related episodic memories.
    /// </summary>
    public IReadOnlyList<uint> IndexFile(string filePath, string fileContent)
    {
        var symbols = ParseContent(fileContent, filePath);
        var createdIds = new List<uint>();

        foreach (var sym in symbols)
        {
            string payload = $"[Project Symbol: {sym.Kind}] {sym.Name} in {sym.FilePath} | Sig: {sym.Signature}";
            float[] vec = Quantizer.CreateDeterministicVector(sym.Name, _container.Dimension);

            uint symbolMemId = _container.AppendRecord(
                AmeMemoryTier.Project,
                payload,
                vec,
                importance: 70,
                confidence: 100,
                decayRate: 0 // Project structure is persistent
            );

            createdIds.Add(symbolMemId);

            // Connect to existing Episodic memories if symbol name matches text
            for (uint id = 1; id < symbolMemId; id++)
            {
                if (_container.TryGetRecord(id, out var rec, out var lessonText))
                {
                    if (rec.Tier == (byte)AmeMemoryTier.Episodic && lessonText.Contains(sym.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        _container.AddRelationship(id, symbolMemId, AmeEdgeType.FixesBugIn, weight: 95);
                    }
                }
            }
        }

        return createdIds;
    }
}
