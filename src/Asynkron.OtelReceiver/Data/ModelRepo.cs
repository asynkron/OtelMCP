using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asynkron.OtelReceiver.Data.Providers;
using Asynkron.OtelReceiver.Monitoring;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Proto.Logs.V1;
using TraceLens.Infra;
using Tracelens.Proto.V1;
using Metric = OpenTelemetry.Proto.Metrics.V1.Metric;

namespace Asynkron.OtelReceiver.Data;

public class ModelRepo(
    IDbContextFactory<OtelReceiverContext> contextFactory,
    ILogger<ModelRepo> logger,
    ISpanBulkInserter spanBulkInserter,
    IReceiverMetricsCollector metricsCollector)
{
    private static readonly HashSet<string> BlockedAttributes =
    [
        "proto.actorpid",
        "proto.senderpid",
        "proto.targetpid"
    ];

    private static readonly HashSet<SpanAttributeEntity> SeenAttributes = [];
    private static readonly HashSet<SpanNameEntity> SeenSpanNames = [];

    private static string GetTraceId(string s)
    {
        if (!s.Contains("==")) return s.ToUpperInvariant();

        var x = ByteString.FromBase64(s);
        return x.ToHex();
    }

    public async Task SaveTrace(SpanWithService[] chunk)
    {
        logger.LogInformation("Before save spans");
        var spanNames = new HashSet<SpanNameEntity>();
        var attributes = new HashSet<SpanAttributeEntity>();
        await using var context = await contextFactory.CreateDbContextAsync();

        var spans = new List<SpanEntity>();
        foreach (var s in chunk)
        {
            var span = new SpanEntity
            {
                TraceId = s.Span.TraceId.ToHex(),
                SpanId = s.Span.SpanId.ToHex(),
                ParentSpanId = s.Span.ParentSpanId.ToHex(),
                ServiceName = s.ServiceName,
                OperationName = s.Span.Name,
                StartTimestamp = (long)s.Span.StartTimeUnixNano,
                EndTimestamp = (long)s.Span.EndTimeUnixNano,
                AttributeMap = s.Span.Attributes
                    .Select(kvp => $"{kvp.Key}:{kvp.Value.ToStringValue()}").ToArray(),
                Events = s.Span.Events.Select(e => e.Name).ToArray(),
                Proto = s.ToByteArray(),
            };
            spans.Add(span);

            foreach (var a in s.Span.Attributes)
            {
                if (BlockedAttributes.Contains(a.Key)) continue;

                var spanAttrib = new SpanAttributeEntity
                {
                    Key = a.Key,
                    Value = a.Value.ToStringValue()
                };
                if (SeenAttributes.Add(spanAttrib)) attributes.Add(spanAttrib);
            }

            var spanName = new SpanNameEntity
            {
                ServiceName = s.ServiceName,
                Name = s.Span.Name
            };

            if (SeenSpanNames.Add(spanName)) spanNames.Add(spanName);
        }

        try
        {
            await spanBulkInserter.InsertAsync(context, spans, CancellationToken.None);
            logger.LogInformation("Before save changes");
            await context.SaveChangesAsync();
            logger.LogInformation("After save changes");
            metricsCollector.RecordSpansStored(spans.Count);
        }
        catch (Exception x)
        {
            logger.LogError(x, "Failed to write spans");
        }

        logger.LogInformation("After save spans");

        logger.LogInformation("Before save attributes");
        foreach (var attrib in attributes)
            try
            {
                //insert using raw sql instead
                await context.Database.ExecuteSqlRawAsync(
                    """INSERT INTO "SpanAttributes" ("Key", "Value") VALUES (@p0, @p1) ON CONFLICT DO NOTHING""",
                    attrib.Key, attrib.Value);
            }
            catch (Exception x)
            {
                logger.LogError(x, "Failed to write attributes");
            }

        logger.LogInformation("After save attributes");

        logger.LogInformation("Before save span names");
        foreach (var spanName in spanNames)
            try
            {
                //insert using raw sql instead
                await context.Database.ExecuteSqlRawAsync(
                    """INSERT INTO "SpanNames" ("ServiceName", "Name") VALUES (@p0, @p1) ON CONFLICT DO NOTHING""",
                    spanName.ServiceName, spanName.Name);
            }
            catch (Exception x)
            {
                logger.LogError(x, "Failed to write span names");
            }

        logger.LogInformation("After save span names");


    }

    public async Task SaveLogs((LogRecord log, ResourceLogs resourceLog)[] chunk)
    {
        logger.LogInformation("Starting to save logs");
        await using var context = await contextFactory.CreateDbContextAsync();
        var logs = new List<LogEntity>();
        foreach (var t in chunk)
        {
            var l = t.log;

            //TODO: add formatted body
            var r = t.resourceLog;
            var log = new LogEntity
            {
                TraceId = l.TraceId.ToHex(),
                SpanId = l.SpanId.ToHex(),
                Timestamp = (long)l.TimeUnixNano,
                ObservedTimestamp = (long)l.ObservedTimeUnixNano,
                AttributeMap = l.Attributes.Select(x => $"{x.Key}:{x.Value.ToStringValue()}")
                    .ToArray(),
                SeverityText = l.SeverityText,
                SeverityNumber = (byte)l.SeverityNumber,
                Body = FormatLog(l),
                RawBody = l.Body.StringValue,
                ResourceMap = r.Resource.Attributes
                    .Select(x => $"{x.Key}:{x.Value.ToStringValue()}").ToArray(),
                Proto = l.ToByteArray(),
            };
            logs.Add(log);
        }

        try
        {
            logger.LogInformation("Before inserting logs");
            await context.Logs.AddRangeAsync(logs);
            await context.SaveChangesAsync();
            logger.LogInformation("After inserting logs");
            metricsCollector.RecordLogsStored(logs.Count);
        }
        catch (Exception x)
        {
            logger.LogError(x, "Failed to write logs");
        }

        logger.LogInformation("Done saving logs");
    }

    private static string FormatLog(LogRecord l)
    {
        var str = l.Body.StringValue;
        if (!string.IsNullOrEmpty(str))
            return str;

        var attributes = l.Attributes.ToDictionary(
            x => x.Key,
            x => x.Value.ToStringValue());

        // Joining attributes makes it easier to inspect ad-hoc log payloads when
        // the body field is empty. This keeps the previous "key:value" format.
        return string.Join(", ", attributes.Select(kvp => $"{kvp.Key}:{kvp.Value}"));
    }

    public async Task SaveSnapshot(SaveSnapshotRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var snapshot = new SnapshotEntity
        {
            Proto = request.Model.ToByteArray()
        };

        await context.Snapshots.AddAsync(snapshot);
        await context.SaveChangesAsync();
    }

    public async Task<GetSnapshotResponse> GetSnapshot(GetSnapshotRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var snapshot = await context.Snapshots.FindAsync(request.Id);
        return new GetSnapshotResponse
        {
            Model = snapshot is null ? null : TraceLensModel.Parser.ParseFrom(snapshot.Proto)
        };
    }

    public async Task<ListSnapshotsResponse> ListSnapshots()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var snapshots = await context.Snapshots.ToListAsync();

        var response = new ListSnapshotsResponse();
        response.Snapshots.AddRange(snapshots.Select(s => new Snapshot
        {
            Id = s.Id,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Traces = TraceLensModel.Parser.ParseFrom(s.Proto)
        }));

        return response;
    }

    public async Task<SetComponentMetadataResponse> SetComponentMetadata(SetComponentMetadataRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var existing = await context.ComponentMetaData
            .SingleOrDefaultAsync(x => x.NamePath == request.NamePath);

        if (existing == null)
        {
            var entity = new ComponentMetadataEntity
            {
                NamePath = request.NamePath,
                Annotation = request.Annotations
            };

            await context.ComponentMetaData.AddAsync(entity);
        }
        else
        {
            existing.Annotation = request.Annotations;
            context.ComponentMetaData.Update(existing);
        }

        try
        {
            await context.SaveChangesAsync();
        }
        catch (Exception x)
        {
            logger.LogError(x, "Failed to write metadata");
        }

        return new SetComponentMetadataResponse();
    }

    public async Task SaveMetrics(MetricEntity[] chunk)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        await context.Metrics.AddRangeAsync(chunk);
        await context.SaveChangesAsync();
        logger.LogInformation("Saving metrics {Size}", chunk.Length);
        metricsCollector.RecordMetricsStored(chunk.Length);
    }

    public async Task<GetMetricNamesResponse> GetMetricNames(GetMetricNamesRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var names = await context.Metrics.Select(m => m.Name).Distinct().ToListAsync();

        return new GetMetricNamesResponse()
        {
            Name = { names }
        };
    }

    public async Task<GetMetricResponse> GetMetric(GetMetricRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var data = await context.Metrics
            .Where(m => m.Name == request.Name).ToListAsync();

        var protos = data.Select(m => Metric.Parser.ParseFrom(m.Proto)).ToList();

        return new GetMetricResponse()
        {
            Metrics = { protos }
        };
    }
}
