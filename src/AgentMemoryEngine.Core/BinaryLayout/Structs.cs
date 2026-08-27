using System.Runtime.InteropServices;

namespace AgentMemoryEngine.Core.BinaryLayout;

/// <summary>
/// Global container header for the .ame file (Exactly 64 bytes, 64-byte aligned).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
public unsafe struct AmeGlobalHeader
{
    public fixed byte Magic[8];             // "AGMEM\x01\x00\x00"
    public uint FormatVersion;              // Version (e.g., 0x00010000 = 1.0)
    public uint Flags;                      // Bitfield: [0: Compressed, 1: ReadOnly, 2: WAL_Active]
    public ulong FileSize;                  // Total container size in bytes
    public ulong SegmentTableOffset;        // Byte offset to Segment Table
    public uint SegmentCount;               // Number of descriptors in Segment Table
    public uint HeaderChecksum;             // CRC32/xxHash32 of Header (0..39 bytes)
    public ulong DataChecksum;              // Checksum of segment contents
    public fixed byte Reserved[16];         // Zero-padded reserved block (64 bytes total)
}

/// <summary>
/// Segment table descriptor (Exactly 32 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
public struct AmeSegmentDescriptor
{
    public ushort SegmentType;              // AmeSegmentType enum
    public ushort Flags;                    // Segment flags
    public uint ItemCount;                  // Number of logical items in segment
    public uint ItemSize;                   // Size per item in bytes (0 if variable)
    public ulong ByteOffset;                // 64-byte aligned offset in file
    public ulong ByteLength;                // Actual used bytes in segment
    public uint Reserved;                   // Alignment padding
}

/// <summary>
/// Fixed-size Cognitive Record (Exactly 32 bytes).
/// Fits 2 records per 64-byte CPU cache line. Supports in-place atomic mutations.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
public struct AmeCognitiveRecord
{
    public uint MemoryId;                   // Unique sequential ID
    public byte Tier;                       // AmeMemoryTier enum (1..6)
    public byte Importance;                 // Business weight: 1..100
    public byte Confidence;                 // Verification score: 0..100
    public byte DecayRate;                  // Ebbinghaus decay lambda (0 = permanent)
    
    public uint LastAccessedTimestamp;      // Unix Epoch timestamp in seconds (valid to year 2106)
    public uint CreatedTimestamp;           // Unix Epoch timestamp in seconds
    
    public uint AccessFrequency;            // Cumulative access counter
    public uint VectorIndexRef;             // Index offset into Vector Index Segment
    public uint PayloadRef;                 // Offset reference into Payload Segment
    public uint Reserved;                   // Alignment padding (32 bytes total)
}

/// <summary>
/// Vector segment header descriptor (Exactly 16 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
public struct AmeVectorHeader
{
    public ushort Dimension;                // Vector dimension (e.g., 384, 768)
    public byte QuantizationType;           // AmeQuantizationType enum
    public byte DistanceMetric;             // AmeDistanceMetric enum
    public uint VectorCount;                // Total vectors stored
    public float Sq8Scale;                  // Dequantization scale factor
    public float Sq8Offset;                 // Dequantization offset factor
}

/// <summary>
/// Payload chunk header descriptor (Exactly 16 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
public struct AmePayloadHeader
{
    public uint UncompressedSize;          // Uncompressed size in bytes
    public uint CompressedSize;            // Compressed size in bytes
    public uint ChunkChecksum;             // CRC32 checksum
    public byte CompressionCodec;          // 0: None, 1: Zstd, 2: Deflate
    public byte MimeType;                  // 1: Text/Markdown, 2: JSON, 3: Diff, 4: Binary
    public ushort Reserved;                // Reserved padding
}
