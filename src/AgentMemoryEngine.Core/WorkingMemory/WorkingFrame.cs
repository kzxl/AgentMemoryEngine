namespace AgentMemoryEngine.Core.WorkingMemory;

/// <summary>
/// Status of a Working Memory Frame or speculative Fork.
/// </summary>
public enum AmeFrameStatus
{
    Active,
    Committed,
    RolledBack
}

/// <summary>
/// Execution state scratchpad for an active agent task.
/// Tracks goal, touched diffs, active hypotheses, and symbol anchors for Graph Proximity.
/// </summary>
public class WorkingFrame
{
    public string FrameId { get; }
    public string TaskGoal { get; set; }
    public string? ActiveHypothesis { get; set; }
    public AmeFrameStatus Status { get; protected set; } = AmeFrameStatus.Active;

    private readonly HashSet<string> _touchedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _fileDiffs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _diagnosticEvidence = [];
    private readonly HashSet<uint> _activeSymbolIds = [];

    public IReadOnlyCollection<string> TouchedFiles => _touchedFiles;
    public IReadOnlyDictionary<string, string> FileDiffs => _fileDiffs;
    public IReadOnlyList<string> DiagnosticEvidence => _diagnosticEvidence;
    public IReadOnlyCollection<uint> ActiveSymbolIds => _activeSymbolIds;

    public WorkingFrame(string taskGoal, string? frameId = null)
    {
        TaskGoal = taskGoal;
        FrameId = frameId ?? Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Records a modified or touched file.
    /// </summary>
    public void TouchFile(string filePath, string? diffSnippet = null)
    {
        _touchedFiles.Add(filePath);
        if (diffSnippet != null)
        {
            _fileDiffs[filePath] = diffSnippet;
        }
    }

    /// <summary>
    /// Records verified diagnostic evidence (compiler output, test results, error logs).
    /// </summary>
    public void AddEvidence(string evidence)
    {
        _diagnosticEvidence.Add(evidence);
    }

    /// <summary>
    /// Attaches an active project symbol ID for Graph Proximity calculation.
    /// </summary>
    public void AddActiveSymbol(uint symbolId)
    {
        _activeSymbolIds.Add(symbolId);
    }

    /// <summary>
    /// Creates a Copy-on-Write (CoW) speculative branch for hypothesis exploration.
    /// </summary>
    public MemoryFork Fork(string speculativeHypothesis)
    {
        return new MemoryFork(this, speculativeHypothesis);
    }

    internal void ApplyDelta(
        IEnumerable<string> touchedFiles,
        IDictionary<string, string> diffs,
        IEnumerable<string> evidence,
        IEnumerable<uint> symbols,
        string? mergedHypothesis)
    {
        foreach (var f in touchedFiles) _touchedFiles.Add(f);
        foreach (var kvp in diffs) _fileDiffs[kvp.Key] = kvp.Value;
        foreach (var ev in evidence) _diagnosticEvidence.Add(ev);
        foreach (var s in symbols) _activeSymbolIds.Add(s);
        if (mergedHypothesis != null) ActiveHypothesis = mergedHypothesis;
    }
}
