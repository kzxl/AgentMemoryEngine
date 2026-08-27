# 04. Engine API & Interface Specification

---

## 1. Native C-ABI Interface (`ame_engine.h`)

AME is exposed as a native C-compatible dynamic library (`libame.so`, `ame.dll`, `libame.dylib`), allowing direct zero-overhead FFI bindings from **Rust, C# (.NET 9 Native AOT), Node.js, and Python**.

```c
#ifndef AME_ENGINE_H
#define AME_ENGINE_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

// --- Handle Types ---
typedef struct AmeDatabase* AmeDbHandle;
typedef struct AmeWorkingFrame* AmeFrameHandle;

// --- Status Codes ---
typedef enum {
    AME_OK                   = 0,
    AME_ERR_INVALID_HANDLE   = -1,
    AME_ERR_CORRUPT_HEADER   = -2,
    AME_ERR_CAPACITY_FULL    = -3,
    AME_ERR_IO               = -4,
    AME_ERR_CHECKSUM_FAILED  = -5,
    AME_ERR_RECORD_NOT_FOUND = -6
} AmeStatus;

// --- Query Configuration ---
typedef struct {
    const float*  query_vector;       // Array of floats
    uint32_t      vector_dimension;   // e.g., 384
    uint8_t       target_tiers;       // Bitfield: (1<<TIER_EPISODIC) | (1<<TIER_SEMANTIC)
    uint32_t      top_k;              // Number of results to return
    float         min_score_threshold;// Minimum composite score (0.0 .. 1.0)
    uint32_t      active_symbol_ids[16]; // Active symbols in Working Memory
    uint32_t      active_symbol_count;
} AmeQueryParams;

// --- Search Result Item ---
typedef struct {
    uint32_t memory_id;
    uint8_t  tier;
    float    composite_score;
    float    vector_similarity;
    float    recency_retention;
    uint32_t payload_length;
    const char* payload_data;        // Null-terminated UTF-8 markdown/text
} AmeQueryResult;

// ============================================================================
// 1. LIFECYCLE & STORAGE OPERATIONS
// ============================================================================

/**
 * Open or create a single-file .ame container with memory mapping.
 */
AmeStatus ame_open(const char* file_path, uint32_t flags, AmeDbHandle* out_db);

/**
 * Flush WAL and release all memory mappings.
 */
AmeStatus ame_close(AmeDbHandle db);

/**
 * Execute background maintenance: Apply Ebbinghaus decay, cluster, and prune.
 */
AmeStatus ame_consolidate(AmeDbHandle db);

// ============================================================================
// 2. RETRIEVAL & FUSED SEARCH
// ============================================================================

/**
 * Execute Single-Pass Fused Search across vector, metadata, graph, and decay.
 */
AmeStatus ame_query_fused(
    AmeDbHandle db,
    const AmeQueryParams* params,
    AmeQueryResult* out_results,
    uint32_t* out_result_count
);

// ============================================================================
// 3. PERSISTENCE & POST-HARVEST
// ============================================================================

/**
 * Store a verified episodic memory record [Problem | Cause | Fix].
 */
AmeStatus ame_store_episodic(
    AmeDbHandle db,
    const float* embedding,
    uint8_t importance,
    uint8_t confidence,
    const char* symptom_cause_fix_payload,
    uint32_t* out_memory_id
);

/**
 * Mutate cognitive attributes in-place (Atomic, zero index rebuild).
 */
AmeStatus ame_touch_cognitive(
    AmeDbHandle db,
    uint32_t memory_id,
    uint8_t importance_delta,
    uint8_t confidence_override
);

// ============================================================================
// 4. WORKING MEMORY & COPY-ON-WRITE BRANCHING
// ============================================================================

/**
 * Create a new Working Memory frame for the active task.
 */
AmeStatus ame_wm_begin_frame(AmeDbHandle db, const char* task_goal, AmeFrameHandle* out_frame);

/**
 * Fork Working Memory for speculative hypothesis exploration (CoW).
 */
AmeStatus ame_wm_fork(AmeFrameHandle parent_frame, AmeFrameHandle* out_child_fork);

/**
 * Commit hypothesis changes back into parent frame.
 */
AmeStatus ame_wm_merge(AmeFrameHandle child_fork);

/**
 * Discard speculative changes cleanly upon hypothesis rejection.
 */
AmeStatus ame_wm_rollback(AmeFrameHandle child_fork);

#ifdef __cplusplus
}
#endif

#endif // AME_ENGINE_H
```

---

## 2. High-Level Language Bindings (TypeScript / Node.js API)

```typescript
import { AmeEngine, MemoryTier } from '@agent-memory/engine';

// 1. Initialize DB container
const db = await AmeEngine.open('./.agents/memory.ame');

// 2. PRE-FETCH: Retrieve fused context before code generation
const context = await db.queryFused({
  task: "Fix NullReferenceException in MDS Sales Packing GridControl",
  domain: "sales",
  topK: 5,
  minScore: 0.65
});

console.log(`Found ${context.length} relevant lessons from past tasks:`);
for (const item of context) {
  console.log(`[Score: ${item.compositeScore.toFixed(2)}] ${item.payload}`);
}

// 3. WORKING MEMORY: Isolate execution branch
const frame = await db.workingMemory.begin("Refactor sales controller");
try {
  frame.touchFile("src/MDS/SalesController.cs");
  frame.setHypothesis("Switch sync delegate to async Task in RunAfterShown");

  // Run compiler & tests...
  const testPassed = true; // Evidence verified

  if (testPassed) {
    // 4. POST-HARVEST: Commit verified insight
    await db.storeEpisodic({
      problem: "GridControl freeze after RunAfterShown",
      rootCause: "Invoked sync void delegate instead of Task",
      fix: "Use RunAfterShown(async () => await LoadData()) with top-level try/catch",
      tags: ["sales", "winforms", "async"],
      confidence: 100
    });
  }
} finally {
  await frame.dispose();
  await db.close();
}
```
