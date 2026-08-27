/**
 * Agent Memory Engine (AME) - Native C-ABI Header
 * Copyright (c) 2026 Agent Memory Engine Contributors.
 * 
 * Provides zero-overhead, memory-mapped cognitive database operations
 * for C/C++, Rust, Go, Python, and Node.js FFI bindings.
 */

#ifndef AME_ENGINE_H
#define AME_ENGINE_H

#include <stdint.h>
#include <stdbool.h>

#ifdef __cplusplus
extern "C" {
#endif

#if defined(_WIN32)
  #define AME_API __declspec(dllexport)
#else
  #define AME_API __attribute__((visibility("default")))
#endif

// --- Status Codes ---
typedef enum {
    AME_STATUS_OK                 = 0,
    AME_STATUS_ERR_INVALID_HANDLE = -1,
    AME_STATUS_ERR_FILE_NOT_FOUND = -2,
    AME_STATUS_ERR_CORRUPT_HEADER = -3,
    AME_STATUS_ERR_CAPACITY_FULL  = -4,
    AME_STATUS_ERR_NULL_POINTER   = -5
} AmeStatusCode;

// --- Memory Tiers ---
typedef enum {
    AME_TIER_SHORT_TERM = 1,
    AME_TIER_WORKING    = 2,
    AME_TIER_EPISODIC   = 3,
    AME_TIER_SEMANTIC   = 4,
    AME_TIER_PROCEDURAL = 5,
    AME_TIER_PROJECT    = 6
} AmeMemoryTierCode;

// --- Opaque Handles ---
typedef void* AmeHandle;

// --- Query Result Struct ---
typedef struct {
    uint32_t memory_id;
    uint8_t  tier;
    uint8_t  importance;
    uint8_t  confidence;
    uint32_t access_frequency;
    float    composite_score;
    float    vector_similarity;
    float    recency_retention;
    uint32_t payload_length;
    const char* payload_text;
} AmeNativeSearchResult;

// ============================================================================
// 1. LIFECYCLE & STORAGE
// ============================================================================

AME_API AmeStatusCode ame_create_container(const char* file_path, uint16_t dimension, AmeHandle* out_handle);
AME_API AmeStatusCode ame_open_container(const char* file_path, AmeHandle* out_handle);
AME_API AmeStatusCode ame_close_container(AmeHandle handle);
AME_API uint32_t      ame_get_record_count(AmeHandle handle);

// ============================================================================
// 2. OPERATIONS
// ============================================================================

AME_API AmeStatusCode ame_append_record(
    AmeHandle handle,
    uint8_t tier,
    const char* payload,
    const float* embedding,
    uint8_t importance,
    uint8_t confidence,
    uint32_t* out_memory_id
);

AME_API AmeStatusCode ame_touch_cognitive(
    AmeHandle handle,
    uint32_t memory_id,
    uint8_t importance,
    uint8_t confidence
);

AME_API AmeStatusCode ame_query_fused(
    AmeHandle handle,
    const float* query_vector,
    uint32_t top_k,
    float min_score,
    AmeNativeSearchResult* out_results,
    uint32_t* out_result_count
);

#ifdef __cplusplus
}
#endif

#endif // AME_ENGINE_H
