using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Tracelens.Proto.V1;
using Xunit;

namespace Asynkron.OtelReceiver.Tests;

[Collection("GrpcIntegration")]
public class DataServiceTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private readonly OtelReceiverApplicationFactory _factory;

    static DataServiceTests()
    {
        // Allow plaintext HTTP/2 during in-process gRPC tests.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public DataServiceTests(OtelReceiverApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DataService_ExposesSearchAndMetadataOperations()
    {
        using var channel = _factory.CreateGrpcChannel();
        var traceClient = new TraceService.TraceServiceClient(channel);
        var logsClient = new LogsService.LogsServiceClient(channel);
        var dataClient = new DataService.DataServiceClient(channel);

        var traceIdBytes = Enumerable.Range(10, 16).Select(i => (byte)i).ToArray();
        var spanIdBytes = Enumerable.Range(1, 8).Select(i => (byte)(i + 40)).ToArray();
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
                                Value = new AnyValue { StringValue = "search-service" }
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
                                    Name = "root-operation",
                                    StartTimeUnixNano = 1_000,
                                    EndTimeUnixNano = 2_000,
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "http.method",
                                            Value = new AnyValue { StringValue = "GET" }
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
                                Value = new AnyValue { StringValue = "search-service" }
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
                                    TimeUnixNano = 1_500,
                                    TraceId = ByteString.CopyFrom(traceIdBytes),
                                    SpanId = ByteString.CopyFrom(spanIdBytes),
                                    Body = new AnyValue { StringValue = "search completed" },
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "http.method",
                                            Value = new AnyValue { StringValue = "GET" }
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

        var searchData = await dataClient.GetSearchDataAsync(new GetSearchDataRequest());
        Assert.Contains("search-service", searchData.ServiceNames);
        Assert.Contains("root-operation", searchData.SpanNames);
        Assert.Contains("http.method", searchData.TagNames);

        var tagValues = await dataClient.GetValuesForTagAsync(new GetValuesForTagRequest { TagName = "http.method" });
        Assert.Contains("GET", tagValues.TagValues);

        var searchResponse = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            ServiceName = "search-service",
            TagName = "http.method",
            TagValue = "GET",
            Limit = 5
        });

        var traceResult = Assert.Single(searchResponse.Results);
        Assert.Equal(traceIdHex, traceResult.Trace.TraceId);
        Assert.NotEmpty(traceResult.Trace.Spans);
        Assert.All(traceResult.Trace.Spans, span => Assert.Equal("search-service", span.ServiceName));
        Assert.NotEmpty(searchResponse.LogCounts);
        Assert.NotEmpty(searchResponse.SpanCounts);
        Assert.Single(traceResult.Logs);

        var serviceMap = await dataClient.GetServiceMapComponentsAsync(new GetServiceMapComponentsRequest());
        Assert.Contains(serviceMap.Components, component => component.ComponentName == "search-service");

        const string metadataKey = "search-service:search-service";
        await dataClient.SetComponentMetadataAsync(new SetComponentMetadataRequest
        {
            NamePath = metadataKey,
            Annotations = "primary service"
        });

        var metadataList = await dataClient.GetComponentMetadataAsync(new GetComponentMetadataRequest());
        Assert.Contains(metadataList.ComponentMetadata,
            entry => entry.NamePath == metadataKey && entry.Annotations == "primary service");

        var metadata = await dataClient.GetMetadataForComponentAsync(new GetMetadataForComponentRequest
        {
            ComponentId = metadataKey
        });

        Assert.Equal("primary service", metadata.Annotation);
        Assert.Equal("search-service", metadata.GroupName);
        Assert.Equal("search-service", metadata.ComponentName);
        Assert.Equal("Service", metadata.ComponentKind);
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
