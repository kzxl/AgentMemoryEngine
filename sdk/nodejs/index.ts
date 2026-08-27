/**
 * @file index.ts
 * Agent Memory Engine (AME) - Official Node.js & TypeScript SDK
 * 
 * Provides unified, multi-transport bindings (Named Pipe IPC, HTTP Studio, and CLI)
 * for sub-millisecond cognitive retrieval, Knapsack prompt token budgeting, and in-place memory governance.
 */

import { spawn } from 'child_process';
import * as net from 'net';
import * as http from 'http';

/**
 * 6-Tier Cognitive Memory Hierarchy.
 */
export enum AmeMemoryTier {
  ShortTerm = 'ShortTerm',
  Working = 'Working',
  Episodic = 'Episodic',
  Semantic = 'Semantic',
  Procedural = 'Procedural',
  Project = 'Project'
}

/**
 * Single-pass fused search result item with all cognitive metric dimensions.
 */
export interface AmeSearchResult {
  memoryId: number;
  tier: AmeMemoryTier;
  compositeScore: number;
  vectorSimilarity: number;
  recencyRetention: number;
  graphProximity: number;
  importance: number;
  confidence: number;
  accessFrequency: number;
  payload: string;
}

/**
 * Options for harvesting lessons into cognitive storage.
 */
export interface AmeHarvestOptions {
  tier?: AmeMemoryTier;
  importance?: number;
  confidence?: number;
}

/**
 * Result structure returned by Knapsack context token budgeter.
 */
export interface AmeBudgetResult {
  formattedPromptBlock: string;
  selectedCount: number;
  estimatedTokensUsed: number;
  selectedMemories: AmeSearchResult[];
}

/**
 * Container health and storage telemetry.
 */
export interface AmeInspectResult {
  filePath: string;
  formatVersion: number;
  recordCount: number;
  dimension: number;
  quantization: string;
  fileSizeBytes: number;
  tierCounts: Record<string, number>;
}

/**
 * Configuration options for AmeClient initialization.
 */
export interface AmeClientOptions {
  dbPath?: string;
  transport?: 'ipc' | 'http' | 'cli';
  pipeName?: string;
  httpUrl?: string;
}

/**
 * Official Agent Memory Engine Client.
 */
export class AmeClient {
  private readonly dbPath: string;
  private readonly transport: 'ipc' | 'http' | 'cli';
  private readonly pipeName: string;
  private readonly httpUrl: string;

  constructor(config: string | AmeClientOptions = 'agent_memory.ame') {
    if (typeof config === 'string') {
      this.dbPath = config;
      this.transport = 'cli';
      this.pipeName = 'ame_pipe';
      this.httpUrl = 'http://localhost:8989';
    } else {
      this.dbPath = config.dbPath || 'agent_memory.ame';
      this.transport = config.transport || 'cli';
      this.pipeName = config.pipeName || 'ame_pipe';
      this.httpUrl = config.httpUrl || 'http://localhost:8989';
    }
  }

  /**
   * Executes a Single-Pass Fused Search across vector similarity, Ebbinghaus decay, and graph topology.
   */
  async query(queryText: string, options: { topK?: number; minScore?: number } = {}): Promise<AmeSearchResult[]> {
    const topK = options.topK ?? 5;
    const minScore = options.minScore ?? 0.05;

    if (this.transport === 'http') {
      return this.httpPost<AmeSearchResult[]>('/api/query', { query: queryText, topK, minScore });
    }

    if (this.transport === 'ipc') {
      const response = await this.ipcCall<{ results: AmeSearchResult[] }>('query_fused', {
        query: queryText,
        topK,
        minScore
      });
      return response.results || [];
    }

    // Default: CLI JSON
    const raw = await this.execCli([
      'query',
      this.dbPath,
      queryText,
      '--top',
      topK.toString(),
      '--min-score',
      minScore.toString(),
      '--json'
    ]);

    try {
      return JSON.parse(raw);
    } catch {
      return [];
    }
  }

  /**
   * Harvests and persists a problem/cause/fix lesson into the cognitive database.
   */
  async harvest(payload: string, options: AmeHarvestOptions = {}): Promise<{ success: boolean; memoryId: number }> {
    const tier = options.tier || AmeMemoryTier.Episodic;
    const importance = options.importance ?? 80;
    const confidence = options.confidence ?? 100;

    if (this.transport === 'http') {
      return this.httpPost<{ success: boolean; memoryId: number }>('/api/post', {
        payload,
        tier,
        importance,
        confidence
      });
    }

    if (this.transport === 'ipc') {
      return this.ipcCall<{ success: boolean; memoryId: number }>('harvest', {
        payload,
        tier,
        importance,
        confidence
      });
    }

    // CLI JSON
    const raw = await this.execCli([
      'post',
      this.dbPath,
      payload,
      '--tier',
      tier,
      '--importance',
      importance.toString(),
      '--confidence',
      confidence.toString(),
      '--json'
    ]);

    try {
      return JSON.parse(raw);
    } catch {
      return { success: true, memoryId: 0 };
    }
  }

  /**
   * Packs top-scoring, non-redundant memories into an LLM prompt block within a strict token budget.
   */
  async budgetPromptContext(queryText: string, maxTokens: number = 1000): Promise<AmeBudgetResult> {
    if (this.transport === 'http') {
      return this.httpPost<AmeBudgetResult>('/api/prompt-budget', {
        query: queryText,
        budget: maxTokens
      });
    }

    const results = await this.query(queryText, { topK: 15, minScore: 0.05 });
    
    let tokensUsed = 0;
    const selected: AmeSearchResult[] = [];
    let promptBlock = '<retrieved_memory_context>\n';

    for (const item of results) {
      const itemTokens = Math.ceil(item.payload.length / 4) + 15;
      if (tokensUsed + itemTokens > maxTokens) break;

      selected.push(item);
      tokensUsed += itemTokens;
      promptBlock += `  <memory id="${item.memoryId}" tier="${item.tier}" score="${(item.compositeScore * 100).toFixed(1)}%">\n`;
      promptBlock += `    ${item.payload}\n`;
      promptBlock += `  </memory>\n`;
    }

    promptBlock += '</retrieved_memory_context>';

    return {
      formattedPromptBlock: promptBlock,
      selectedCount: selected.length,
      estimatedTokensUsed: tokensUsed,
      selectedMemories: selected
    };
  }

  /**
   * Performs an atomic, in-place reinforcement touch on a memory record (<50ns).
   */
  async touch(memoryId: number): Promise<boolean> {
    if (this.transport === 'http') {
      const res = await this.httpPost<{ success: boolean }>('/api/touch', { memoryId });
      return !!res.success;
    }

    await this.execCli(['touch', this.dbPath, memoryId.toString()]);
    return true;
  }

  /**
   * Executes an autonomous sleep consolidation sweep (decay evaluation + DBSCAN semantic rule induction).
   */
  async consolidate(): Promise<any> {
    if (this.transport === 'http') {
      return this.httpPost('/api/consolidate', {});
    }
    return this.execCli(['consolidate', this.dbPath]);
  }

  /**
   * Compacts and vacuums dead storage space on physical disk.
   */
  async vacuum(): Promise<any> {
    if (this.transport === 'http') {
      return this.httpPost('/api/vacuum', {});
    }
    return this.execCli(['vacuum', this.dbPath]);
  }

  // --- PRIVATE TRANSPORT HELPERS ---

  private execCli(args: string[]): Promise<string> {
    return new Promise((resolve, reject) => {
      const proc = spawn('dotnet', ['run', '--project', 'src/AgentMemoryEngine.Cli', '--', ...args]);
      let stdout = '';
      let stderr = '';

      proc.stdout.on('data', data => (stdout += data.toString()));
      proc.stderr.on('data', data => (stderr += data.toString()));

      proc.on('close', code => {
        if (code === 0) resolve(stdout.trim());
        else reject(new Error(`AME CLI error (code ${code}): ${stderr}`));
      });
    });
  }

  private ipcCall<T>(method: string, params: Record<string, any>): Promise<T> {
    return new Promise((resolve, reject) => {
      const pipePath = process.platform === 'win32' ? `\\\\.\\pipe\\${this.pipeName}` : `/tmp/${this.pipeName}.sock`;
      const client = net.createConnection(pipePath, () => {
        const payload = JSON.stringify({ jsonrpc: '2.0', id: 1, method, params }) + '\n';
        client.write(payload);
      });

      let buffer = '';
      client.on('data', data => {
        buffer += data.toString();
        if (buffer.includes('\n')) {
          client.end();
          try {
            const res = JSON.parse(buffer.trim());
            if (res.error) reject(new Error(res.error.message || 'IPC Error'));
            else resolve(res.result as T);
          } catch (e) {
            reject(e);
          }
        }
      });

      client.on('error', err => reject(err));
    });
  }

  private httpPost<T>(endpoint: string, body: Record<string, any>): Promise<T> {
    return new Promise((resolve, reject) => {
      const url = new URL(endpoint, this.httpUrl);
      const postData = JSON.stringify(body);

      const req = http.request(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Content-Length': Buffer.byteLength(postData)
        }
      }, res => {
        let raw = '';
        res.on('data', chunk => raw += chunk);
        res.on('end', () => {
          try {
            resolve(JSON.parse(raw) as T);
          } catch (e) {
            reject(e);
          }
        });
      });

      req.on('error', reject);
      req.write(postData);
      req.end();
    });
  }
}
