# Agent Memory Engine (AME)

> **A High-Performance, Specialized Cognitive Database Engine for Autonomous AI Agents**

---

## 📌 Executive Summary

Current AI Agent architectures rely on generic Vector Databases (Pinecone, Qdrant, Milvus) or relational databases (SQLite, PostgreSQL) to store memories. These general-purpose databases suffer from fundamental **impedance mismatches** when applied to autonomous agent cognition:
1. **Siloed Querying:** RAG pipelines must execute vector similarity searches, relational metadata filters, and graph relationship traversals in isolated, sequential steps, resulting in unacceptable latency.
2. **High-Frequency In-Place Updates:** Cognitive metrics (*Recency, Access Frequency, Confidence, Decay factors*) change dynamically every time an agent interacts with a memory. Relational and vector engines incur heavy write locks and index rebuilds for such mutations.
3. **Absence of Memory Tiering & Lifecycle:** Generic databases treat data as flat, static records with no innate understanding of *Working Memory*, *Episodic Lessons*, *Procedural Workflows*, or *Autonomous Consolidation & Forgetting*.

**Agent Memory Engine (AME)** is a purpose-built, single-file binary storage engine (`.ame`) designed to provide **sub-millisecond fused cognitive retrieval**, **in-place mutation**, **zero-copy memory mapping (`mmap`)**, and **autonomous lifecycle governance** for AI Agents.

---

## 📚 Specification Index

The complete specification is organized into the following architectural documents:

| Document | Description |
| :--- | :--- |
| **[01. Architecture Overview](file:///e:/15.%20Other/AgentMemoryEngine/docs/01-architecture-overview.md)** | Multi-tier memory model, cognitive lifecycle (Pre-Fetch/Post-Harvest), and core invariants. |
| **[02. Binary Format Spec (`.ame`)](file:///e:/15.%20Other/AgentMemoryEngine/docs/02-binary-format-spec.md)** | Byte-level file layout, segment tables, fixed-size cognitive blocks, quantized vectors, and CSR graph encoding. |
| **[03. Scoring & Decay Spec](file:///e:/15.%20Other/AgentMemoryEngine/docs/03-scoring-and-decay-spec.md)** | Mathematical formulation for single-pass fused scoring, Ebbinghaus decay curves, and autonomous consolidation. |
| **[04. Engine API & Interface Spec](file:///e:/15.%20Other/AgentMemoryEngine/docs/04-engine-api-spec.md)** | C-ABI / FFI bindings, CRUD operations, state branching (CoW), and client integration protocols. |
| **[05. Implementation Roadmap](file:///e:/15.%20Other/AgentMemoryEngine/docs/05-roadmap-and-implementation.md)** | Phased implementation milestones, verification strategy, crash consistency (WAL), and benchmarks. |

---

## 🧩 Architectural Highlights

```
┌────────────────────────────────────────────────────────────────────────┐
│                        AGENT RUNTIME (LLM CORE)                        │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ Pre-Fetch / Fused Search / Post-Harvest
 ┌──────────────────────────────────▼────────────────────────────────────┐
 │                      AGENT MEMORY ENGINE (.ame)                       │
 │                                                                       │
 │  ┌─────────────────────────┐           ┌───────────────────────────┐  │
 │  │ Working Memory (State)  │ ◄───────► │ Cognitive Array (Dynamic) │  │
 │  │ [Fast RAM / CoW Forks]  │           │ [Fixed Structs, In-Place] │  │
 │  └─────────────────────────┘           └─────────────┬─────────────┘  │
 │                                                      │                │
 │  ┌───────────────────────────────────────────────────▼─────────────┐  │
 │  │ Single-Pass Fused Search Pipeline (SIMD + Score Fused Scan)     │  │
 │  └───────────┬───────────────────────────────┬─────────────────────┘  │
 │              │                               │                        │
 │  ┌───────────▼──────────┐      ┌─────────────▼──────────┐             │
 │  │ Quantized Vectors    │      │ CSR Relationship Graph │             │
 │  │ (SQ8 / FP16 HNSW)    │      │ (Topology & AST Links) │             │
 │  └──────────────────────┘      └────────────────────────┘             │
 │                                                                       │
 │  ┌─────────────────────────────────────────────────────────────────┐  │
 │  │ Compressed Payloads (Zstd Chunks: Markdown, Schemas, Code Diffs)│  │
 │  └─────────────────────────────────────────────────────────────────┘  │
 └──────────────────────────────────▲────────────────────────────────────┘
                                    │ Background Worker
 ┌──────────────────────────────────┴────────────────────────────────────┐
 │        AUTONOMOUS GOVERNANCE & SLEEP CONSOLIDATION SUBSYSTEM          │
 │       (Decay Evaluation, Rule Induction, Cold-Memory Pruning)         │
 └───────────────────────────────────────────────────────────────────────┘
```
