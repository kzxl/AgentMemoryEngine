using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Storage;

namespace AgentMemoryEngine.Cli;

/// <summary>
/// High-speed local IPC Server using Named Pipes (Windows) and Unix Domain Sockets (Linux).
/// Enables multiple concurrent AI subagents to share a single in-memory database with zero network overhead.
/// </summary>
public sealed class IpcServer
{
    private readonly AmeContainer _container;
    private readonly string _pipeName;

    public IpcServer(AmeContainer container, string pipeName = "ame_pipe")
    {
        _container = container;
        _pipeName = pipeName;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        Console.WriteLine($"[AME IPC] Listening on Named Pipe: \\\\.\\pipe\\{_pipeName}...");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipeServer = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await pipeServer.WaitForConnectionAsync(ct);
                _ = HandleClientAsync(pipeServer, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[AME IPC Server Error] {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        using (pipe)
        using (var reader = new StreamReader(pipe, Encoding.UTF8))
        using (var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true })
        {
            while (pipe.IsConnected && !ct.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line == null) break;

                try
                {
                    var request = JsonNode.Parse(line)?.AsObject();
                    if (request == null) continue;

                    string command = request["cmd"]?.GetValue<string>()?.ToLowerInvariant() ?? string.Empty;
                    var response = new JsonObject();

                    switch (command)
                    {
                        case "query":
                            string qText = request["query"]?.GetValue<string>() ?? string.Empty;
                            uint topK = (uint)(request["topK"]?.GetValue<int?>() ?? 5);
                            float minScore = (float)(request["minScore"]?.GetValue<double?>() ?? 0.1);

                            float[] qVec = McpServer.CreateDeterministicVector(qText, _container.Dimension);
                            var results = _container.QueryFused(qVec, topK: topK, minScore: minScore);

                            var array = new JsonArray();
                            foreach (var r in results)
                            {
                                array.Add(new JsonObject
                                {
                                    ["id"] = r.MemoryId,
                                    ["tier"] = r.Tier.ToString(),
                                    ["score"] = r.CompositeScore,
                                    ["payload"] = r.Payload
                                });
                            }
                            response["status"] = "ok";
                            response["results"] = array;
                            break;

                        case "post":
                            string payload = request["payload"]?.GetValue<string>() ?? string.Empty;
                            string tierStr = request["tier"]?.GetValue<string>() ?? "Episodic";
                            byte imp = (byte)(request["importance"]?.GetValue<int?>() ?? 80);
                            byte conf = (byte)(request["confidence"]?.GetValue<int?>() ?? 100);

                            var tier = Enum.TryParse<AmeMemoryTier>(tierStr, true, out var t) ? t : AmeMemoryTier.Episodic;
                            float[] emb = McpServer.CreateDeterministicVector(payload, _container.Dimension);
                            uint newId = _container.AppendRecord(tier, payload, emb, imp, conf);

                            response["status"] = "ok";
                            response["memoryId"] = newId;
                            break;

                        case "ping":
                            response["status"] = "pong";
                            response["recordCount"] = _container.RecordCount;
                            break;

                        default:
                            response["status"] = "error";
                            response["message"] = $"Unknown IPC command: {command}";
                            break;
                    }

                    await writer.WriteLineAsync(response.ToJsonString());
                }
                catch (Exception ex)
                {
                    var errObj = new JsonObject
                    {
                        ["status"] = "error",
                        ["message"] = ex.Message
                    };
                    await writer.WriteLineAsync(errObj.ToJsonString());
                }
            }
        }
    }
}
