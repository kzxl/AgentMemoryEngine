/**
 * @file index.ts
 * Agent Memory Engine (AME) - Official Node.js & TypeScript SDK
 */

import { spawn } from 'child_process';
import * as net from 'net';

export enum AmeMemoryTier {
  ShortTerm = 'ShortTerm',
  Working = 'Working',
  Episodic = 'Episodic',
  Semantic = 'Semantic',
  Procedural = 'Procedural',
  Project = 'Project'
}

export interface AmeSearchResult {
  memoryId: number;
  tier: AmeMemoryTier;
  compositeScore: number;
  vectorSimilarity: number;
  recencyRetention: number;
  importance: number;
  confidence: number;
  accessFrequency: number;
  payload: string;
}

export interface AmeHarvestOptions {
  tier?: AmeMemoryTier;
  importance?: number;
  confidence?: number;
}

/**
 * High-performance client for interacting with Agent Memory Engine containers.
 */
export class AmeClient {
  private readonly dbPath: string;

  constructor(dbPath: string) {
    this.dbPath = dbPath;
  }

  /**
   * Harvest and persist a verified lesson or standard into the cognitive database.
   */
  async harvest(payload: string, options: AmeHarvestOptions = {}): Promise<void> {
    const tier = options.tier || AmeMemoryTier.Episodic;
    const imp = options.importance ?? 80;
    const conf = options.confidence ?? 100;

    await this.execCli(['post', this.dbPath, payload, '--tier', tier, '--importance', imp.toString(), '--confidence', conf.toString()]);
  }

  /**
   * Executes a single-pass fused cognitive search.
   */
  async queryFused(query: string, topK: number = 5, minScore: number = 0.1): Promise<AmeSearchResult[]> {
    const output = await this.execCli(['query', this.dbPath, query, '--top', topK.toString(), '--min-score', minScore.toString()]);
    // Parse output or use IPC/MCP endpoint
    return [];
  }

  private execCli(args: string[]): Promise<string> {
    return new Promise((resolve, reject) => {
      const proc = spawn('dotnet', ['run', '--project', 'src/AgentMemoryEngine.Cli', '--', ...args]);
      let stdout = '';
      let stderr = '';

      proc.stdout.on('data', data => (stdout += data.toString()));
      proc.stderr.on('data', data => (stderr += data.toString()));

      proc.on('close', code => {
        if (code === 0) resolve(stdout);
        else reject(new Error(`AME CLI exited with code ${code}: ${stderr}`));
      });
    });
  }
}
