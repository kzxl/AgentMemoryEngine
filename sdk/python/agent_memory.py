"""Agent Memory Engine (AME) - Official Python SDK.

Provides high-level bindings for querying and harvesting memories into .ame containers.
"""

from dataclasses import dataclass
from enum import Enum
import subprocess
from typing import List, Optional


class AmeMemoryTier(str, Enum):
    SHORT_TERM = "ShortTerm"
    WORKING = "Working"
    EPISODIC = "Episodic"
    SEMANTIC = "Semantic"
    PROCEDURAL = "Procedural"
    PROJECT = "Project"


@dataclass
class AmeSearchResult:
    memory_id: int
    tier: AmeMemoryTier
    composite_score: float
    vector_similarity: float
    recency_retention: float
    importance: int
    confidence: int
    access_frequency: int
    payload: str


class AmeClient:
    """Python Client for Agent Memory Engine."""

    def __init__(self, db_path: str):
        self.db_path = db_path

    def harvest(
        self,
        payload: str,
        tier: AmeMemoryTier = AmeMemoryTier.EPISODIC,
        importance: int = 80,
        confidence: int = 100,
    ) -> None:
        """Harvests a verified problem/cause/fix lesson into the cognitive container."""
        cmd = [
            "dotnet",
            "run",
            "--project",
            "src/AgentMemoryEngine.Cli",
            "--",
            "post",
            self.db_path,
            payload,
            "--tier",
            tier.value,
            "--importance",
            str(importance),
            "--confidence",
            str(confidence),
        ]
        res = subprocess.run(cmd, capture_output=True, text=True, check=True)

    def query_fused(
        self, query: str, top_k: int = 5, min_score: float = 0.1
    ) -> List[AmeSearchResult]:
        """Executes a single-pass fused search."""
        # Query via CLI or IPC
        return []
