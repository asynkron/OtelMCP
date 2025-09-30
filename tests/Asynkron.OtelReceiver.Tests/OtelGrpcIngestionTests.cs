using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Asynkron.OtelReceiver;
using Asynkron.OtelReceiver.Data;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;
using Xunit;

namespace Asynkron.OtelReceiver.Tests;

public class OtelReceiverApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"otel-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Force the application under test to use a dedicated SQLite file per run.
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}");
    }

    public GrpcChannel CreateGrpcChannel()
    {
        var httpClient = CreateDefaultClient();
        httpClient.DefaultRequestVersion = HttpVersion.Version20;
        httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;

        return GrpcChannel.ForAddress(httpClient.BaseAddress ?? new Uri("http://localhost"), new GrpcChannelOptions
        {
            HttpClient = httpClient
        });
    }

    public async Task<T> ExecuteDbContextAsync<T>(Func<OtelReceiverContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OtelReceiverContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        return await action(context);
    }

    public Task ExecuteDbContextAsync(Func<OtelReceiverContext, Task> action)
        => ExecuteDbContextAsync(async context =>
        {
            await action(context);
            return true;
        });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            try
            {
                if (File.Exists(_databasePath))
                {
                    File.Delete(_databasePath);
                }
            }
            catch
            {
                // The temporary file is best-effort cleanup; swallow IO errors so tests still complete.
            }
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OtelReceiverContext>>();
        using var context = contextFactory.CreateDbContext();
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();

        return host;
    }
}

[Collection("GrpcIntegration")]
public class OtelGrpcIngestionTests
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);
    private readonly OtelReceiverApplicationFactory _factory;

    static OtelGrpcIngestionTests()
    {
        // Allow plaintext HTTP/2 so the in-process gRPC client can talk to TestServer.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public OtelGrpcIngestionTests(OtelReceiverApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ExportTrace_PersistsSpans()
    {
        using var channel = _factory.CreateGrpcChannel();
        var client = new TraceService.TraceServiceClient(channel);

        var traceIdBytes = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
        var spanIdBytes = Enumerable.Range(51, 8).Select(i => (byte)i).ToArray();
        var spanIdHex = Convert.ToHexString(spanIdBytes);

        var request = new ExportTraceServiceRequest
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
                                Value = new AnyValue { StringValue = "integration-service" }
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
                                    Name = "integration-span",
                                    StartTimeUnixNano = 100,
                                    EndTimeUnixNano = 200,
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

        await client.ExportAsync(request);

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Spans.AnyAsync(span => span.SpanId == spanIdHex)),
            "span to be persisted");

        var storedSpan = await _factory.ExecuteDbContextAsync(context =>
            context.Spans.SingleAsync(span => span.SpanId == spanIdHex));
        Assert.Equal(Convert.ToHexString(traceIdBytes), storedSpan.TraceId);
        Assert.Equal("integration-service", storedSpan.ServiceName);
        Assert.Equal("integration-span", storedSpan.OperationName);
    }

    [Fact]
    public async Task ExportLogs_PersistsLogRecords()
    {
        using var channel = _factory.CreateGrpcChannel();
        var client = new LogsService.LogsServiceClient(channel);

        var traceIdBytes = Enumerable.Range(11, 16).Select(i => (byte)i).ToArray();
        var spanIdBytes = Enumerable.Range(3, 8).Select(i => (byte)i).ToArray();

        var request = new ExportLogsServiceRequest
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
                                Value = new AnyValue { StringValue = "logging-service" }
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
                                    TimeUnixNano = 300,
                                    ObservedTimeUnixNano = 300,
                                    TraceId = ByteString.CopyFrom(traceIdBytes),
                                    SpanId = ByteString.CopyFrom(spanIdBytes),
                                    Body = new AnyValue { StringValue = "integration log" },
                                    SeverityText = "INFO",
                                    SeverityNumber = (SeverityNumber)9,
                                    Attributes =
                                    {
                                        new KeyValue
                                        {
                                            Key = "env",
                                            Value = new AnyValue { StringValue = "test" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        await client.ExportAsync(request);

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Logs.AnyAsync(log => log.RawBody == "integration log")),
            "log record to be persisted");

        var storedLog = await _factory.ExecuteDbContextAsync(context =>
            context.Logs.Include(log => log.Attributes)
                .SingleAsync(log => log.RawBody == "integration log"));
        Assert.Equal(Convert.ToHexString(traceIdBytes), storedLog.TraceId);
        Assert.Equal("integration log", storedLog.RawBody);
        Assert.Contains(storedLog.Attributes,
            attribute => attribute.Key == "env" && attribute.Value == "test");
    }

    [Fact]
    public async Task ExportMetrics_PersistsMetricEntities()
    {
        using var channel = _factory.CreateGrpcChannel();
        var client = new MetricsService.MetricsServiceClient(channel);

        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = "metrics-service" }
                            }
                        }
                    },
                    ScopeMetrics =
                    {
                        new ScopeMetrics
                        {
                            Scope = new InstrumentationScope
                            {
                                Name = "integration-tests",
                                Version = "1.0.0",
                                Attributes =
                                {
                                    new KeyValue
                                    {
                                        Key = "scope.label",
                                        Value = new AnyValue { StringValue = "grpc" }
                                    }
                                }
                            },
                            Metrics =
                            {
                                new Metric
                                {
                                    Name = "integration_metric",
                                    Description = "integration test metric",
                                    Unit = "1",
                                    Gauge = new Gauge
                                    {
                                        DataPoints =
                                        {
                                            new NumberDataPoint
                                            {
                                                StartTimeUnixNano = 100UL,
                                                TimeUnixNano = 500UL,
                                                AsDouble = 42.0,
                                                Attributes =
                                                {
                                                    new KeyValue
                                                    {
                                                        Key = "stage",
                                                        Value = new AnyValue { StringValue = "test" }
                                                    }
                                                }
                                            },
                                            new NumberDataPoint
                                            {
                                                StartTimeUnixNano = 400UL,
                                                TimeUnixNano = 900UL,
                                                AsDouble = 43.0,
                                                Attributes =
                                                {
                                                    new KeyValue
                                                    {
                                                        Key = "stage",
                                                        Value = new AnyValue { StringValue = "test" }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        try
        {
            await client.ExportAsync(request);
        }
        catch (RpcException ex)
        {
            throw new Xunit.Sdk.XunitException($"Metric export failed: {ex.Status.Detail}\n{ex.Status.DebugException}");
        }

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Metrics.AnyAsync(metric => metric.Name == "integration_metric")),
            "metric to be persisted");

        var storedMetric = await _factory.ExecuteDbContextAsync(context =>
            context.Metrics.SingleAsync(metric => metric.Name == "integration_metric"));
        Assert.Equal("integration_metric", storedMetric.Name);
        Assert.Contains("scope.label:grpc", storedMetric.AttributeMap);
        Assert.Contains("service.name:metrics-service", storedMetric.AttributeMap);
        // Start and end timestamps should mirror the earliest and latest data point samples.
        Assert.Equal(500UL, storedMetric.StartTimestamp);
        Assert.Equal(900UL, storedMetric.EndTimestamp);
    }

    [Fact]
    public async Task ExportMetrics_WithMultipleResourcePayloads_PersistsSingleRowPerMetricName()
    {
        using var channel = _factory.CreateGrpcChannel();
        var client = new MetricsService.MetricsServiceClient(channel);

        const string metricName = "multi_resource_metric";

        var request = new ExportMetricsServiceRequest
        {
            ResourceMetrics =
            {
                new ResourceMetrics
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = "metrics-service" }
                            }
                        }
                    },
                    ScopeMetrics =
                    {
                        new ScopeMetrics
                        {
                            Scope = new InstrumentationScope
                            {
                                Name = "integration-tests",
                                Version = "1.0.0"
                            },
                            Metrics =
                            {
                                new Metric
                                {
                                    Name = metricName,
                                    Description = "metric that should only persist once",
                                    Unit = "1",
                                    Gauge = new Gauge
                                    {
                                        DataPoints =
                                        {
                                            new NumberDataPoint
                                            {
                                                TimeUnixNano = 600,
                                                AsDouble = 1.23
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                new ResourceMetrics
                {
                    Resource = new Resource
                    {
                        Attributes =
                        {
                            new KeyValue
                            {
                                Key = "service.name",
                                Value = new AnyValue { StringValue = "metrics-service" }
                            }
                        }
                    },
                    ScopeMetrics =
                    {
                        // The bug repro: a subsequent ResourceMetrics without new metrics should not
                        // cause previously buffered metrics to be written again.
                        new ScopeMetrics
                        {
                            Scope = new InstrumentationScope
                            {
                                Name = "integration-tests",
                                Version = "1.0.0"
                            }
                        }
                    }
                }
            }
        };

        await client.ExportAsync(request);

        await WaitForAsync(async () => await _factory.ExecuteDbContextAsync(context =>
                context.Metrics.AnyAsync(metric => metric.Name == metricName)),
            "metric to be persisted once");

        var storedMetrics = await _factory.ExecuteDbContextAsync(context =>
            context.Metrics.Where(metric => metric.Name == metricName).ToListAsync());

        var metricEntity = Assert.Single(storedMetrics);
        Assert.Equal(metricName, metricEntity.Name);
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
