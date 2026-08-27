using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AgentMemoryEngine.Core.BinaryLayout;
using AgentMemoryEngine.Core.Payload;
using AgentMemoryEngine.Core.Scoring;
using AgentMemoryEngine.Core.Vector;

namespace AgentMemoryEngine.Core.Storage;

/// <summary>
/// Result item returned by Fused Search.
/// </summary>
public record AmeSearchResult
{
    public uint MemoryId { get; init; }
    public AmeMemoryTier Tier { get; init; }
    public float CompositeScore { get; init; }
    public float VectorSimilarity { get; init; }
    public float RecencyRetention { get; init; }
    public byte Importance { get; init; }
    public byte Confidence { get; init; }
    public uint AccessFrequency { get; init; }
    public string Payload { get; init; } = string.Empty;
}

/// <summary>
/// Internal lightweight candidate for Top-K min-heap before payload hydration.
/// </summary>
internal readonly record struct AmeCandidate(
    uint MemoryIndex,
    uint MemoryId,
    byte Tier,
    float CompositeScore,
    float VectorSimilarity,
    float RecencyRetention,
    byte Importance,
    byte Confidence,
    uint AccessFrequency,
    uint PayloadRef
);

/// <summary>
/// Single-file binary container manager for Agent Memory Engine (.ame).
/// Manages memory-mapped I/O, quantized vector scanning, and in-place atomic mutations.
/// </summary>
public sealed unsafe class AmeContainer : IDisposable
{
    private readonly string _filePath;
    private FileStream? _fileStream;
    private MemoryMappedFile? _mmapFile;
    private MemoryMappedViewAccessor? _accessor;
    private byte* _basePointer;
    private readonly object _writeLock = new();
    private bool _disposed;

    // Fast in-memory cached state
    private AmeGlobalHeader _header;
    private readonly List<AmeSegmentDescriptor> _segments = [];
    private AmeVectorHeader _vectorHeader;
    
    /// <summary>
    /// In-memory Compressed Sparse Row (CSR) relationship graph.
    /// </summary>
    public AgentMemoryEngine.Core.Graph.CsrGraph Graph { get; } = new();

    /// <summary>
    /// Adds a relationship edge between two nodes in the graph.
    /// </summary>
    public void AddRelationship(uint sourceNodeId, uint targetNodeId, AmeEdgeType edgeType, byte weight = 100)
    {
        Graph.AddEdge(sourceNodeId, targetNodeId, edgeType, weight);
    }
    
    // Internal offsets
    private long _cognitiveArrayOffset;
    private long _vectorIndexOffset;
    private long _payloadSegmentOffset;

    public string FilePath => _filePath;
    public uint RecordCount => _segments.Count > 0 ? _segments[0].ItemCount : 0;
    public ushort Dimension => _vectorHeader.Dimension;

    private AmeContainer(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Creates and initializes a new .ame single-file container.
    /// </summary>
    public static AmeContainer Create(
        string filePath,
        ushort dimension = 384,
        AmeQuantizationType quantization = AmeQuantizationType.Int8SQ8)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Initialize empty file with default segment capacity (1MB initial size)
        long initialCapacity = 1024 * 1024; // 1MB

        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            fs.SetLength(initialCapacity);

            // 1. Write Global Header (64 bytes)
            var header = new AmeGlobalHeader
            {
                FormatVersion = AmeConstants.CurrentVersion,
                Flags = 0,
                FileSize = (ulong)initialCapacity,
                SegmentTableOffset = (ulong)AmeConstants.GlobalHeaderSize,
                SegmentCount = 3, // Cognitive, Vector, Payload
                HeaderChecksum = 0,
                DataChecksum = 0
            };

            AmeConstants.MagicBytes.CopyTo(new Span<byte>(header.Magic, 8));

            fs.Seek(0, SeekOrigin.Begin);
            fs.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref header, 1)));

            // 2. Write Segment Table (3 Descriptors = 96 bytes)
            long segmentTableOffset = AmeConstants.GlobalHeaderSize;
            long cognitiveOffset = 512; // 64-byte aligned
            long vectorOffset = 131072; // 128KB
            long payloadOffset = 524288; // 512KB

            var segCognitive = new AmeSegmentDescriptor
            {
                SegmentType = (ushort)AmeSegmentType.CognitiveArray,
                Flags = 0,
                ItemCount = 0,
                ItemSize = (uint)AmeConstants.CognitiveRecordSize,
                ByteOffset = (ulong)cognitiveOffset,
                ByteLength = 0,
                Reserved = 0
            };

            var segVector = new AmeSegmentDescriptor
            {
                SegmentType = (ushort)AmeSegmentType.VectorIndex,
                Flags = 0,
                ItemCount = 0,
                ItemSize = (uint)(quantization == AmeQuantizationType.Int8SQ8 ? dimension + 8 : dimension * 4),
                ByteOffset = (ulong)vectorOffset,
                ByteLength = (ulong)sizeof(AmeVectorHeader),
                Reserved = 0
            };

            var segPayload = new AmeSegmentDescriptor
            {
                SegmentType = (ushort)AmeSegmentType.PayloadChunk,
                Flags = 0,
                ItemCount = 0,
                ItemSize = 0, // Variable
                ByteOffset = (ulong)payloadOffset,
                ByteLength = 0,
                Reserved = 0
            };

            fs.Seek(segmentTableOffset, SeekOrigin.Begin);
            fs.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref segCognitive, 1)));
            fs.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref segVector, 1)));
            fs.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref segPayload, 1)));

            // 3. Write Vector Header
            var vHeader = new AmeVectorHeader
            {
                Dimension = dimension,
                QuantizationType = (byte)quantization,
                DistanceMetric = (byte)AmeDistanceMetric.Cosine,
                VectorCount = 0,
                Sq8Scale = 1.0f,
                Sq8Offset = 0.0f
            };

            fs.Seek(vectorOffset, SeekOrigin.Begin);
            fs.Write(MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref vHeader, 1)));
            fs.Flush();
        }

        return Open(filePath);
    }

    /// <summary>
    /// Opens an existing .ame binary container with memory-mapping.
    /// </summary>
    public static AmeContainer Open(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"AME container file not found: {filePath}");

        var container = new AmeContainer(filePath);
        container.MapContainer();
        return container;
    }

    private void MapContainer()
    {
        _fileStream = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        _mmapFile = MemoryMappedFile.CreateFromFile(
            _fileStream,
            null,
            0,
            MemoryMappedFileAccess.ReadWrite,
            HandleInheritability.None,
            leaveOpen: false);

        _accessor = _mmapFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
        _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);

        // Read and validate Global Header
        _header = *(AmeGlobalHeader*)_basePointer;
        
        fixed (byte* magicPtr = _header.Magic)
        {
            var magicSpan = new ReadOnlySpan<byte>(magicPtr, 8);
            if (!magicSpan.SequenceEqual(AmeConstants.MagicBytes))
            {
                throw new InvalidDataException("Invalid AME file: Magic bytes mismatch.");
            }
        }

        // Read Segment Descriptors
        _segments.Clear();
        var descriptorPtr = (AmeSegmentDescriptor*)(_basePointer + _header.SegmentTableOffset);
        for (int i = 0; i < _header.SegmentCount; i++)
        {
            _segments.Add(descriptorPtr[i]);
        }

        _cognitiveArrayOffset = (long)_segments[0].ByteOffset;
        _vectorIndexOffset = (long)_segments[1].ByteOffset;
        _payloadSegmentOffset = (long)_segments[2].ByteOffset;

        // Read Vector Header
        _vectorHeader = *(AmeVectorHeader*)(_basePointer + _vectorIndexOffset);
    }

    /// <summary>
    /// Appends a new memory record and stores its vector and compressed payload.
    /// </summary>
    public uint AppendRecord(
        AmeMemoryTier tier,
        string payload,
        ReadOnlySpan<float> embedding,
        byte importance = 50,
        byte confidence = 100,
        byte decayRate = 128)
    {
        lock (_writeLock)
        {
            uint newMemoryId = _segments[0].ItemCount + 1;
            uint currentTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // 1. Compress payload
            byte[] compressedPayload = PayloadManager.CompressString(payload, out var payloadHeader);
            uint payloadOffsetInSegment = (uint)_segments[2].ByteLength;

            // Write Payload Header + Data
            byte* payloadWritePtr = _basePointer + _payloadSegmentOffset + payloadOffsetInSegment;
            *(AmePayloadHeader*)payloadWritePtr = payloadHeader;
            new ReadOnlySpan<byte>(compressedPayload).CopyTo(
                new Span<byte>(payloadWritePtr + sizeof(AmePayloadHeader), compressedPayload.Length));

            _segments[2] = _segments[2] with
            {
                ItemCount = _segments[2].ItemCount + 1,
                ByteLength = _segments[2].ByteLength + (ulong)(sizeof(AmePayloadHeader) + compressedPayload.Length)
            };

            // 2. Quantize and write Vector
            uint vectorIndex = _segments[1].ItemCount;
            long vectorWriteOffset = _vectorIndexOffset + sizeof(AmeVectorHeader) + (vectorIndex * (Dimension + 8));
            byte* vectorWritePtr = _basePointer + vectorWriteOffset;

            Span<sbyte> quantizedSpan = stackalloc sbyte[Dimension];
            Quantizer.QuantizeSQ8(embedding, quantizedSpan, out float scale, out float offset);

            *(float*)vectorWritePtr = scale;
            *(float*)(vectorWritePtr + 4) = offset;
            quantizedSpan.CopyTo(new Span<sbyte>(vectorWritePtr + 8, Dimension));

            _segments[1] = _segments[1] with
            {
                ItemCount = _segments[1].ItemCount + 1,
                ByteLength = _segments[1].ByteLength + (ulong)(Dimension + 8)
            };

            _vectorHeader = _vectorHeader with { VectorCount = _vectorHeader.VectorCount + 1 };
            *(AmeVectorHeader*)(_basePointer + _vectorIndexOffset) = _vectorHeader;

            // 3. Write Cognitive Record (Fixed 32 bytes)
            var record = new AmeCognitiveRecord
            {
                MemoryId = newMemoryId,
                Tier = (byte)tier,
                Importance = importance,
                Confidence = confidence,
                DecayRate = (byte)(tier == AmeMemoryTier.Semantic ? 0 : decayRate),
                CreatedTimestamp = currentTimestamp,
                LastAccessedTimestamp = currentTimestamp,
                AccessFrequency = 1,
                VectorIndexRef = vectorIndex,
                PayloadRef = payloadOffsetInSegment,
                Reserved = 0
            };

            var recordPtr = (AmeCognitiveRecord*)(_basePointer + _cognitiveArrayOffset + ((newMemoryId - 1) * sizeof(AmeCognitiveRecord)));
            *recordPtr = record;

            _segments[0] = _segments[0] with
            {
                ItemCount = _segments[0].ItemCount + 1,
                ByteLength = _segments[0].ByteLength + (ulong)sizeof(AmeCognitiveRecord)
            };

            // Persist updated Segment Descriptors
            var descriptorPtr = (AmeSegmentDescriptor*)(_basePointer + _header.SegmentTableOffset);
            for (int i = 0; i < _segments.Count; i++)
            {
                descriptorPtr[i] = _segments[i];
            }

            _accessor?.Flush();
            return newMemoryId;
        }
    }

    /// <summary>
    /// In-place atomic update of a cognitive record (Zero re-indexing).
    /// </summary>
    public bool TouchCognitiveInPlace(
        uint memoryId,
        byte? importance = null,
        byte? confidence = null,
        bool incrementAccessCount = true)
    {
        if (memoryId == 0 || memoryId > RecordCount)
            return false;

        var recordPtr = (AmeCognitiveRecord*)(_basePointer + _cognitiveArrayOffset + ((memoryId - 1) * sizeof(AmeCognitiveRecord)));
        uint currentTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Mutate in-place
        recordPtr->LastAccessedTimestamp = currentTimestamp;
        
        if (incrementAccessCount)
        {
            Interlocked.Increment(ref Unsafe.As<uint, int>(ref recordPtr->AccessFrequency));
        }

        if (importance.HasValue)
        {
            recordPtr->Importance = importance.Value;
        }

        if (confidence.HasValue)
        {
            recordPtr->Confidence = confidence.Value;
        }

        return true;
    }

    /// <summary>
    /// Executes a Single-Pass Fused Search across quantized vectors, Ebbinghaus decay, cognitive metrics, and graph proximity.
    /// Defers payload decompression until Top-K selection to achieve sub-millisecond latency.
    /// </summary>
    public IReadOnlyList<AmeSearchResult> QueryFused(
        ReadOnlySpan<float> queryVector,
        uint topK = 5,
        float minScore = 0.0f,
        byte targetTierMask = 0xFF,
        ReadOnlySpan<uint> activeSymbols = default,
        AmeScoringWeights? weights = null)
    {
        uint totalRecords = RecordCount;
        if (totalRecords == 0)
            return Array.Empty<AmeSearchResult>();

        // Normalize query vector
        float[] normalizedQuery = queryVector.ToArray();
        Quantizer.Normalize(normalizedQuery);

        uint currentTimestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var candidateHeap = new PriorityQueue<AmeCandidate, float>();

        var recordsPtr = (AmeCognitiveRecord*)(_basePointer + _cognitiveArrayOffset);
        byte* vectorsBase = _basePointer + _vectorIndexOffset + sizeof(AmeVectorHeader);

        for (uint i = 0; i < totalRecords; i++)
        {
            ref AmeCognitiveRecord rec = ref recordsPtr[i];

            // Tier filtering
            if (((1 << rec.Tier) & targetTierMask) == 0)
                continue;

            // Read vector scale, offset, and quantized values
            byte* vPtr = vectorsBase + (rec.VectorIndexRef * (Dimension + 8));
            float scale = *(float*)vPtr;
            float offset = *(float*)(vPtr + 4);
            var targetSq8 = new ReadOnlySpan<sbyte>(vPtr + 8, Dimension);

            // Compute vector cosine similarity
            float vecSim = SimdVectorEngine.CosineSimilaritySq8(
                normalizedQuery,
                targetSq8,
                scale,
                offset,
                0.0f);

            // Compute Ebbinghaus retention
            float retention = AmeScoringEngine.ComputeRetention(rec, currentTimestamp);

            // Compute graph proximity if active symbols provided
            float graphProx = activeSymbols.IsEmpty ? 0.0f : Graph.ComputeProximity(rec.MemoryId, activeSymbols);

            // Compute composite score in single-pass
            float compositeScore = AmeScoringEngine.ComputeCompositeScore(
                vecSim,
                rec,
                currentTimestamp,
                graphProximity: graphProx,
                weights: weights);

            if (compositeScore < minScore)
                continue;

            var candidate = new AmeCandidate(
                MemoryIndex: i,
                MemoryId: rec.MemoryId,
                Tier: rec.Tier,
                CompositeScore: compositeScore,
                VectorSimilarity: vecSim,
                RecencyRetention: retention,
                Importance: rec.Importance,
                Confidence: rec.Confidence,
                AccessFrequency: rec.AccessFrequency,
                PayloadRef: rec.PayloadRef
            );

            // Maintain Top-K min-heap (without payload decompression overhead)
            if (candidateHeap.Count < topK)
            {
                candidateHeap.Enqueue(candidate, compositeScore);
            }
            else if (compositeScore > candidateHeap.Peek().CompositeScore)
            {
                candidateHeap.Dequeue();
                candidateHeap.Enqueue(candidate, compositeScore);
            }
        }

        // Extract ordered candidates (lowest to highest from min-heap)
        var orderedCandidates = new List<AmeCandidate>(candidateHeap.Count);
        while (candidateHeap.Count > 0)
        {
            orderedCandidates.Add(candidateHeap.Dequeue());
        }
        orderedCandidates.Reverse(); // Now highest score is first

        // Hydrate and decompress payload ONLY for the final Top-K items
        var results = new List<AmeSearchResult>(orderedCandidates.Count);
        foreach (var c in orderedCandidates)
        {
            byte* pPtr = _basePointer + _payloadSegmentOffset + c.PayloadRef;
            var pHeader = *(AmePayloadHeader*)pPtr;
            var compressedData = new ReadOnlySpan<byte>(pPtr + sizeof(AmePayloadHeader), (int)pHeader.CompressedSize);
            string payloadText = PayloadManager.DecompressString(compressedData, pHeader);

            results.Add(new AmeSearchResult
            {
                MemoryId = c.MemoryId,
                Tier = (AmeMemoryTier)c.Tier,
                CompositeScore = c.CompositeScore,
                VectorSimilarity = c.VectorSimilarity,
                RecencyRetention = c.RecencyRetention,
                Importance = c.Importance,
                Confidence = c.Confidence,
                AccessFrequency = c.AccessFrequency,
                Payload = payloadText
            });
        }

        return results;
    }

    /// <summary>
    /// Retrieves and dequantizes the stored vector for a given Memory ID.
    /// </summary>
    public bool TryGetVector(uint memoryId, Span<float> destination)
    {
        if (memoryId == 0 || memoryId > RecordCount || destination.Length < Dimension)
            return false;

        var rec = *((AmeCognitiveRecord*)(_basePointer + _cognitiveArrayOffset + ((memoryId - 1) * sizeof(AmeCognitiveRecord))));
        byte* vPtr = _basePointer + _vectorIndexOffset + sizeof(AmeVectorHeader) + (rec.VectorIndexRef * (Dimension + 8));
        float scale = *(float*)vPtr;
        float offset = *(float*)(vPtr + 4);
        var targetSq8 = new ReadOnlySpan<sbyte>(vPtr + 8, Dimension);

        Quantizer.DequantizeSQ8(targetSq8, destination, scale, offset);
        return true;
    }

    /// <summary>
    /// Retrieves a single record by its Memory ID.
    /// </summary>
    public bool TryGetRecord(uint memoryId, out AmeCognitiveRecord record, out string payload)
    {
        record = default;
        payload = string.Empty;

        if (memoryId == 0 || memoryId > RecordCount)
            return false;

        record = *((AmeCognitiveRecord*)(_basePointer + _cognitiveArrayOffset + ((memoryId - 1) * sizeof(AmeCognitiveRecord))));

        byte* pPtr = _basePointer + _payloadSegmentOffset + record.PayloadRef;
        var pHeader = *(AmePayloadHeader*)pPtr;
        var compressedData = new ReadOnlySpan<byte>(pPtr + sizeof(AmePayloadHeader), (int)pHeader.CompressedSize);
        payload = PayloadManager.DecompressString(compressedData, pHeader);

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_basePointer != null)
        {
            _accessor?.SafeMemoryMappedViewHandle.ReleasePointer();
            _basePointer = null;
        }

        _accessor?.Dispose();
        _mmapFile?.Dispose();
        _fileStream?.Dispose();
    }
}
