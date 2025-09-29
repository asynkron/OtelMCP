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
        var spanAttributeIndexEntries = new List<SpanAttributeIndexEntity>();
        var spanAttributeIndexSeen = new HashSet<(string SpanId, string Key, string Value)>(SpanAttributeIndexComparer.Ordinal);
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

                var attributeValue = a.Value.ToStringValue();

                var spanAttrib = new SpanAttributeEntity
                {
                    Key = a.Key,
                    Value = attributeValue
                };
                if (SeenAttributes.Add(spanAttrib)) attributes.Add(spanAttrib);

                var indexEntry = (span.SpanId, a.Key, attributeValue);
                if (spanAttributeIndexSeen.Add(indexEntry))
                {
                    spanAttributeIndexEntries.Add(new SpanAttributeIndexEntity
                    {
                        SpanId = span.SpanId,
                        Key = a.Key,
                        Value = attributeValue
                    });
                }
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
            if (spanAttributeIndexEntries.Count > 0)
            {
                await context.SpanAttributeIndex.AddRangeAsync(spanAttributeIndexEntries);
            }
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

    public async Task<GetSearchDataResponse> GetSearchData(GetSearchDataRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var serviceNames = await context.Spans
            .Select(span => span.ServiceName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var spanNames = await context.SpanNames
            .Select(span => span.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var tagNames = await context.SpanAttributes
            .Select(attribute => attribute.Key)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var response = new GetSearchDataResponse();
        response.ServiceNames.AddRange(serviceNames);
        response.SpanNames.AddRange(spanNames);
        response.TagNames.AddRange(tagNames);

        return response;
    }

    public async Task<GetValuesForTagResponse> GetValuesForTag(GetValuesForTagRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var values = await context.SpanAttributes
            .Where(attribute => attribute.Key == request.TagName)
            .Select(attribute => attribute.Value)
            .Distinct()
            .OrderBy(value => value)
            .ToListAsync();

        var response = new GetValuesForTagResponse();
        response.TagValues.AddRange(values);

        return response;
    }

    public async Task<SearchTracesResponse> SearchTraces(SearchTracesRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var limit = request.Limit > 0 ? request.Limit : 10;

        var spansQuery = context.Spans.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ServiceName))
        {
            spansQuery = spansQuery.Where(span => span.ServiceName == request.ServiceName);
        }

        if (!string.IsNullOrWhiteSpace(request.SpanName))
        {
            spansQuery = spansQuery.Where(span => span.OperationName == request.SpanName);
        }

        if (request.StartTime != 0)
        {
            spansQuery = spansQuery.Where(span => span.StartTimestamp >= (long)request.StartTime);
        }

        if (request.EndTime != 0)
        {
            spansQuery = spansQuery.Where(span => span.EndTimestamp <= (long)request.EndTime);
        }

        if (!string.IsNullOrWhiteSpace(request.TagName))
        {
            var tagName = request.TagName;
            var tagValue = request.TagValue;

            spansQuery = spansQuery.Where(span => context.SpanAttributeIndex.Any(index =>
                index.SpanId == span.SpanId &&
                index.Key == tagName &&
                (string.IsNullOrWhiteSpace(tagValue) || index.Value == tagValue)));
        }

        var candidates = await spansQuery
            .GroupBy(span => span.TraceId)
            .Select(group => new
            {
                TraceId = group.Key,
                Start = group.Min(span => span.StartTimestamp)
            })
            .OrderByDescending(group => group.Start)
            .Take(limit * 3)
            .ToListAsync();

        if (candidates.Count == 0)
        {
            return new SearchTracesResponse();
        }

        var candidateIds = candidates.Select(group => group.TraceId).ToList();

        var spans = await context.Spans
            .Where(span => candidateIds.Contains(span.TraceId))
            .ToListAsync();

        var logs = await context.Logs
            .Where(log => candidateIds.Contains(log.TraceId))
            .ToListAsync();

        var traceOrder = candidates
            .OrderByDescending(group => group.Start)
            .Select(group => group.TraceId)
            .ToList();

        var response = new SearchTracesResponse();
        var selectedTraceIds = new List<string>();

        foreach (var traceId in traceOrder)
        {
            var traceSpans = spans
                .Where(span => span.TraceId == traceId)
                .OrderBy(span => span.StartTimestamp)
                .ToList();

            if (traceSpans.Count == 0)
            {
                continue;
            }

            selectedTraceIds.Add(traceId);

            var traceLogs = logs
                .Where(log => log.TraceId == traceId)
                .ToList();

            if (!string.IsNullOrWhiteSpace(request.LogSearch))
            {
                traceLogs = traceLogs
                    .Where(log => log.RawBody?.Contains(request.LogSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                    .ToList();
            }

            var overview = new TraceOverview
            {
                TraceId = traceId,
                Name = traceSpans.First().OperationName,
                StartTimeUnixNano = (ulong)traceSpans.Min(span => span.StartTimestamp),
                EndTimeUnixNano = (ulong)traceSpans.Max(span => span.EndTimestamp),
                HasError = traceSpans.Any(SpanHasError)
            };

            overview.Spans.AddRange(traceSpans.Select(span => new SpanOverview
            {
                TraceId = span.TraceId,
                OperationName = span.OperationName,
                ServiceName = span.ServiceName
            }));

            var traceResult = new SearchTraceResult
            {
                Trace = overview
            };

            foreach (var log in traceLogs)
            {
                traceResult.Logs.Add(LogRecord.Parser.ParseFrom(log.Proto));
            }

            response.Results.Add(traceResult);

            if (response.Results.Count == limit)
            {
                break;
            }
        }

        var logCounts = logs
            .Where(log => selectedTraceIds.Contains(log.TraceId))
            .Where(log => string.IsNullOrWhiteSpace(request.LogSearch) ||
                          (log.RawBody?.Contains(request.LogSearch, StringComparison.OrdinalIgnoreCase) ?? false))
            .GroupBy(log => log.RawBody ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        var spanCounts = spans
            .Where(span => selectedTraceIds.Contains(span.TraceId))
            .GroupBy(span => span.OperationName ?? string.Empty)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        response.LogCounts.AddRange(logCounts.Select(kvp => new LogCount
        {
            RawBody = kvp.Key,
            Count = kvp.Value
        }));

        response.SpanCounts.AddRange(spanCounts.Select(kvp => new SpanCount
        {
            SpanName = kvp.Key,
            Count = kvp.Value
        }));

        return response;
    }

    public async Task<GetServiceMapComponentsResponse> GetServiceMapComponents(GetServiceMapComponentsRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var spansQuery = context.Spans.AsQueryable();

        if (request.StartTime != 0)
        {
            spansQuery = spansQuery.Where(span => span.StartTimestamp >= (long)request.StartTime);
        }

        if (request.EndTime != 0)
        {
            spansQuery = spansQuery.Where(span => span.EndTimestamp <= (long)request.EndTime);
        }

        var services = await spansQuery
            .Select(span => span.ServiceName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var response = new GetServiceMapComponentsResponse();
        foreach (var service in services)
        {
            if (string.IsNullOrWhiteSpace(service))
            {
                continue;
            }

            response.Components.Add(new GetServiceMapComponentsResponse.Types.Component
            {
                Id = $"{service}:{service}",
                GroupName = service,
                ComponentName = service,
                ComponentKind = "Service"
            });
        }

        return response;
    }

    public async Task<GetComponentMetadataResponse> GetComponentMetadata(GetComponentMetadataRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var metadata = await context.ComponentMetaData
            .OrderBy(component => component.NamePath)
            .ToListAsync();

        var response = new GetComponentMetadataResponse();
        response.ComponentMetadata.AddRange(metadata.Select(component => new GetComponentMetadataResponse.Types.ComponentMetadata
        {
            NamePath = component.NamePath,
            Annotations = component.Annotation
        }));

        return response;
    }

    public async Task<GetMetadataForComponentResponse> GetMetadataForComponent(GetMetadataForComponentRequest request)
    {
        var (groupName, componentName) = ParseComponentId(request.ComponentId);

        await using var context = await contextFactory.CreateDbContextAsync();

        var key = string.IsNullOrWhiteSpace(groupName) && string.IsNullOrWhiteSpace(componentName)
            ? string.Empty
            : $"{groupName}:{componentName}";

        var annotation = key == string.Empty
            ? string.Empty
            : await context.ComponentMetaData
                .Where(component => component.NamePath == key)
                .Select(component => component.Annotation)
                .FirstOrDefaultAsync();

        return new GetMetadataForComponentResponse
        {
            GroupName = groupName,
            ComponentName = componentName,
            ComponentKind = string.IsNullOrWhiteSpace(groupName) ? string.Empty : "Service",
            Annotation = annotation ?? string.Empty
        };
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

    private static bool SpanHasError(SpanEntity span)
    {
        if (span.AttributeMap is not { Length: > 0 })
        {
            return false;
        }

        return span.AttributeMap.Any(attribute =>
            string.Equals(attribute, "status.code:STATUS_CODE_ERROR", StringComparison.Ordinal) ||
            attribute.Contains("error", StringComparison.OrdinalIgnoreCase));
    }

    private static (string GroupName, string ComponentName) ParseComponentId(string componentId)
    {
        if (string.IsNullOrWhiteSpace(componentId))
        {
            return (string.Empty, string.Empty);
        }

        var parts = componentId.Split(':', 2, StringSplitOptions.TrimEntries);

        return parts.Length == 2
            ? (parts[0], parts[1])
            : (componentId, componentId);
    }

    private sealed class SpanAttributeIndexComparer : IEqualityComparer<(string SpanId, string Key, string Value)>
    {
        public static SpanAttributeIndexComparer Ordinal { get; } = new();

        public bool Equals((string SpanId, string Key, string Value) x, (string SpanId, string Key, string Value) y)
        {
            return string.Equals(x.SpanId, y.SpanId, StringComparison.Ordinal) &&
                   string.Equals(x.Key, y.Key, StringComparison.Ordinal) &&
                   string.Equals(x.Value, y.Value, StringComparison.Ordinal);
        }

        public int GetHashCode((string SpanId, string Key, string Value) obj)
        {
            var hash = new HashCode();
            hash.Add(obj.SpanId, StringComparer.Ordinal);
            hash.Add(obj.Key, StringComparer.Ordinal);
            hash.Add(obj.Value, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }
}
