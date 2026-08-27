# Agent Memory Engine (AME)

> **A High-Performance, Specialized Cognitive Database Engine for Autonomous AI Agents**

---

## 📌 Executive Summary

Current AI Agent architectures rely on generic Vector Databases (Pinecone, Qdrant, Milvus) or relational databases (SQLite, PostgreSQL) to store memories. These general-purpose engines suffer from fundamental **impedance mismatches** when applied to autonomous agent cognition:
1. **Siloed Querying:** RAG pipelines execute vector similarity searches, metadata filters, and graph traversals in isolated, sequential steps, resulting in unacceptable multi-roundtrip latency.
2. **High-Frequency In-Place Updates:** Cognitive metrics (*Recency, Access Frequency, Confidence, Decay factors*) change dynamically every time an agent interacts with a memory. Relational and vector engines incur heavy write locks and index rebuilds for such mutations.
3. **Absence of Memory Tiering & Lifecycle:** Generic databases treat data as flat, static records with no innate understanding of *Working Memory*, *Episodic Lessons*, *Procedural Workflows*, or *Autonomous Consolidation & Forgetting*.

**Agent Memory Engine (AME)** is a purpose-built, single-file binary storage engine (`.ame`) designed to provide **sub-millisecond fused cognitive retrieval**, **in-place atomic mutations**, **zero-copy memory mapping (`mmap`)**, and **autonomous biological-inspired lifecycle governance** for AI Agents.

---

## 🏛️ Comprehensive Architecture Matrix

```
┌────────────────────────────────────────────────────────────────────────┐
│                        AGENT RUNTIME (LLM / IDE)                       │
│        (Node.js SDK, Python SDK, Cursor, Claude Code, Antigravity)     │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
          ┌─────────────────────────┼─────────────────────────┐
          │                         │                         │
   [1. MCP stdio Server]   [2. Named Pipe IPC Server] [3. Native C-ABI / FFI]
   (`ame mcp <file.ame>`)  (`\\.\pipe\ame_pipe`)      (`include/ame_engine.h`)
          │                         │                         │
          └─────────────────────────┼─────────────────────────┘
                                    │
 ┌──────────────────────────────────▼────────────────────────────────────┐
 │                      AGENT MEMORY ENGINE CORE                         │
 │                                                                       │
 │  ┌─────────────────────────┐           ┌───────────────────────────┐  │
 │  │ Working Memory (State)  │ ◄───────► │ Cognitive Array (Dynamic) │  │
 │  │ [CoW Forks & Rollback]  │           │ [Fixed 32B Structs]       │  │
 │  └─────────────────────────┘           └─────────────┬─────────────┘  │
 │                                                      │                │
 │  ┌───────────────────────────────────────────────────▼─────────────┐  │
 │  │ Two-Stage Hybrid Fused Search Pipeline (SIMD + BM25 + Decay)    │  │
 │  │ Stage 1: 1-Bit BQ Hamming Filter (PopCount in 50ns)             │  │
 │  │ Stage 2: SQ8 AVX2 + BM25 + Spreading Activation + Decay Rerank  │  │
 │  └───────────┬───────────────────────────────┬─────────────────────┘  │
 │              │                               │                        │
 │  ┌───────────▼──────────┐      ┌─────────────▼──────────┐             │
 │  │ Quantized Vectors    │      │ CSR Relationship Graph │             │
 │  │ (SQ8 AVX2 + BQ 1-Bit)│      │ [Spreading Activation] │             │
 │  │ + HNSW Multi-Layer   │      │ + Inverted BM25 Index  │             │
 │  └──────────────────────┘      └────────────────────────┘             │
 │                                                                       │
 │  ┌─────────────────────────────────────────────────────────────────┐  │
 │  │ Smart Context Token Budgeter (`ContextBudgeter.cs`)             │  │
 │  │ [Greedy Knapsack Packing & Markdown LLM Prompt Injection]       │  │
 │  └─────────────────────────────────────────────────────────────────┘  │
 │                                                                       │
 │  ┌─────────────────────────────────────────────────────────────────┐  │
 │  │ Write-Ahead Log (WAL) & Storage Vacuum Compactor Defragmenter   │  │
 │  └─────────────────────────────────────────────────────────────────┘  │
 └──────────────────────────────────▲────────────────────────────────────┘
                                    │
 ┌──────────────────────────────────┴────────────────────────────────────┐
 │                   AME STUDIO V2 WEB DASHBOARD (UI)                    │
 │      Embedded HTTP Server (`ame studio <file.ame> --port 8989`)       │
 │   - ✨ AI Prompt Token Budgeter (Live Knapsack packing & 1-click copy)│
 │   - 🔍 Fused Search & 5-Dimension Score Breakdown Inspector           │
 │   - 🕸️ Force-Directed Knowledge Graph Explorer (Canvas)               │
 │   - 📉 Ebbinghaus Continuous Decay Curve Simulator                    │
 │   - 🧹 Autonomous Sleep Consolidation & Storage Vacuum Defragmenter   │
 └───────────────────────────────────────────────────────────────────────┘
```

---

## 📊 Benchmark & Performance Measurements

All measurements were executed on an AMD/Intel x64 machine running Windows 11 with .NET 9.0 Native AOT / Release mode.

### 1. Large-Scale 100MB Dataset Benchmark (50,000 Memories)

```
==================================================================
  🚀 AME LARGE-SCALE BENCHMARK RESULTS (100MB Container)
  Dataset: 100.00 MB | 50,000 Records | 384 Dimensions | 100 Queries
==================================================================
```

| Metric | Measured Value | Evaluation & Details |
| :--- | :---: | :--- |
| **Container File Size** | **$100.00\text{ MB}$** | $104,857,600\text{ bytes}$ on physical disk. |
| **Ingestion Speed** | **$2,234\text{ records/sec}$** | Quantized SQ8 + Zstd Compressed + Indexed in $22.38\text{s}$. |
| **Query Throughput** | **$\mathbf{486.2 - 526.4\text{ QPS}}$** | Over 500 fused searches / second across 50,000 vectors. |
| **Average Query Latency** | **$\mathbf{1.89\text{ ms}}$** | Vector Cosine + Decay Math + CSR Graph + Top-K selection. |
| **Min Query Latency** | **$\mathbf{1.51\text{ ms}}$** | Best-case L1/L2 cache hit. |
| **Median (P50) Latency** | **$\mathbf{1.84\text{ ms}}$** | $50\%$ of queries complete in $< 1.84\text{ ms}$. |
| **95th Percentile (P95)** | **$\mathbf{2.30\text{ ms}}$** | $95\%$ of queries complete in $< 2.30\text{ ms}$. |
| **99th Percentile (P99)** | **$\mathbf{4.50\text{ ms}}$** | Tail latency remains tightly bounded with zero GC pauses. |
| **Managed Heap Footprint** | **$\approx 45.9\text{ MB}$** | Zero-alloc SIMD loop prevents heap thrashing. |

### 2. Standard Benchmark (1,000 Records Baseline)

| Measurement | Result |
| :--- | :---: |
| **Average Fused Query Latency** | **$\mathbf{0.8449\text{ ms}}$** |
| **In-Place Atomic Mutation Latency** | **$< 50\text{ ns}$** |
| **Working Memory Fork Creation** | **$< 1\text{ }\mu\text{s}$** |
| **Automated Test Suite Pass Rate** | **28/28 Passed ($100\%$)** |

### 3. Architectural Comparison Matrix

| Feature | SQLite | RocksDB (LSM) | Qdrant / Milvus | LanceDB | **Agent Memory Engine (AME)** |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Data Model** | B-Tree (Row) | LSM-Tree (SST) | Graph / IVF-PQ | Columnar (Arrow) | **Heterogeneous Segmented** |
| **Query Latency (50k)** | $35 - 90\text{ ms}$ | $20 - 45\text{ ms}$ | $15 - 35\text{ ms}$ | $8 - 15\text{ ms}$ | **$\mathbf{1.89\text{ ms}}$ (AVX2 Multi-Core)** |
| **In-Place Mutation** | Page Lock | Append New Key | Re-index / Buffer | Append Fragment | **Atomic Memory Overwrite ($<50\text{ns}$)** |
| **Knowledge Graph** | Needs JOINs | No | No | No | **CSR Graph ($O(1)$ Slices + Spreading Activation)** |
| **CoW State Branching** | Savepoint | No | No | Table-level | **Sub-microsecond Memory Forks** |
| **Cognitive Dynamics** | Static | TTL delete | Static | Static | **Ebbinghaus Decay + DBSCAN Induction** |
| **LLM Context Budgeter**| External | External | External | External | **Built-in Knapsack Token Budgeter** |

---

## ⚡ Quickstart & Usage

### 1. Command Line Interface (CLI)

```bash
# Initialize a new .ame cognitive container
dotnet run --project src/AgentMemoryEngine.Cli -- init my_memory.ame --dim 384

# Harvest an episodic lesson
dotnet run --project src/AgentMemoryEngine.Cli -- post my_memory.ame "GridControl freeze on WinForms | Sync void delegate | Use RunAfterShown with Task" --tier Episodic --importance 90

# Execute a Single-Pass Fused Search
dotnet run --project src/AgentMemoryEngine.Cli -- query my_memory.ame "GridControl freeze" --top 5

# Auto-Index repository AST codebase symbols into Project Memory
dotnet run --project src/AgentMemoryEngine.Cli -- index my_memory.ame src/AgentMemoryEngine.Core

# Run live 100MB dataset benchmark
dotnet run --project src/AgentMemoryEngine.Cli -c Release -- bench --records 50000 --queries 100

# Launch AME Studio Web UI
dotnet run --project src/AgentMemoryEngine.Cli -- studio my_memory.ame --port 8989
```

### 2. Model Context Protocol (MCP) Server

AI agents (Cursor, Claude Code, Windsurf, Antigravity) connect via stdio:
```json
{
  "mcpServers": {
    "agent-memory-engine": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/AgentMemoryEngine.Cli", "--", "mcp", "path/to/database.ame"]
    }
  }
}
```

Exposed MCP tools:
- `ame_query`: Single-pass fused semantic, lexical, and topological retrieval.
- `ame_store`: Persists verified lessons, rules, or procedural workflows.
- `ame_inspect`: Returns container health, tier distribution, and decay metrics.

### 3. Node.js & TypeScript SDK

```typescript
import { AmeClient, AmeMemoryTier } from '@agent-memory/engine';

const client = new AmeClient('path/to/database.ame');

// Harvest memory
await client.harvest('GridControl freeze | Sync call | Use async Task', {
  tier: AmeMemoryTier.Episodic,
  importance: 90,
  confidence: 100
});

// Fused search
const results = await client.queryFused('GridControl freeze', 5);
```

### 4. Python SDK

```python
from agent_memory import AmeClient, AmeMemoryTier

client = AmeClient("path/to/database.ame")

client.harvest(
    payload="SQL deadlock on commit | Missing index | Added IX_Order_Status",
    tier=AmeMemoryTier.EPISODIC,
    importance=95
)

results = client.query_fused("SQL deadlock", top_k=5)
```

---

## 📚 Specification Documentation

- **[01. Architecture Overview](docs/01-architecture-overview.md)**
- **[02. Binary Format Specification (`.ame`)](docs/02-binary-format-spec.md)**
- **[03. Scoring & Ebbinghaus Decay Specification](docs/03-scoring-and-decay-spec.md)**
- **[04. Engine API & Interface Specification](docs/04-engine-api-spec.md)**
- **[05. Roadmap & Implementation History](docs/05-roadmap-and-implementation.md)**
