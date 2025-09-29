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
        var spanIdHex = Convert.ToHexString(spanIdBytes);

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
            Limit = 5,
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter { Name = "search-service" }
                        },
                        new TraceFilterExpression
                        {
                            Attribute = new AttributeFilter
                            {
                                Key = "http.method",
                                Value = "GET",
                                Operator = AttributeFilterOperator.Equals,
                                Target = AttributeFilterTarget.Span
                            }
                        }
                    }
                }
            }
        });

        var traceResult = Assert.Single(searchResponse.Results);
        Assert.Equal(traceIdHex, traceResult.Trace.TraceId);
        Assert.NotEmpty(traceResult.Trace.Spans);
        Assert.All(traceResult.Trace.Spans, span => Assert.Equal("search-service", span.ServiceName));
        var clause = Assert.Single(traceResult.AttributeClauses);
        Assert.True(clause.Satisfied);
        Assert.Equal("tag:http.method=GET", clause.Clause);
        var match = Assert.Single(clause.Matches);
        Assert.Equal(spanIdHex, match.SpanId);
        Assert.Equal("http.method", match.Key);
        Assert.Equal("GET", match.Value);
        Assert.NotEmpty(traceResult.Spans);
        Assert.Equal("root-operation", traceResult.Spans[0].Name);
        Assert.Equal(traceIdBytes, traceResult.Spans[0].TraceId.ToByteArray());
        Assert.Equal(spanIdBytes, traceResult.Spans[0].SpanId.ToByteArray());
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

    [Fact]
    public async Task SearchTraces_SupportsCompositeAttributeFilters()
    {
        using var channel = _factory.CreateGrpcChannel();
        var traceClient = new TraceService.TraceServiceClient(channel);
        var dataClient = new DataService.DataServiceClient(channel);

        var traces = new[]
        {
            new
            {
                TraceIdBytes = Enumerable.Range(60, 16).Select(i => (byte)i).ToArray(),
                SpanIdBytes = Enumerable.Range(120, 8).Select(i => (byte)i).ToArray(),
                Method = "GET",
                Status = "200"
            },
            new
            {
                TraceIdBytes = Enumerable.Range(90, 16).Select(i => (byte)i).ToArray(),
                SpanIdBytes = Enumerable.Range(160, 8).Select(i => (byte)i).ToArray(),
                Method = "POST",
                Status = "500"
            }
        };

        var traceRequest = new ExportTraceServiceRequest();
        foreach (var trace in traces)
        {
            traceRequest.ResourceSpans.Add(new ResourceSpans
            {
                Resource = new Resource
                {
                    Attributes =
                    {
                        new KeyValue
                        {
                            Key = "service.name",
                            Value = new AnyValue { StringValue = "filter-service" }
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
                                TraceId = ByteString.CopyFrom(trace.TraceIdBytes),
                                SpanId = ByteString.CopyFrom(trace.SpanIdBytes),
                                Name = $"{trace.Method}-operation",
                                StartTimeUnixNano = 10_000,
                                EndTimeUnixNano = 20_000,
                                Attributes =
                                {
                                    new KeyValue
                                    {
                                        Key = "http.method",
                                        Value = new AnyValue { StringValue = trace.Method }
                                    },
                                    new KeyValue
                                    {
                                        Key = "http.status_code",
                                        Value = new AnyValue { StringValue = trace.Status }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        await traceClient.ExportAsync(traceRequest);

        var traceIds = traces
            .Select(trace => Convert.ToHexString(trace.TraceIdBytes))
            .ToArray();

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(async context =>
                (await context.Spans.CountAsync(span => traceIds.Contains(span.TraceId))) == traceIds.Length),
            "composite-filter traces to be queryable");

        var orFilter = new TraceFilterExpression
        {
            Composite = new TraceFilterComposite
            {
                Operator = TraceFilterComposite.Types.Operator.And,
                Expressions =
                {
                    new TraceFilterExpression
                    {
                        Service = new ServiceFilter { Name = "filter-service" }
                    },
                    new TraceFilterExpression
                    {
                        Composite = new TraceFilterComposite
                        {
                            Operator = TraceFilterComposite.Types.Operator.Or,
                            Expressions =
                            {
                                new TraceFilterExpression
                                {
                                    Attribute = new AttributeFilter
                                    {
                                        Key = "http.method",
                                        Value = "GET",
                                        Operator = AttributeFilterOperator.Equals
                                    }
                                },
                                new TraceFilterExpression
                                {
                                    Attribute = new AttributeFilter
                                    {
                                        Key = "http.method",
                                        Value = "POST",
                                        Operator = AttributeFilterOperator.Equals
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var orResponse = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Limit = 10,
            Filter = orFilter
        });

        Assert.Equal(traceIds.OrderBy(id => id),
            orResponse.Results.Select(result => result.Trace.TraceId).OrderBy(id => id));

        var andFilter = new TraceFilterExpression
        {
            Composite = new TraceFilterComposite
            {
                Operator = TraceFilterComposite.Types.Operator.And,
                Expressions =
                {
                    new TraceFilterExpression
                    {
                        Service = new ServiceFilter { Name = "filter-service" }
                    },
                    new TraceFilterExpression
                    {
                        Composite = new TraceFilterComposite
                        {
                            Operator = TraceFilterComposite.Types.Operator.And,
                            Expressions =
                            {
                                new TraceFilterExpression
                                {
                                    Attribute = new AttributeFilter
                                    {
                                        Key = "http.method",
                                        Value = "GET",
                                        Operator = AttributeFilterOperator.Equals
                                    }
                                },
                                new TraceFilterExpression
                                {
                                    Attribute = new AttributeFilter
                                    {
                                        Key = "http.status_code",
                                        Value = "200",
                                        Operator = AttributeFilterOperator.Equals
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var andResponse = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Limit = 10,
            Filter = andFilter
        });

        var singleTrace = Assert.Single(andResponse.Results);
        Assert.Equal(traceIds[0], singleTrace.Trace.TraceId);
    }

    [Fact]
    public async Task SearchTraces_FiltersLogsByLogAttributes()
    {
        using var channel = _factory.CreateGrpcChannel();
        var traceClient = new TraceService.TraceServiceClient(channel);
        var logsClient = new LogsService.LogsServiceClient(channel);
        var dataClient = new DataService.DataServiceClient(channel);

        var traceIdBytes = Enumerable.Range(200, 16).Select(i => (byte)i).ToArray();
        var spanIdBytes = Enumerable.Range(200, 8).Select(i => (byte)i).ToArray();
        var traceIdHex = Convert.ToHexString(traceIdBytes);
        var spanIdHex = Convert.ToHexString(spanIdBytes);

        // Seed a trace so search responses have span context to hydrate.
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
                                Value = new AnyValue { StringValue = "log-service" }
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
                                    Name = "log-operation",
                                    StartTimeUnixNano = 5_000,
                                    EndTimeUnixNano = 6_000
                                }
                            }
                        }
                    }
                }
            }
        };

        await traceClient.ExportAsync(traceRequest);

        var logsRequest = new ExportLogsServiceRequest
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
                                Value = new AnyValue { StringValue = "log-service" }
                            },
                            new KeyValue
                            {
                                Key = "deployment.environment",
                                Value = new AnyValue { StringValue = "prod" }
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
                                    TimeUnixNano = 5_500,
                                    TraceId = ByteString.CopyFrom(traceIdBytes),
                                    SpanId = ByteString.CopyFrom(spanIdBytes),
                                    Body = new AnyValue { StringValue = "cart log" },
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "http.route",
                                            Value = new AnyValue { StringValue = "/cart" }
                                        },
                                        new KeyValue
                                        {
                                            Key = "log.kind",
                                            Value = new AnyValue { StringValue = "record" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                new ResourceLogs
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = "log-service" }
                            },
                            new KeyValue
                            {
                                Key = "deployment.environment",
                                Value = new AnyValue { StringValue = "qa" }
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
                                    TimeUnixNano = 5_600,
                                    TraceId = ByteString.CopyFrom(traceIdBytes),
                                    SpanId = ByteString.CopyFrom(spanIdBytes),
                                    Body = new AnyValue { StringValue = "checkout log" },
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "http.route",
                                            Value = new AnyValue { StringValue = "/checkout" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        await logsClient.ExportAsync(logsRequest);

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(async context =>
                (await context.Spans.CountAsync(span => span.TraceId == traceIdHex)) > 0),
            "trace to be queryable for log filtering");

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(async context =>
                (await context.Logs.CountAsync(log => log.TraceId == traceIdHex)) == 2),
            "logs to be queryable for attribute filtering");

        var routeFilterResponse = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Limit = 5,
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter { Name = "log-service" }
                        },
                        new TraceFilterExpression
                        {
                            Attribute = new AttributeFilter
                            {
                                Key = "http.route",
                                Value = "/cart",
                                Operator = AttributeFilterOperator.Equals,
                                Target = AttributeFilterTarget.Log
                            }
                        }
                    }
                }
            }
        });

        var routeResult = Assert.Single(routeFilterResponse.Results);
        var routeLog = Assert.Single(routeResult.Logs);
        Assert.Equal("cart log", routeLog.Body.StringValue);
        var routeClause = Assert.Single(routeResult.AttributeClauses);
        Assert.True(routeClause.Satisfied);
        Assert.Equal("log:http.route=/cart", routeClause.Clause);
        var routeMatch = Assert.Single(routeClause.Matches);
        Assert.Equal(spanIdHex, routeMatch.SpanId);

        var resourceFilterResponse = await dataClient.SearchTracesAsync(new SearchTracesRequest
        {
            Limit = 5,
            Filter = new TraceFilterExpression
            {
                Composite = new TraceFilterComposite
                {
                    Operator = TraceFilterComposite.Types.Operator.And,
                    Expressions =
                    {
                        new TraceFilterExpression
                        {
                            Service = new ServiceFilter { Name = "log-service" }
                        },
                        new TraceFilterExpression
                        {
                            Attribute = new AttributeFilter
                            {
                                Key = "deployment.environment",
                                Value = "prod",
                                Operator = AttributeFilterOperator.Equals,
                                Target = AttributeFilterTarget.Log
                            }
                        }
                    }
                }
            }
        });

        var resourceResult = Assert.Single(resourceFilterResponse.Results);
        var resourceLog = Assert.Single(resourceResult.Logs);
        Assert.Equal("cart log", resourceLog.Body.StringValue);
        var resourceClause = Assert.Single(resourceResult.AttributeClauses);
        Assert.True(resourceClause.Satisfied);
        Assert.Equal("log:deployment.environment=prod", resourceClause.Clause);
        var resourceMatch = Assert.Single(resourceClause.Matches);
        Assert.Equal(spanIdHex, resourceMatch.SpanId);
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
