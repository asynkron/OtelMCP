using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Xunit;

namespace Asynkron.OtelReceiver.Tests;

[Collection("GrpcIntegration")]
public class McpStreamingHttpTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions CommandSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly OtelReceiverApplicationFactory _factory;

    static McpStreamingHttpTests()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public McpStreamingHttpTests(OtelReceiverApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task StreamingEndpoint_EmitsResultsForTraceLensCommands()
    {
        using var channel = _factory.CreateGrpcChannel();
        var traceClient = new TraceService.TraceServiceClient(channel);
        var logsClient = new LogsService.LogsServiceClient(channel);

        var traceIdBytes = Enumerable.Range(25, 16).Select(i => (byte)i).ToArray();
        var spanIdBytes = Enumerable.Range(50, 8).Select(i => (byte)i).ToArray();
        var traceIdHex = Convert.ToHexString(traceIdBytes);

        var traceRequest = new ExportTraceServiceRequest
        {
            ResourceSpans =
            {
                new ResourceSpans
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = "mcp-service" }
                            }
                        }
                    },
                    ScopeSpans =
                    {
                        new ScopeSpans
                        {
                            Spans =
                            {
                                new Span
                                {
                                    TraceId = ByteString.CopyFrom(traceIdBytes),
                                    SpanId = ByteString.CopyFrom(spanIdBytes),
                                    Name = "mcp-operation",
                                    StartTimeUnixNano = 10_000,
                                    EndTimeUnixNano = 20_000,
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "http.method",
                                            Value = new AnyValue { StringValue = "POST" }
                                        },
                                        new KeyValue
                                        {
                                            Key = "status.code",
                                            Value = new AnyValue { StringValue = "STATUS_CODE_OK" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        await traceClient.ExportAsync(traceRequest);

        var logRequest = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                new ResourceLogs
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = "mcp-service" }
                            }
                        }
                    },
                    ScopeLogs =
                    {
                        new ScopeLogs
                        {
                            LogRecords =
                            {
                                new LogRecord
                                {
                                    TimeUnixNano = 15_000,
                                    TraceId = ByteString.CopyFrom(traceIdBytes),
                                    SpanId = ByteString.CopyFrom(spanIdBytes),
                                    Body = new AnyValue { StringValue = "mcp search completed" },
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "http.method",
                                            Value = new AnyValue { StringValue = "POST" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        await logsClient.ExportAsync(logRequest);

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.TraceId == traceIdHex)),
            "trace to be queryable");

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Logs.AnyAsync(log => log.TraceId == traceIdHex)),
            "log to be queryable");

        var metadataKey = "mcp-service:mcp-service";

        var commands = new[]
        {
            SerializeCommand("1", "getSearchData", new { }),
            SerializeCommand("2", "searchTraces", new
            {
                serviceName = "mcp-service",
                tagName = "http.method",
                tagValue = "POST",
                limit = 5
            }),
            SerializeCommand("3", "setComponentMetadata", new
            {
                namePath = metadataKey,
                annotations = "mcp primary"
            }),
            SerializeCommand("4", "getMetadataForComponent", new
            {
                componentId = metadataKey
            })
        };

        var requestBody = string.Join("\n", commands);

        var client = _factory.CreateDefaultClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp/stream")
        {
            Content = new StringContent(requestBody, Encoding.UTF8, "application/x-ndjson")
        };

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(responseStream, Encoding.UTF8);

        var handshake = await ReadEnvelopeAsync(reader);
        Assert.Equal("ready", handshake.GetProperty("type").GetString());
        var handshakeCommands = handshake.GetProperty("commands")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        Assert.Contains("getSearchData", handshakeCommands);
        Assert.Contains("getMetric", handshakeCommands);

        var searchDataEnvelope = await ReadEnvelopeAsync(reader);
        Assert.Equal("1", searchDataEnvelope.GetProperty("id").GetString());
        Assert.Equal("result", searchDataEnvelope.GetProperty("type").GetString());
        var searchDataResult = searchDataEnvelope.GetProperty("result");
        Assert.Contains("mcp-service", searchDataResult.GetProperty("serviceNames")
            .EnumerateArray()
            .Select(element => element.GetString()));
        Assert.Contains("mcp-operation", searchDataResult.GetProperty("spanNames")
            .EnumerateArray()
            .Select(element => element.GetString()));

        var searchEnvelope = await ReadEnvelopeAsync(reader);
        Assert.Equal("2", searchEnvelope.GetProperty("id").GetString());
        var searchResult = searchEnvelope.GetProperty("result");
        var traceEntry = Assert.Single(searchResult.GetProperty("results").EnumerateArray());
        var trace = traceEntry.GetProperty("trace");
        Assert.Equal(traceIdHex, trace.GetProperty("traceId").GetString());
        Assert.NotEmpty(trace.GetProperty("spans").EnumerateArray());
        Assert.NotEmpty(traceEntry.GetProperty("logs").EnumerateArray());

        var metadataAck = await ReadEnvelopeAsync(reader);
        Assert.Equal("3", metadataAck.GetProperty("id").GetString());
        Assert.Equal("result", metadataAck.GetProperty("type").GetString());

        var metadataEnvelope = await ReadEnvelopeAsync(reader);
        Assert.Equal("4", metadataEnvelope.GetProperty("id").GetString());
        var metadataResult = metadataEnvelope.GetProperty("result");
        Assert.Equal("mcp-service", metadataResult.GetProperty("groupName").GetString());
        Assert.Equal("mcp-service", metadataResult.GetProperty("componentName").GetString());
        Assert.Equal("Service", metadataResult.GetProperty("componentKind").GetString());
        Assert.Equal("mcp primary", metadataResult.GetProperty("annotation").GetString());
    }

    private static string SerializeCommand(string id, string command, object payload)
        => JsonSerializer.Serialize(new { id, command, payload }, CommandSerializerOptions);

    private static async Task<JsonElement> ReadEnvelopeAsync(StreamReader reader)
    {
        var line = await reader.ReadLineAsync();
        Assert.False(string.IsNullOrWhiteSpace(line), "Expected MCP response line");
        using var document = JsonDocument.Parse(line!);
        return document.RootElement.Clone();
    }

    private static async Task WaitForAsync(Func<Task<bool>> predicate, string failureMessage)
    {
        var timeoutAt = DateTime.UtcNow + DefaultTimeout;
        Exception? lastException = null;

        while (DateTime.UtcNow < timeoutAt)
        {
            try
            {
                if (await predicate())
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Timed out waiting for {failureMessage}. Last exception: {lastException?.Message}");
    }
}
