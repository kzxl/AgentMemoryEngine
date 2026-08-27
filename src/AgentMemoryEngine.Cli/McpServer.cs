using System.Text.Json;
using System.Text.Json.Nodes;
using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Storage;
using AgentMemoryEngine.Core.Vector;

namespace AgentMemoryEngine.Cli;

/// <summary>
/// Model Context Protocol (MCP) stdio server implementation for Agent Memory Engine.
/// Conforms to Anthropic & open MCP specifications (JSON-RPC 2.0).
/// </summary>
public sealed class McpServer
{
    private readonly AmeContainer _container;

    public McpServer(AmeContainer container)
    {
        _container = container;
    }

    public async Task RunStdioAsync(CancellationToken ct = default)
    {
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var writer = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };

        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct);
            if (line == null) break;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var request = JsonNode.Parse(line)?.AsObject();
                if (request == null) continue;

                var response = HandleRpcRequest(request);
                if (response != null)
                {
                    await writer.WriteLineAsync(response.ToJsonString());
                }
            }
            catch (Exception ex)
            {
                var errorResponse = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = null,
                    ["error"] = new JsonObject
                    {
                        ["code"] = -32603,
                        ["message"] = ex.Message
                    }
                };
                await writer.WriteLineAsync(errorResponse.ToJsonString());
            }
        }
    }

    private JsonObject? HandleRpcRequest(JsonObject request)
    {
        string? method = request["method"]?.GetValue<string>();
        var id = request["id"];

        if (method == "initialize")
        {
            return new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["result"] = new JsonObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "AgentMemoryEngine-MCP",
                        ["version"] = "1.0.0"
                    },
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject(),
                        ["resources"] = new JsonObject()
                    }
                }
            };
        }

        if (method == "tools/list")
        {
            return new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id?.DeepClone(),
                ["result"] = new JsonObject
                {
                    ["tools"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "ame_query",
                            ["description"] = "Fused retrieval across past episodic lessons, semantic rules, and project topology with time decay.",
                            ["inputSchema"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Search query or task description" },
                                    ["topK"] = new JsonObject { ["type"] = "integer", ["description"] = "Number of top results (default: 5)" },
                                    ["minScore"] = new JsonObject { ["type"] = "number", ["description"] = "Minimum fused composite score (default: 0.1)" }
                                },
                                ["required"] = new JsonArray { "query" }
                            }
                        },
                        new JsonObject
                        {
                            ["name"] = "ame_store",
                            ["description"] = "Harvest and persist a verified lesson [Problem | Cause | Fix] or semantic rule.",
                            ["inputSchema"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["properties"] = new JsonObject
                                {
                                    ["payload"] = new JsonObject { ["type"] = "string", ["description"] = "Content formatted as '[Symptom] | [Root Cause] | [Fix]'" },
                                    ["tier"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray { "Episodic", "Semantic", "Procedural" }, ["default"] = "Episodic" },
                                    ["importance"] = new JsonObject { ["type"] = "integer", ["default"] = 80 },
                                    ["confidence"] = new JsonObject { ["type"] = "integer", ["default"] = 100 }
                                },
                                ["required"] = new JsonArray { "payload" }
                            }
                        },
                        new JsonObject
                        {
                            ["name"] = "ame_inspect",
                            ["description"] = "Get database statistics, memory tier distribution, and active status.",
                            ["inputSchema"] = new JsonObject { ["type"] = "object" }
                        }
                    }
                }
            };
        }

        if (method == "tools/call")
        {
            string? toolName = request["params"]?["name"]?.GetValue<string>();
            var args = request["params"]?["arguments"]?.AsObject();

            if (toolName == "ame_query")
            {
                string queryText = args?["query"]?.GetValue<string>() ?? string.Empty;
                uint topK = (uint)(args?["topK"]?.GetValue<int?>() ?? 5);
                float minScore = (float)(args?["minScore"]?.GetValue<double?>() ?? 0.1);

                // Mock/Deterministic text-to-vector for CLI/MCP demo: generate hash-seeded unit vector
                float[] queryVec = CreateDeterministicVector(queryText, _container.Dimension);
                var results = _container.QueryFused(queryVec, topK: topK, minScore: minScore);

                var itemsArray = new JsonArray();
                foreach (var r in results)
                {
                    itemsArray.Add(new JsonObject
                    {
                        ["memoryId"] = r.MemoryId,
                        ["tier"] = r.Tier.ToString(),
                        ["score"] = r.CompositeScore,
                        ["similarity"] = r.VectorSimilarity,
                        ["retention"] = r.RecencyRetention,
                        ["importance"] = r.Importance,
                        ["confidence"] = r.Confidence,
                        ["accessFrequency"] = r.AccessFrequency,
                        ["payload"] = r.Payload
                    });
                }

                return new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id?.DeepClone(),
                    ["result"] = new JsonObject
                    {
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = itemsArray.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
                            }
                        }
                    }
                };
            }

            if (toolName == "ame_store")
            {
                string payload = args?["payload"]?.GetValue<string>() ?? string.Empty;
                string tierStr = args?["tier"]?.GetValue<string>() ?? "Episodic";
                byte importance = (byte)(args?["importance"]?.GetValue<int?>() ?? 80);
                byte confidence = (byte)(args?["confidence"]?.GetValue<int?>() ?? 100);

                var tier = Enum.TryParse<AmeMemoryTier>(tierStr, true, out var parsedTier) ? parsedTier : AmeMemoryTier.Episodic;
                float[] embedding = CreateDeterministicVector(payload, _container.Dimension);

                uint memoryId = _container.AppendRecord(
                    tier,
                    payload,
                    embedding,
                    importance: importance,
                    confidence: confidence,
                    decayRate: (byte)(tier == AmeMemoryTier.Semantic ? 0 : 128));

                return new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id?.DeepClone(),
                    ["result"] = new JsonObject
                    {
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = $"Successfully stored memory ID #{memoryId} in tier [{tier}]."
                            }
                        }
                    }
                };
            }

            if (toolName == "ame_inspect")
            {
                return new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id?.DeepClone(),
                    ["result"] = new JsonObject
                    {
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "text",
                                ["text"] = JsonSerializer.Serialize(new
                                {
                                    filePath = _container.FilePath,
                                    recordCount = _container.RecordCount,
                                    dimension = _container.Dimension,
                                    graphNodes = _container.Graph.NodeCount,
                                    graphEdges = _container.Graph.EdgeCount
                                }, new JsonSerializerOptions { WriteIndented = true })
                            }
                        }
                    }
                };
            }
        }

        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id?.DeepClone(),
            ["error"] = new JsonObject
            {
                ["code"] = -32601,
                ["message"] = $"Method '{method}' not found."
            }
        };
    }

    /// <summary>
    /// Generates a normalized pseudo-embedding from text for CLI/demo fallback without external network dependency.
    /// </summary>
    public static float[] CreateDeterministicVector(string text, int dimension)
    {
        float[] vector = new float[dimension];
        var hash = text.GetHashCode();
        var rng = new Random(hash);

        for (int i = 0; i < dimension; i++)
        {
            vector[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        Quantizer.Normalize(vector);
        return vector;
    }
}
