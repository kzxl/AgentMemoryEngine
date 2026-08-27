namespace AgentMemoryEngine.Core;

/// <summary>
/// Core constants and enumeration types for Agent Memory Engine (AME).
/// </summary>
public static class AmeConstants
{
    /// <summary>
    /// File magic bytes: "AGMEM\x01\x00\x00" (8 bytes).
    /// </summary>
    public static ReadOnlySpan<byte> MagicBytes => "AGMEM\x01\x00\x00"u8;

    /// <summary>
    /// Current format version 1.0 (Major = 1, Minor = 0).
    /// </summary>
    public const uint CurrentVersion = 0x00010000;

    /// <summary>
    /// Standard CPU cache-line memory alignment boundary (64 bytes).
    /// </summary>
    public const int AlignmentBytes = 64;

    /// <summary>
    /// Fixed size of a cognitive record in bytes (32 bytes).
    /// </summary>
    public const int CognitiveRecordSize = 32;

    /// <summary>
    /// Fixed size of a segment descriptor in bytes (32 bytes).
    /// </summary>
    public const int SegmentDescriptorSize = 32;

    /// <summary>
    /// Fixed size of the container global header in bytes (64 bytes).
    /// </summary>
    public const int GlobalHeaderSize = 64;

    /// <summary>
    /// Default retention half-life in hours for unreinforced episodic memory (72 hours = 3 days).
    /// </summary>
    public const double DefaultBaseRetentionHours = 72.0;
}

/// <summary>
/// The 6 distinct memory tiers in the cognitive memory hierarchy.
/// </summary>
public enum AmeMemoryTier : byte
{
    ShortTerm  = 1,
    Working    = 2,
    Episodic   = 3,
    Semantic   = 4,
    Procedural = 5,
    Project    = 6
}

/// <summary>
/// Segment types stored in the .ame binary container.
/// </summary>
public enum AmeSegmentType : ushort
{
    CognitiveArray = 0x0001,
    VectorIndex    = 0x0002,
    GraphCsr       = 0x0003,
    PayloadChunk   = 0x0004,
    WalJournal     = 0x0005,
    SymbolTable    = 0x0006
}

/// <summary>
/// Vector embedding quantization format.
/// </summary>
public enum AmeQuantizationType : byte
{
    Float32 = 0,
    Float16 = 1,
    Int8SQ8 = 2
}

/// <summary>
/// Vector distance metric.
/// </summary>
public enum AmeDistanceMetric : byte
{
    Cosine     = 0,
    DotProduct = 1,
    Euclidean  = 2
}

/// <summary>
/// Edge relationship types in the CSR graph.
/// </summary>
public enum AmeEdgeType : byte
{
    DependsOn   = 1,
    FixesBugIn  = 2,
    DerivedFrom = 3,
    Implements  = 4,
    FollowedBy  = 5,
    RelatedTo   = 6
}
