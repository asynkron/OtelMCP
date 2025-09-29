using System.Collections.Generic;
using System.Threading;
using Asynkron.OtelReceiver.Data;
using Asynkron.OtelReceiver.Data.Providers;
using Asynkron.OtelReceiver.Monitoring;
using Google.Protobuf;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Metrics.V1;
using OpenTelemetry.Proto.Trace.V1;
using Tracelens.Proto.V1;
using Xunit;

namespace Asynkron.OtelReceiver.Tests;

public class SqliteSpanBulkInserterTests
{
    [Fact]
    public async Task InsertAsync_PersistsAllSpans()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var inserter = new SqliteSpanBulkInserter();

        var spans = new List<SpanEntity>
        {
            CreateSpanEntity(),
            CreateSpanEntity()
        };

        await inserter.InsertAsync(context, spans);
        await context.SaveChangesAsync();

        await using var verification = database.CreateContext();
        var stored = await verification.Spans.CountAsync();
        Assert.Equal(spans.Count, stored);
    }

    [Fact]
    public async Task SaveTrace_StoresSpansAttributesAndNames()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        using var metrics = new ReceiverMetricsCollector();
        var repo = CreateRepository(database, metrics);

        var span = new Span
        {
            TraceId = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
            SpanId = ByteString.CopyFrom(Guid.NewGuid().ToByteArray().AsSpan(0, 8).ToArray()),
            ParentSpanId = ByteString.CopyFrom(Guid.NewGuid().ToByteArray().AsSpan(0, 8).ToArray()),
            Name = "TestOperation",
            StartTimeUnixNano = 1,
            EndTimeUnixNano = 2,
        };
        span.Attributes.Add(new KeyValue
        {
            Key = "env",
            Value = new AnyValue { StringValue = "local" }
        });

        await repo.SaveTrace(new[]
        {
            new SpanWithService
            {
                ServiceName = "orders",
                Span = span
            }
        });

        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Spans.CountAsync());
        Assert.Equal(1, await verification.SpanAttributes.CountAsync());
        Assert.Equal(1, await verification.SpanNames.CountAsync());
    }

    [Fact]
    public async Task SaveLogsAndMetrics_PersistEntities()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        using var metrics = new ReceiverMetricsCollector();
        var repo = CreateRepository(database, metrics);

        var logRecord = new LogRecord
        {
            TimeUnixNano = 1,
            ObservedTimeUnixNano = 1,
            SpanId = ByteString.CopyFrom(Guid.NewGuid().ToByteArray().AsSpan(0, 8).ToArray()),
            TraceId = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
            Body = new AnyValue { StringValue = "Test" }
        };

        var resourceLogs = new ResourceLogs
        {
            Resource = new OpenTelemetry.Proto.Resource.V1.Resource()
        };

        await repo.SaveLogs(new[] { (logRecord, resourceLogs) });

        var metric = new Metric
        {
            Name = "requests",
            Description = "Number of requests",
            Unit = "1",
            Gauge = new Gauge()
        };

        await repo.SaveMetrics(new[]
        {
            new MetricEntity
            {
                Name = metric.Name,
                Description = metric.Description,
                Unit = metric.Unit,
                Proto = metric.ToByteArray(),
                AttributeMap = Array.Empty<string>()
            }
        });

        await using var verification = database.CreateContext();
        Assert.Equal(1, await verification.Logs.CountAsync());
        Assert.Equal(1, await verification.Metrics.CountAsync());
    }

    private static ModelRepo CreateRepository(SqliteTestDatabase database, ReceiverMetricsCollector metrics)
    {
        var contextFactory = database.CreateFactory();
        return new ModelRepo(contextFactory, NullLogger<ModelRepo>.Instance, new SqliteSpanBulkInserter(), metrics);
    }

    private static SpanEntity CreateSpanEntity() => new()
    {
        SpanId = Guid.NewGuid().ToString("N")[..16],
        TraceId = Guid.NewGuid().ToString("N"),
        ParentSpanId = Guid.NewGuid().ToString("N")[..16],
        OperationName = "op",
        ServiceName = "svc",
        StartTimestamp = 1,
        EndTimestamp = 2,
        AttributeMap = Array.Empty<string>(),
        Events = Array.Empty<string>(),
        Proto = Array.Empty<byte>()
    };

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<OtelReceiverContext> _options;

        private SqliteTestDatabase(SqliteConnection connection, DbContextOptions<OtelReceiverContext> options)
        {
            _connection = connection;
            _options = options;
        }

        public static async Task<SqliteTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<OtelReceiverContext>()
                .UseSqlite(connection)
                .Options;

            await using (var context = new OtelReceiverContext(options))
            {
                await context.Database.EnsureCreatedAsync();
            }

            return new SqliteTestDatabase(connection, options);
        }

        public OtelReceiverContext CreateContext() => new(_options);

        public IDbContextFactory<OtelReceiverContext> CreateFactory() => new TestContextFactory(_options);

        public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    }

    private sealed class TestContextFactory(DbContextOptions<OtelReceiverContext> options) : IDbContextFactory<OtelReceiverContext>
    {
        public Task<OtelReceiverContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());

        public OtelReceiverContext CreateDbContext() => new(options);
    }
}
