using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Governance;
using AgentMemoryEngine.Core.Scoring;
using AgentMemoryEngine.Core.Storage;

namespace AgentMemoryEngine.Cli;

/// <summary>
/// Embedded lightweight HTTP server for AME Studio Web Dashboard.
/// Serves the single-page visualizer and provides JSON REST APIs.
/// </summary>
public sealed class StudioServer
{
    private readonly AmeContainer _container;
    private readonly int _port;
    private HttpListener? _listener;

    public StudioServer(AmeContainer container, int port = 8989)
    {
        _container = container;
        _port = port;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();

        Console.WriteLine($"\n=================================================");
        Console.WriteLine($"  🧠 AME STUDIO IS LIVE & LISTENING");
        Console.WriteLine($"  👉 URL: http://localhost:{_port}/");
        Console.WriteLine($"  Press Ctrl+C to stop the Studio server.");
        Console.WriteLine($"=================================================\n");

        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = HandleRequestAsync(context);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Studio Error] {ex.Message}");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS headers
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = 204;
            response.Close();
            return;
        }

        string path = request.Url?.AbsolutePath ?? "/";

        try
        {
            if (request.HttpMethod == "GET" && path == "/")
            {
                string html = StudioHtml.GetHtml();
                byte[] buffer = Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
            }
            else if (request.HttpMethod == "GET" && path == "/api/memories")
            {
                var list = new List<object>();
                uint now = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                for (uint id = 1; id <= _container.RecordCount; id++)
                {
                    if (_container.TryGetRecord(id, out var rec, out var payload))
                    {
                        float retention = AmeScoringEngine.ComputeRetention(rec, now);
                        list.Add(new
                        {
                            memoryId = rec.MemoryId,
                            tier = ((AmeMemoryTier)rec.Tier).ToString(),
                            importance = rec.Importance,
                            confidence = rec.Confidence,
                            decayRate = rec.DecayRate,
                            retention = retention,
                            accessFrequency = rec.AccessFrequency,
                            createdTimestamp = rec.CreatedTimestamp,
                            lastAccessedTimestamp = rec.LastAccessedTimestamp,
                            payload = payload
                        });
                    }
                }

                await SendJsonAsync(response, list);
            }
            else if (request.HttpMethod == "POST" && path == "/api/query")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                string body = await reader.ReadToEndAsync();
                var json = JsonNode.Parse(body)?.AsObject();

                string query = json?["query"]?.GetValue<string>() ?? string.Empty;
                uint topK = (uint)(json?["topK"]?.GetValue<int?>() ?? 10);
                float minScore = (float)(json?["minScore"]?.GetValue<double?>() ?? 0.05);

                float[] queryVec = McpServer.CreateDeterministicVector(query, _container.Dimension);
                var results = _container.QueryFused(queryVec, topK: topK, minScore: minScore);

                var list = results.Select(r => new
                {
                    memoryId = r.MemoryId,
                    tier = r.Tier.ToString(),
                    importance = r.Importance,
                    confidence = r.Confidence,
                    compositeScore = r.CompositeScore,
                    similarity = r.VectorSimilarity,
                    retention = r.RecencyRetention,
                    accessFrequency = r.AccessFrequency,
                    payload = r.Payload
                });

                await SendJsonAsync(response, list);
            }
            else if (request.HttpMethod == "POST" && path == "/api/post")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                string body = await reader.ReadToEndAsync();
                var json = JsonNode.Parse(body)?.AsObject();

                string payload = json?["payload"]?.GetValue<string>() ?? string.Empty;
                string tierStr = json?["tier"]?.GetValue<string>() ?? "Episodic";
                byte importance = (byte)(json?["importance"]?.GetValue<int?>() ?? 80);
                byte confidence = (byte)(json?["confidence"]?.GetValue<int?>() ?? 100);

                var tier = Enum.TryParse<AmeMemoryTier>(tierStr, true, out var parsedTier) ? parsedTier : AmeMemoryTier.Episodic;
                float[] embedding = McpServer.CreateDeterministicVector(payload, _container.Dimension);

                uint id = _container.AppendRecord(
                    tier,
                    payload,
                    embedding,
                    importance: importance,
                    confidence: confidence,
                    decayRate: (byte)(tier == AmeMemoryTier.Semantic ? 0 : 128));

                await SendJsonAsync(response, new { success = true, memoryId = id });
            }
            else if (request.HttpMethod == "POST" && path == "/api/touch")
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                string body = await reader.ReadToEndAsync();
                var json = JsonNode.Parse(body)?.AsObject();

                uint memoryId = (uint)(json?["memoryId"]?.GetValue<int?>() ?? 0);
                bool ok = _container.TouchCognitiveInPlace(memoryId, incrementAccessCount: true);

                await SendJsonAsync(response, new { success = ok });
            }
            else if (request.HttpMethod == "POST" && path == "/api/consolidate")
            {
                var worker = new ConsolidationWorker(_container);
                var report = worker.ExecuteSweep();
                await SendJsonAsync(response, report);
            }
            else
            {
                response.StatusCode = 404;
            }
        }
        catch (Exception ex)
        {
            response.StatusCode = 500;
            byte[] errorBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
            response.ContentType = "application/json";
            await response.OutputStream.WriteAsync(errorBytes);
        }
        finally
        {
            response.Close();
        }
    }

    private static async Task SendJsonAsync(HttpResponse response, object data)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true });
        response.ContentType = "application/json; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }
}
