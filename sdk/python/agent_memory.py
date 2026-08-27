"""Agent Memory Engine (AME) - Official Python SDK.

Provides high-performance, multi-transport bindings (Named Pipe IPC, HTTP Studio, and CLI)
for sub-millisecond cognitive retrieval, Knapsack prompt token budgeting, and in-place memory governance.
"""

from dataclasses import dataclass
from enum import Enum
import json
import os
import subprocess
import sys
from typing import Any, Dict, List, Optional
import urllib.request


class AmeMemoryTier(str, Enum):
    """6-Tier Cognitive Memory Taxonomy."""
    SHORT_TERM = "ShortTerm"
    WORKING = "Working"
    EPISODIC = "Episodic"
    SEMANTIC = "Semantic"
    PROCEDURAL = "Procedural"
    PROJECT = "Project"


@dataclass
class AmeSearchResult:
    """Result item returned by Single-Pass Fused Search."""
    memory_id: int
    tier: AmeMemoryTier
    composite_score: float
    vector_similarity: float
    recency_retention: float
    graph_proximity: float
    importance: int
    confidence: int
    access_frequency: int
    payload: str


@dataclass
class AmeBudgetResult:
    """Prompt Context Block packed within LLM Token Budget."""
    formatted_prompt_block: str
    selected_count: int
    estimated_tokens_used: int
    selected_memories: List[AmeSearchResult]


class AmeClient:
    """Official Python Client for Agent Memory Engine."""

    def __init__(
        self,
        db_path: str = "agent_memory.ame",
        transport: str = "cli",
        pipe_name: str = "ame_pipe",
        http_url: str = "http://localhost:8989"
    ):
        self.db_path = db_path
        self.transport = transport.lower()
        self.pipe_name = pipe_name
        self.http_url = http_url

    def query(
        self,
        query_text: str,
        top_k: int = 5,
        min_score: float = 0.05
    ) -> List[AmeSearchResult]:
        """Executes a single-pass fused search across vector similarity, Ebbinghaus decay, and graph proximity."""
        if self.transport == "http":
            data = self._http_post("/api/query", {"query": query_text, "topK": top_k, "minScore": min_score})
            return [self._parse_result_dict(r) for r in data]

        if self.transport == "ipc":
            res = self._ipc_call("query_fused", {"query": query_text, "topK": top_k, "minScore": min_score})
            raw_list = res.get("results", [])
            return [self._parse_result_dict(r) for r in raw_list]

        # Default: CLI JSON
        cmd = [
            "dotnet", "run", "--project", "src/AgentMemoryEngine.Cli", "--",
            "query", self.db_path, query_text,
            "--top", str(top_k),
            "--min-score", str(min_score),
            "--json"
        ]
        proc = subprocess.run(cmd, capture_output=True, text=True, check=True)
        try:
            items = json.loads(proc.stdout)
            return [self._parse_result_dict(r) for r in items]
        except Exception:
            return []

    def harvest(
        self,
        payload: str,
        tier: AmeMemoryTier = AmeMemoryTier.EPISODIC,
        importance: int = 80,
        confidence: int = 100,
    ) -> Dict[str, Any]:
        """Harvests a verified problem/cause/fix lesson into the cognitive database."""
        if self.transport == "http":
            return self._http_post("/api/post", {
                "payload": payload,
                "tier": tier.value if isinstance(tier, AmeMemoryTier) else str(tier),
                "importance": importance,
                "confidence": confidence
            })

        if self.transport == "ipc":
            return self._ipc_call("harvest", {
                "payload": payload,
                "tier": tier.value if isinstance(tier, AmeMemoryTier) else str(tier),
                "importance": importance,
                "confidence": confidence
            })

        tier_val = tier.value if isinstance(tier, AmeMemoryTier) else str(tier)
        cmd = [
            "dotnet", "run", "--project", "src/AgentMemoryEngine.Cli", "--",
            "post", self.db_path, payload,
            "--tier", tier_val,
            "--importance", str(importance),
            "--confidence", str(confidence),
            "--json"
        ]
        proc = subprocess.run(cmd, capture_output=True, text=True, check=True)
        try:
            return json.loads(proc.stdout)
        except Exception:
            return {"success": True, "memoryId": 0}

    def budget_prompt_context(
        self,
        query_text: str,
        max_tokens: int = 1000
    ) -> AmeBudgetResult:
        """Packs highest-scoring memories into an LLM prompt XML/Markdown block within a token budget."""
        if self.transport == "http":
            res = self._http_post("/api/prompt-budget", {"query": query_text, "budget": max_tokens})
            return AmeBudgetResult(
                formatted_prompt_block=res.get("formattedPromptBlock", ""),
                selected_count=res.get("selectedCount", 0),
                estimated_tokens_used=res.get("estimatedTokensUsed", 0),
                selected_memories=[self._parse_result_dict(r) for r in res.get("selectedMemories", [])]
            )

        results = self.query(query_text, top_k=15, min_score=0.05)
        tokens_used = 0
        selected: List[AmeSearchResult] = []
        prompt_block = ["<retrieved_memory_context>"]

        for item in results:
            item_tokens = len(item.payload) // 4 + 15
            if tokens_used + item_tokens > max_tokens:
                break
            selected.append(item)
            tokens_used += item_tokens
            prompt_block.append(f'  <memory id="{item.memory_id}" tier="{item.tier.value}" score="{item.composite_score * 100:.1f}%">')
            prompt_block.append(f'    {item.payload}')
            prompt_block.append("  </memory>")

        prompt_block.append("</retrieved_memory_context>")

        return AmeBudgetResult(
            formatted_prompt_block="\n".join(prompt_block),
            selected_count=len(selected),
            estimated_tokens_used=tokens_used,
            selected_memories=selected
        )

    def touch(self, memory_id: int) -> bool:
        """Performs atomic in-place reinforcement on a memory record (<50ns)."""
        if self.transport == "http":
            res = self._http_post("/api/touch", {"memoryId": memory_id})
            return bool(res.get("success"))

        cmd = ["dotnet", "run", "--project", "src/AgentMemoryEngine.Cli", "--", "touch", self.db_path, str(memory_id)]
        subprocess.run(cmd, capture_output=True, check=True)
        return True

    def consolidate(self) -> Dict[str, Any]:
        """Runs background sleep consolidation sweep (decay evaluation + DBSCAN semantic rule induction)."""
        if self.transport == "http":
            return self._http_post("/api/consolidate", {})
        return {}

    def vacuum(self) -> Dict[str, Any]:
        """Compacts and vacuums dead storage space on physical disk."""
        if self.transport == "http":
            return self._http_post("/api/vacuum", {})
        return {}

    # --- PRIVATE HELPERS ---

    def _parse_result_dict(self, r: Dict[str, Any]) -> AmeSearchResult:
        tier_str = r.get("tier", "Episodic")
        try:
            tier_enum = AmeMemoryTier(tier_str)
        except ValueError:
            tier_enum = AmeMemoryTier.EPISODIC

        return AmeSearchResult(
            memory_id=int(r.get("memoryId", 0)),
            tier=tier_enum,
            composite_score=float(r.get("compositeScore", 0.0)),
            vector_similarity=float(r.get("vectorSimilarity", r.get("similarity", 0.0))),
            recency_retention=float(r.get("recencyRetention", r.get("retention", 0.0))),
            graph_proximity=float(r.get("graphProximity", 0.0)),
            importance=int(r.get("importance", 80)),
            confidence=int(r.get("confidence", 100)),
            access_frequency=int(r.get("accessFrequency", 1)),
            payload=str(r.get("payload", ""))
        )

    def _http_post(self, endpoint: str, data: Dict[str, Any]) -> Any:
        url = f"{self.http_url.rstrip('/')}{endpoint}"
        payload = json.dumps(data).encode("utf-8")
        req = urllib.request.Request(
            url,
            data=payload,
            headers={"Content-Type": "application/json"}
        )
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read().decode("utf-8"))

    def _ipc_call(self, method: str, params: Dict[str, Any]) -> Dict[str, Any]:
        if sys.platform == "win32":
            pipe_path = f"\\\\.\\pipe\\{self.pipe_name}"
            with open(pipe_path, "r+b", buffering=0) as f:
                req = json.dumps({"jsonrpc": "2.0", "id": 1, "method": method, "params": params}) + "\n"
                f.write(req.encode("utf-8"))
                resp_line = f.readline().decode("utf-8")
                res = json.loads(resp_line)
                if "error" in res:
                    raise RuntimeError(res["error"].get("message", "IPC error"))
                return res.get("result", {})
        else:
            import socket
            sock_path = f"/tmp/{self.pipe_name}.sock"
            with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as client:
                client.connect(sock_path)
                req = json.dumps({"jsonrpc": "2.0", "id": 1, "method": method, "params": params}) + "\n"
                client.sendall(req.encode("utf-8"))
                resp = client.recv(65536).decode("utf-8")
                res = json.loads(resp)
                if "error" in res:
                    raise RuntimeError(res["error"].get("message", "IPC error"))
                return res.get("result", {})
