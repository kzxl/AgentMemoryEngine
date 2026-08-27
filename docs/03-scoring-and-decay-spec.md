# 03. Scoring & Decay Specification: Cognitive Dynamics

---

## 1. Unified Retrieval Scoring Formula

When an agent initiates a retrieval query $q$ within an active Working Memory context $\mathcal{W}$, AME evaluates every candidate memory record $M$ using a **Single-Pass Fused Scoring Function**:

$$Score(M, q, \mathcal{W}) = w_v \cdot S_{\text{vec}}(q, M) + w_i \cdot \hat{I}(M) + w_r \cdot R(M, t) + w_f \cdot \hat{F}(M) + w_g \cdot G(M, \mathcal{W}) + w_c \cdot \hat{C}(M)$$

Where the weights $\mathbf{w} = [w_v, w_i, w_r, w_f, w_g, w_c]$ are normalized ($\sum w_k = 1.0$) with default values:
- $w_v = 0.35$ (Semantic Vector Similarity)
- $w_i = 0.15$ (Intrinsic Importance)
- $w_r = 0.15$ (Temporal Recency Decay)
- $w_f = 0.10$ (Access Frequency)
- $w_g = 0.15$ (Graph Relationship & Working Memory Proximity)
- $w_c = 0.10$ (Empirical Confidence)

---

## 2. Component Mathematical Formulations

### 2.1. Vector Similarity $S_{\text{vec}}(q, M)$
Computed via Cosine Similarity on normalized embeddings:
$$S_{\text{vec}}(q, M) = \frac{\mathbf{v}_q \cdot \mathbf{v}_M}{\|\mathbf{v}_q\| \|\mathbf{v}_M\|} \in [0.0, 1.0]$$

### 2.2. Intrinsic Importance $\hat{I}(M)$
Normalized representation of the record's raw importance ($I \in [1, 100]$):
$$\hat{I}(M) = \frac{I(M)}{100.0}$$

### 2.3. Temporal Recency & Ebbinghaus Decay Curve $R(M, t)$
Memory retention decays exponentially according to an adapted **Ebbinghaus Forgetting Model**, where high access frequency and intrinsic stability increase memory resistance to decay:

$$R(M, t) = \exp\left( - \frac{\Delta t}{\tau(M)} \right)$$

Where:
- $\Delta t = t_{\text{current}} - t_{\text{last\_accessed}}$ (in hours/days).
- $\tau(M)$ is the **Retention Half-Life**, computed as:
  $$\tau(M) = \tau_0 \cdot \left(1 + \beta \cdot \log_2(1 + \text{Freq}(M))\right) \cdot \left(\frac{256 - \text{DecayRate}(M)}{128}\right)$$
  - $\tau_0 = 72.0\text{ hours}$ (Base retention period).
  - $\beta = 0.5$ (Frequency reinforcement factor).
  - For Semantic Memory, $\text{DecayRate} = 0 \implies \tau(M) \to \infty$ (Permanent).

```
Retention R(t)
1.0 |-------------------------\ (Semantic Rule: Zero Decay)
    |                          \
    |-------\                   \---- (Reinforced 5x Accesses)
    |        \
    |         \------- (Unreinforced Single-Use Episodic)
0.0 +-------------------------------------------> Time (Days)
```

### 2.4. Frequency Factor $\hat{F}(M)$
Logarithmic scaling prevents frequently accessed nodes from dominating fresh memories:
$$\hat{F}(M) = \frac{\log_2(1 + \text{Freq}(M))}{\log_2(1 + \text{Freq}_{\text{max}})}$$

### 2.5. Graph Proximity $G(M, \mathcal{W})$
Measures direct or multi-hop adjacency between candidate record $M$ and the active symbols/files present in Working Memory $\mathcal{W}$:
$$G(M, \mathcal{W}) = \max_{u \in \mathcal{W}} \left( \sum_{p \in \text{Paths}(M, u)} \gamma^{\text{len}(p)} \cdot W(p) \right)$$
- $\gamma = 0.7$ (Hop attenuation factor, maximum 2 hops).
- $W(p)$ is the normalized product of edge weights along path $p$.

### 2.6. Empirical Confidence $\hat{C}(M)$
$$\hat{C}(M) = \frac{\text{Confidence}(M)}{100.0}$$
- `Confidence = 100`: Verified by compiler, unit test pass, or explicit user confirmation.
- `Confidence = 50`: Intermediate agent reasoning / hypothesis.
- `Confidence = 20`: Speculative or unverified observation.

---

## 3. Autonomous Memory Governance & Lifecycle

AME executes memory governance through three autonomous pipelines:

```mermaid
flowchart TD
    subgraph P1 ["1. Reinforcement (Post-Task)"]
        A[Task Solved Successfully] --> B[Increment Frequency + 1]
        B --> C[Update Last_Accessed_Ts = Now]
        C --> D[Boost Confidence to 100]
    end

    subgraph P2 ["2. Sleep Consolidation (Background)"]
        E[Scan Episodic Memory Clusters] --> F{Frequency >= 3 & Similarity > 0.85?}
        F -- Yes --> G[Synthesize Generalized Rule ➔ Semantic Memory]
        F -- No --> H[Retain Individual Episodes]
    end

    subgraph P3 ["3. Pruning & Cold Storage (Decay Sweep)"]
        I[Evaluate Retention R(t)] --> J{Score < Eviction_Threshold?}
        J -- Yes --> K[Compress to Cold Archive / Delete]
        J -- No --> L[Keep Active in Fast Index]
    end
```

### 3.1. Reinforcement Pipeline (On Verification)
When an agent utilizes an existing Episodic Memory to solve a problem and verifies it via compiler/test pass:
1. `access_frequency` is incremented.
2. `last_accessed_ts` is reset to current timestamp.
3. `confidence` is elevated to maximum (`100`).

### 3.2. Semantic Induction (Sleep Cycle)
During system idle or background maintenance:
1. **DBSCAN Clustering:** Group episodic lessons based on vector distance ($d < 0.15$) and shared graph targets.
2. **LLM Distillation:** If a cluster has $\ge 3$ verified episodes, synthesize a generalized pattern into **Semantic Memory**.
3. **Link Creation:** Create CSR Graph edges `[EpisodicInstances] ──DerivedInto──> [SemanticRule]`.

### 3.3. Eviction & Cold-Tier Compression
Records where composite $Score < 0.15$ and $\text{Age} > 30 \text{ days}$ are automatically moved to the cold archive or pruned, keeping the hot `.ame` index lean and ultra-responsive.
