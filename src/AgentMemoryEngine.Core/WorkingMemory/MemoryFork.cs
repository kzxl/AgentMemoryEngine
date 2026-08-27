namespace AgentMemoryEngine.Core.WorkingMemory;

/// <summary>
/// A lightweight Copy-on-Write (CoW) speculative branch of Working Memory.
/// Allows the agent to test uncertain hypotheses (Tree of Thoughts) and rollback cleanly on failure.
/// </summary>
public sealed class MemoryFork : WorkingFrame
{
    public WorkingFrame ParentFrame { get; }
    private readonly HashSet<string> _forkTouchedFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _forkDiffs = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _forkEvidence = [];
    private readonly HashSet<uint> _forkSymbols = [];

    public new IReadOnlyCollection<string> TouchedFiles => 
        Status == AmeFrameStatus.RolledBack 
            ? ParentFrame.TouchedFiles 
            : ParentFrame.TouchedFiles.Concat(_forkTouchedFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public new IReadOnlyCollection<uint> ActiveSymbolIds =>
        Status == AmeFrameStatus.RolledBack
            ? ParentFrame.ActiveSymbolIds
            : ParentFrame.ActiveSymbolIds.Concat(_forkSymbols).Distinct().ToArray();

    public new IReadOnlyList<string> DiagnosticEvidence =>
        Status == AmeFrameStatus.RolledBack
            ? ParentFrame.DiagnosticEvidence
            : ParentFrame.DiagnosticEvidence.Concat(_forkEvidence).ToArray();

    internal MemoryFork(WorkingFrame parent, string speculativeHypothesis)
        : base(parent.TaskGoal)
    {
        ParentFrame = parent;
        ActiveHypothesis = speculativeHypothesis;
    }

    public new void TouchFile(string filePath, string? diffSnippet = null)
    {
        _forkTouchedFiles.Add(filePath);
        if (diffSnippet != null)
        {
            _forkDiffs[filePath] = diffSnippet;
        }
    }

    public new void AddEvidence(string evidence)
    {
        _forkEvidence.Add(evidence);
    }

    public new void AddActiveSymbol(uint symbolId)
    {
        _forkSymbols.Add(symbolId);
    }

    /// <summary>
    /// Merges speculative changes into the parent Working Memory frame upon hypothesis success.
    /// </summary>
    public void Merge()
    {
        if (Status != AmeFrameStatus.Active)
            throw new InvalidOperationException($"Cannot merge a fork in {Status} status.");

        ParentFrame.ApplyDelta(_forkTouchedFiles, _forkDiffs, _forkEvidence, _forkSymbols, ActiveHypothesis);
        Status = AmeFrameStatus.Committed;
    }

    /// <summary>
    /// Discards all speculative changes and resets state, leaving parent frame pristine.
    /// </summary>
    public void Rollback()
    {
        if (Status != AmeFrameStatus.Active)
            throw new InvalidOperationException($"Cannot rollback a fork in {Status} status.");

        _forkTouchedFiles.Clear();
        _forkDiffs.Clear();
        _forkEvidence.Clear();
        _forkSymbols.Clear();
        Status = AmeFrameStatus.RolledBack;
    }
}
