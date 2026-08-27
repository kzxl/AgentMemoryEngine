# 02. Binary Format Specification: `.ame` Container

---

## 1. Physical Layout & Alignment

The `.ame` (Agent Memory Engine) file is a **single-file, memory-mappable (`mmap`) binary container**.
- **Byte Order:** Little Endian (x86-64 / ARM64 native).
- **Memory Alignment:** All segments and internal record headers are aligned to **64-byte boundaries** (matching modern CPU cache lines).

```
+------------------------------------------------------------------------+
| 1. GLOBAL CONTAINER HEADER (64 Bytes, Fixed)                           |
+------------------------------------------------------------------------+
| 2. SEGMENT TABLE / TABLE OF CONTENTS (TOC)                             |
|    - Segment Descriptors (Type, Offset, Length, Capacity, Count)      |
+------------------------------------------------------------------------+
| 3. COGNITIVE ARRAY SEGMENT (Fixed-size 32-byte records, In-Place)      |
+------------------------------------------------------------------------+
| 4. VECTOR INDEX SEGMENT (Quantized FP16 / SQ8 SIMD-aligned Vectors)    |
+------------------------------------------------------------------------+
| 5. RELATION GRAPH SEGMENT (Compressed Sparse Row - CSR Graph)          |
+------------------------------------------------------------------------+
| 6. PAYLOAD SEGMENT (Zstandard-compressed Text, Markdown, Code, Diffs)  |
+------------------------------------------------------------------------+
| 7. WRITE-AHEAD LOG / JOURNAL (WAL Append-Only Ring Buffer)             |
+------------------------------------------------------------------------+
```

---

## 2. Global Container Header (64 Bytes)

```c
struct AmeGlobalHeader {
    uint8_t  magic[8];              // "AGMEM\x01\x00\x00"
    uint32_t format_version;        // Version (e.g., 0x00010000 = 1.0)
    uint32_t flags;                 // Bitfield: [0: Compressed, 1: ReadOnly, 2: WAL_Active]
    uint64_t file_size;             // Total container size in bytes
    uint64_t segment_table_offset;  // Byte offset to Segment Table
    uint32_t segment_count;         // Total number of segments
    uint32_t header_checksum;       // xxHash32 of Header (0..39 bytes)
    uint64_t data_checksum;         // xxHash64 of all segment data
    uint8_t  reserved[16];          // Zero-padded for future extensions
};
```

---

## 3. Segment Table & Descriptors

The Segment Table starts at `segment_table_offset`. Each entry is a 32-byte descriptor:

```c
enum AmeSegmentType : uint16_t {
    SEG_TYPE_COGNITIVE_ARRAY = 0x0001,
    SEG_TYPE_VECTOR_INDEX    = 0x0002,
    SEG_TYPE_GRAPH_CSR       = 0x0003,
    SEG_TYPE_PAYLOAD_CHUNK   = 0x0004,
    SEG_TYPE_WAL_JOURNAL     = 0x0005,
    SEG_TYPE_SYMBOL_TABLE    = 0x0006
};

struct AmeSegmentDescriptor {
    uint16_t segment_type;     // AmeSegmentType enum
    uint16_t flags;            // Bitfield (IsEncrypted, IsDirty)
    uint32_t item_count;       // Number of logical items in segment
    uint32_t item_size;        // Size per item (0 if variable-length)
    uint64_t byte_offset;      // Byte offset from start of file (64-byte aligned)
    uint64_t byte_length;      // Actual used bytes
    uint64_t byte_capacity;    // Allocated capacity for pre-growth
};
```

---

## 4. Cognitive Array Segment (`SEG_TYPE_COGNITIVE_ARRAY`)

The Cognitive Array consists of contiguous **32-byte fixed-size structs**. Because every record is identical in length, cognitive attributes can be mutated **in-place** via atomic memory operations (`InterlockedCompareExchange` / `atomic CAS`) without rewriting surrounding data or rebuilding vector indexes.

```c
struct AmeCognitiveRecord {
    uint32_t memory_id;            // Unique sequential Memory Record ID
    uint8_t  tier;                 // 1: STM, 2: WM, 3: Episodic, 4: Semantic, 5: Procedural, 6: Project
    uint8_t  importance;           // Static business weight: 1..100
    uint8_t  confidence;           // Verification certainty: 0..100 (100 = Compiler/Test Verified)
    uint8_t  decay_rate;           // Decay factor lambda: 0..255 (0 = permanent, 255 = rapid decay)
    
    uint64_t last_accessed_ts;     // Unix Epoch timestamp (milliseconds)
    uint64_t created_ts;           // Unix Epoch timestamp (milliseconds)
    
    uint32_t access_frequency;     // Cumulative access counter
    uint32_t vector_index_ref;     // Index offset into Vector Index Segment
    uint32_t payload_ref;          // Offset reference into Payload Segment
}; // Exactly 32 bytes
```

---

## 5. Vector Index Segment (`SEG_TYPE_VECTOR_INDEX`)

The Vector Segment stores dense embeddings optimized for **AVX2 / NEON SIMD Dot-Product Scanning**.

### 5.1. Vector Header
```c
struct AmeVectorHeader {
    uint16_t dimension;         // Vector dimension (e.g., 384, 768, 1536)
    uint8_t  quantization_type; // 0: FP32 (Full), 1: FP16 (Half), 2: SQ8 (Scalar Quantized Int8)
    uint8_t  distance_metric;   // 0: Cosine, 1: DotProduct, 2: Euclidean
    uint32_t vector_count;      // Total vector count
    float    sq8_scale;         // Dequantization scale factor (for SQ8)
    float    sq8_offset;        // Dequantization offset factor (for SQ8)
};
```

### 5.2. Scalar Quantization (SQ8) Format
For memory efficiency and SIMD performance, 384-dim vectors are compressed from `1536 bytes` (FP32) to `384 bytes` (Int8):
$$q_i = \text{round}\left(\frac{v_i - \text{offset}}{\text{scale}}\right) \quad \text{where } q_i \in [-128, 127]$$

---

## 6. Relationship Graph Segment (`SEG_TYPE_GRAPH_CSR`)

AME utilizes a **Compressed Sparse Row (CSR)** layout to represent dense relationships (e.g., `[Episodic Memory] ──FixesBugIn──> [Project Code Symbol]`).

```
Nodes (row_ptr): [ 0, 2, 5, 5, 8 ]  -> Indicates slice in col_ind for each node
Edges (col_ind): [ 1, 3,  0, 2, 4,  1, 2, 3 ]
Edge Metadata:   [ Type, Weight, Flags for each edge ]
```

```c
struct AmeGraphEdge {
    uint32_t target_node_id;    // Target Memory ID or Symbol ID
    uint8_t  edge_type;         // 1: DependsOn, 2: FixesBugIn, 3: DerivedFrom, 4: Implements
    uint8_t  weight;            // Relationship strength: 1..100
    uint16_t reserved;
};
```
- **Lookup Complexity:** Finding all neighbors of node $u$ is $O(1)$ slice access: `edges[row_ptr[u] .. row_ptr[u+1]]`.

---

## 7. Payload Segment (`SEG_TYPE_PAYLOAD_CHUNK`)

Payloads (markdown lesson text, code diffs, AST schema JSONs) are packed into **Zstandard (Zstd) compressed frame blocks** (typically 64KB per frame).

```c
struct AmePayloadHeader {
    uint32_t uncompressed_size;
    uint32_t compressed_size;
    uint32_t chunk_checksum;   // CRC32
    uint8_t  compression_codec; // 0: None, 1: Zstd, 2: LZ4
    uint8_t  mime_type;         // 1: Text/Markdown, 2: JSON, 3: Diff, 4: AST_Binary
};
```

---

## 8. Write-Ahead Log (WAL) & Crash Resilience

To avoid data corruption during unexpected agent aborts or power loss:
1. **Append-Only Journal:** All mutations (`INSERT_EPISODIC`, `UPDATE_COGNITIVE`, `FORK_STATE`) are first written sequentially to the `SEG_TYPE_WAL_JOURNAL` block.
2. **Atomic Commit Pointer:** Once the WAL entry is flushed to disk via `fsync()`, the in-place fields in the Cognitive Array and Segment TOC are updated.
3. **Recovery Sequence:** On startup, if `flags.WAL_Active == 1`, AME replays pending journal entries and verifies segment checksums before opening for read/write.
