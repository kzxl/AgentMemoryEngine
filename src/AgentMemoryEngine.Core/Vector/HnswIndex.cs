using System.Collections.Concurrent;

namespace AgentMemoryEngine.Core.Vector;

/// <summary>
/// Hierarchical Navigable Small World (HNSW) Vector Graph Index.
/// Enables O(log N) approximate nearest neighbor retrieval for large-scale memory repositories.
/// </summary>
public sealed class HnswIndex
{
    private readonly int _dimension;
    private readonly int _m;
    private readonly int _m0;
    private readonly int _efConstruction;
    private readonly double _mL;
    private readonly Random _rng = new(42);

    private readonly List<float[]> _vectors = [];
    private readonly List<Dictionary<int, List<int>>> _layers = []; // Layer -> NodeIndex -> Neighbors
    private int _entryPoint = -1;
    private int _maxLevel = -1;
    private readonly object _graphLock = new();

    public int Count => _vectors.Count;
    public int MaxLevel => _maxLevel;

    public HnswIndex(int dimension = 384, int m = 16, int efConstruction = 64)
    {
        _dimension = dimension;
        _m = m;
        _m0 = 2 * m;
        _efConstruction = efConstruction;
        _mL = 1.0 / Math.Log(m);
    }

    private int GetRandomLevel()
    {
        double r = _rng.NextDouble();
        if (r == 0.0) r = 0.0001;
        int level = (int)Math.Floor(-Math.Log(r) * _mL);
        return Math.Min(level, 16);
    }

    /// <summary>
    /// Inserts a vector into the multi-layer HNSW graph.
    /// </summary>
    public void Insert(uint memoryId, ReadOnlySpan<float> vector)
    {
        lock (_graphLock)
        {
            float[] vecCopy = vector.ToArray();
            Quantizer.Normalize(vecCopy);
            int newNodeIndex = _vectors.Count;
            _vectors.Add(vecCopy);

            int nodeLevel = GetRandomLevel();

            // Ensure layers capacity
            while (_layers.Count <= nodeLevel)
            {
                _layers.Add(new Dictionary<int, List<int>>());
            }

            if (_entryPoint == -1)
            {
                _entryPoint = newNodeIndex;
                _maxLevel = nodeLevel;
                for (int l = 0; l <= nodeLevel; l++)
                {
                    _layers[l][newNodeIndex] = new List<int>();
                }
                return;
            }

            int currObj = _entryPoint;
            float currDist = 1.0f - SimdVectorEngine.DotProduct(vecCopy, _vectors[currObj]);

            // 1. Greedily traverse from top level down to nodeLevel + 1
            for (int l = _maxLevel; l > nodeLevel; l--)
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    if (_layers[l].TryGetValue(currObj, out var neighbors))
                    {
                        for (int i = 0; i < neighbors.Count; i++)
                        {
                            int neighbor = neighbors[i];
                            float dist = 1.0f - SimdVectorEngine.DotProduct(vecCopy, _vectors[neighbor]);
                            if (dist < currDist)
                            {
                                currDist = dist;
                                currObj = neighbor;
                                changed = true;
                            }
                        }
                    }
                }
            }

            // 2. Search and connect neighbors from min(maxLevel, nodeLevel) down to level 0
            var enterPoints = new List<int> { currObj };
            for (int l = Math.Min(_maxLevel, nodeLevel); l >= 0; l--)
            {
                var candidates = SearchLayer(vecCopy, enterPoints, _efConstruction, l);
                int maxNeighbors = l == 0 ? _m0 : _m;

                // Select neighbors
                var selectedNeighbors = SelectNeighbors(candidates, maxNeighbors);
                _layers[l][newNodeIndex] = new List<int>(selectedNeighbors);

                // Add bidirectional edges
                foreach (int neighbor in selectedNeighbors)
                {
                    if (!_layers[l].ContainsKey(neighbor))
                    {
                        _layers[l][neighbor] = new List<int>();
                    }

                    var neighborList = _layers[l][neighbor];
                    neighborList.Add(newNodeIndex);

                    // Shrink if capacity exceeded
                    if (neighborList.Count > maxNeighbors)
                    {
                        ShrinkNeighbors(neighbor, vecCopy, maxNeighbors, l);
                    }
                }

                enterPoints = selectedNeighbors;
            }

            if (nodeLevel > _maxLevel)
            {
                _maxLevel = nodeLevel;
                _entryPoint = newNodeIndex;
            }
        }
    }

    /// <summary>
    /// Searches for Top-K approximate nearest neighbors.
    /// </summary>
    public IReadOnlyList<(int NodeIndex, float Similarity)> SearchKnn(ReadOnlySpan<float> query, int topK, int ef = 32)
    {
        if (_entryPoint == -1 || _vectors.Count == 0)
            return Array.Empty<(int, float)>();

        float[] normQuery = query.ToArray();
        Quantizer.Normalize(normQuery);

        int currObj = _entryPoint;
        float currDist = 1.0f - SimdVectorEngine.DotProduct(normQuery, _vectors[currObj]);

        // Greedily traverse down to level 0
        for (int l = _maxLevel; l > 0; l--)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                if (_layers[l].TryGetValue(currObj, out var neighbors))
                {
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        int neighbor = neighbors[i];
                        float dist = 1.0f - SimdVectorEngine.DotProduct(normQuery, _vectors[neighbor]);
                        if (dist < currDist)
                        {
                            currDist = dist;
                            currObj = neighbor;
                            changed = true;
                        }
                    }
                }
            }
        }

        // Search layer 0 with beam size ef
        var candidates = SearchLayer(normQuery, new List<int> { currObj }, Math.Max(ef, topK), 0);

        var results = new List<(int NodeIndex, float Similarity)>();
        foreach (var (dist, idx) in candidates.Take(topK))
        {
            results.Add((idx, Math.Clamp(1.0f - dist, -1.0f, 1.0f)));
        }

        return results;
    }

    private List<(float Distance, int Index)> SearchLayer(float[] query, List<int> enterPoints, int ef, int level)
    {
        var visited = new HashSet<int>();
        var candidates = new PriorityQueue<int, float>(); // Min-heap by distance
        var w = new PriorityQueue<int, float>(); // Max-heap (simulated with negative distance)
        var wList = new List<(float Distance, int Index)>();

        foreach (int ep in enterPoints)
        {
            float dist = 1.0f - SimdVectorEngine.DotProduct(query, _vectors[ep]);
            visited.Add(ep);
            candidates.Enqueue(ep, dist);
            wList.Add((dist, ep));
        }

        while (candidates.Count > 0)
        {
            candidates.TryPeek(out int c, out float cDist);
            candidates.Dequeue();

            float furthestDist = wList.Count > 0 ? wList.Max(x => x.Distance) : float.MaxValue;
            if (cDist > furthestDist && wList.Count >= ef)
                break;

            if (_layers[level].TryGetValue(c, out var neighbors))
            {
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int e = neighbors[i];
                    if (visited.Add(e))
                    {
                        float eDist = 1.0f - SimdVectorEngine.DotProduct(query, _vectors[e]);
                        furthestDist = wList.Count > 0 ? wList.Max(x => x.Distance) : float.MaxValue;

                        if (eDist < furthestDist || wList.Count < ef)
                        {
                            candidates.Enqueue(e, eDist);
                            wList.Add((eDist, e));

                            if (wList.Count > ef)
                            {
                                var maxElem = wList.OrderByDescending(x => x.Distance).First();
                                wList.Remove(maxElem);
                            }
                        }
                    }
                }
            }
        }

        wList.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        return wList;
    }

    private static List<int> SelectNeighbors(List<(float Distance, int Index)> candidates, int maxNeighbors)
    {
        var selected = new List<int>();
        for (int i = 0; i < Math.Min(candidates.Count, maxNeighbors); i++)
        {
            selected.Add(candidates[i].Index);
        }
        return selected;
    }

    private void ShrinkNeighbors(int nodeIndex, float[] vec, int maxNeighbors, int level)
    {
        var list = _layers[level][nodeIndex];
        var sorted = list.Select(idx => (Distance: 1.0f - SimdVectorEngine.DotProduct(_vectors[nodeIndex], _vectors[idx]), Index: idx))
                         .OrderBy(x => x.Distance)
                         .Take(maxNeighbors)
                         .Select(x => x.Index)
                         .ToList();
        _layers[level][nodeIndex] = sorted;
    }
}
