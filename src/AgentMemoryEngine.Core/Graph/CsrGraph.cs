using System.Runtime.InteropServices;

namespace AgentMemoryEngine.Core.Graph;

/// <summary>
/// Packed 4-byte metadata for each edge in the CSR Graph.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
public struct AmeGraphEdgeMeta
{
    public byte EdgeType;       // AmeEdgeType enum
    public byte Weight;         // Relationship strength: 1..100
    public ushort Reserved;     // Alignment padding
}

/// <summary>
/// Ref struct representing a zero-copy slice of node neighbors and edge metadata.
/// </summary>
public readonly ref struct AmeNeighborSlice
{
    public ReadOnlySpan<uint> Targets { get; }
    public ReadOnlySpan<AmeGraphEdgeMeta> Meta { get; }
    public int Length => Targets.Length;
    public bool IsEmpty => Targets.IsEmpty;

    public AmeNeighborSlice(ReadOnlySpan<uint> targets, ReadOnlySpan<AmeGraphEdgeMeta> meta)
    {
        Targets = targets;
        Meta = meta;
    }

    public static AmeNeighborSlice Empty => new(ReadOnlySpan<uint>.Empty, ReadOnlySpan<AmeGraphEdgeMeta>.Empty);
}

/// <summary>
/// High-performance Compressed Sparse Row (CSR) Relationship Graph.
/// Provides O(1) neighbor slicing and multi-hop proximity traversal for Fused Retrieval.
/// </summary>
public sealed class CsrGraph
{
    private readonly List<List<(uint targetId, AmeGraphEdgeMeta meta)>> _dynamicAdjacency = [];
    private uint[] _rowPtr = [0];
    private uint[] _colInd = [];
    private AmeGraphEdgeMeta[] _edgeMeta = [];
    private bool _isDirty = false;

    public uint NodeCount => (uint)_dynamicAdjacency.Count;
    public uint EdgeCount => (uint)_colInd.Length;

    public CsrGraph(uint initialCapacity = 16)
    {
        for (int i = 0; i < initialCapacity; i++)
        {
            _dynamicAdjacency.Add([]);
        }
    }

    /// <summary>
    /// Adds a directed relationship edge between source and target memory/symbol nodes.
    /// </summary>
    public void AddEdge(uint sourceNodeId, uint targetNodeId, AmeEdgeType edgeType, byte weight = 100)
    {
        EnsureCapacity(Math.Max(sourceNodeId, targetNodeId));

        var meta = new AmeGraphEdgeMeta
        {
            EdgeType = (byte)edgeType,
            Weight = weight,
            Reserved = 0
        };

        _dynamicAdjacency[(int)sourceNodeId - 1].Add((targetNodeId, meta));
        _isDirty = true;
    }

    private void EnsureCapacity(uint maxNodeId)
    {
        while (_dynamicAdjacency.Count < maxNodeId)
        {
            _dynamicAdjacency.Add([]);
            _isDirty = true;
        }
    }

    /// <summary>
    /// Builds and compacts the dynamic adjacency list into flat CSR arrays.
    /// </summary>
    public void RebuildCsr()
    {
        if (!_isDirty && _colInd.Length > 0) return;

        int totalNodes = _dynamicAdjacency.Count;
        int totalEdges = 0;
        for (int i = 0; i < totalNodes; i++)
        {
            totalEdges += _dynamicAdjacency[i].Count;
        }

        _rowPtr = new uint[totalNodes + 1];
        _colInd = new uint[totalEdges];
        _edgeMeta = new AmeGraphEdgeMeta[totalEdges];

        uint currentEdgeIndex = 0;
        _rowPtr[0] = 0;

        for (int i = 0; i < totalNodes; i++)
        {
            var edges = _dynamicAdjacency[i];
            for (int e = 0; e < edges.Count; e++)
            {
                _colInd[currentEdgeIndex] = edges[e].targetId;
                _edgeMeta[currentEdgeIndex] = edges[e].meta;
                currentEdgeIndex++;
            }
            _rowPtr[i + 1] = currentEdgeIndex;
        }

        _isDirty = false;
    }

    /// <summary>
    /// Returns the target node IDs and edge metadata connected to the given node in O(1) time.
    /// </summary>
    public AmeNeighborSlice GetNeighbors(uint nodeId)
    {
        if (nodeId == 0 || nodeId > NodeCount)
        {
            return AmeNeighborSlice.Empty;
        }

        RebuildCsr();

        uint start = _rowPtr[nodeId - 1];
        uint end = _rowPtr[nodeId];
        int count = (int)(end - start);

        if (count == 0)
        {
            return AmeNeighborSlice.Empty;
        }

        var targets = new ReadOnlySpan<uint>(_colInd, (int)start, count);
        var meta = new ReadOnlySpan<AmeGraphEdgeMeta>(_edgeMeta, (int)start, count);
        return new AmeNeighborSlice(targets, meta);
    }

    /// <summary>
    /// Calculates graph proximity between a candidate node and a set of active Working Memory symbols (1-hop & 2-hop).
    /// </summary>
    public float ComputeProximity(
        uint candidateNodeId,
        ReadOnlySpan<uint> activeWorkingSymbols,
        float hopAttenuation = 0.7f,
        int maxHops = 2)
    {
        if (activeWorkingSymbols.IsEmpty || candidateNodeId == 0 || candidateNodeId > NodeCount)
        {
            return 0.0f;
        }

        RebuildCsr();
        float maxProximity = 0.0f;

        // Check 1-hop direct neighbors
        var hop1 = GetNeighbors(candidateNodeId);
        for (int i = 0; i < hop1.Length; i++)
        {
            uint targetId = hop1.Targets[i];
            float edgeWeightNorm = hop1.Meta[i].Weight / 100.0f;

            for (int s = 0; s < activeWorkingSymbols.Length; s++)
            {
                if (targetId == activeWorkingSymbols[s])
                {
                    float score = edgeWeightNorm * 1.0f;
                    if (score > maxProximity) maxProximity = score;
                }
            }

            // Check 2-hop neighbors if maxHops >= 2
            if (maxHops >= 2)
            {
                var hop2 = GetNeighbors(targetId);
                for (int j = 0; j < hop2.Length; j++)
                {
                    uint hop2TargetId = hop2.Targets[j];
                    float hop2WeightNorm = (hop2.Meta[j].Weight / 100.0f) * edgeWeightNorm * hopAttenuation;

                    for (int s = 0; s < activeWorkingSymbols.Length; s++)
                    {
                        if (hop2TargetId == activeWorkingSymbols[s])
                        {
                            if (hop2WeightNorm > maxProximity) maxProximity = hop2WeightNorm;
                        }
                    }
                }
            }
        }

        return Math.Clamp(maxProximity, 0.0f, 1.0f);
    }
}
