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
            var attributes = new List<LogAttributeEntity>();

            foreach (var attribute in l.Attributes)
            {
                attributes.Add(new LogAttributeEntity
                {
                    Key = attribute.Key,
                    Value = attribute.Value.ToStringValue(),
                    Source = LogAttributeSource.Record
                });
            }

            foreach (var attribute in r.Resource.Attributes)
            {
                attributes.Add(new LogAttributeEntity
                {
                    Key = attribute.Key,
                    Value = attribute.Value.ToStringValue(),
                    Source = LogAttributeSource.Resource
                });
            }

            var log = new LogEntity
            {
                TraceId = l.TraceId.ToHex(),
                SpanId = l.SpanId.ToHex(),
                Timestamp = (long)l.TimeUnixNano,
                ObservedTimestamp = (long)l.ObservedTimeUnixNano,
                SeverityText = l.SeverityText,
                SeverityNumber = (byte)l.SeverityNumber,
                Body = FormatLog(l),
                RawBody = l.Body.StringValue,
                Proto = l.ToByteArray(),
                Attributes = attributes
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
            .AsNoTracking()
            .Select(span => span.ServiceName)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var spanNames = await context.SpanNames
            .AsNoTracking()
            .Select(span => span.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToListAsync();

        var tagNames = await context.SpanAttributes
            .AsNoTracking()
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
            .AsNoTracking()
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

        var spansQuery = context.Spans
            .AsNoTracking()
            .AsQueryable();

        var serviceNames = new HashSet<string>(StringComparer.Ordinal);
        var spanNames = new HashSet<string>(StringComparer.Ordinal);
        CollectFilterHints(request.Filter, serviceNames, spanNames);

        if (serviceNames.Count > 0 && spanNames.Count == 0)
        {
            spansQuery = spansQuery.Where(span => serviceNames.Contains(span.ServiceName));
        }
        else if (spanNames.Count > 0 && serviceNames.Count == 0)
        {
            spansQuery = spansQuery.Where(span => spanNames.Contains(span.OperationName));
        }
        else if (serviceNames.Count > 0 && spanNames.Count > 0)
        {
            spansQuery = spansQuery.Where(span =>
                serviceNames.Contains(span.ServiceName) ||
                spanNames.Contains(span.OperationName));
        }

        if (request.StartTime != 0)
        {
            spansQuery = spansQuery.Where(span => span.StartTimestamp >= (long)request.StartTime);
        }

        if (request.EndTime != 0)
        {
            spansQuery = spansQuery.Where(span => span.EndTimestamp <= (long)request.EndTime);
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
            .AsNoTracking()
            .Where(span => candidateIds.Contains(span.TraceId))
            .ToListAsync();

        var logSearch = request.LogFilter?.BodyContains;
        var normalizedLogSearch = string.IsNullOrWhiteSpace(logSearch)
            ? null
            : logSearch.ToLowerInvariant();

        var requiredLogFilters = new List<AttributeFilter>();
        CollectRequiredLogAttributeFilters(request.Filter, requiredLogFilters, true);

        IQueryable<LogEntity> logsQuery = context.Logs
            .AsNoTracking()
            .Where(log => candidateIds.Contains(log.TraceId));

        if (normalizedLogSearch is not null)
        {
            logsQuery = logsQuery.Where(log =>
                log.RawBody != null &&
                EF.Functions.Like(log.RawBody.ToLower(), $"%{normalizedLogSearch}%"));
        }

        foreach (var filter in requiredLogFilters)
        {
            logsQuery = ApplyLogAttributeFilter(logsQuery, filter);
        }

        var logs = await logsQuery
            .Include(log => log.Attributes)
            .ToListAsync();

        var logsByTrace = logs
            .GroupBy(log => log.TraceId)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

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

            var traceLogs = logsByTrace.TryGetValue(traceId, out var groupedLogs)
                ? groupedLogs
                : new List<LogEntity>();

            var clauseMap = new Dictionary<string, AttributeClauseMatch>(StringComparer.Ordinal);
            var traceContext = new TraceContext(traceSpans, traceLogs);
            if (!EvaluateTraceFilter(request.Filter, traceContext, clauseMap))
            {
                continue;
            }

            selectedTraceIds.Add(traceId);

            if (normalizedLogSearch is not null)
            {
                traceLogs = traceLogs
                    .Where(log => log.RawBody?.Contains(logSearch!, StringComparison.OrdinalIgnoreCase) ?? false)
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

            foreach (var clause in clauseMap.Values.OrderBy(match => match.Clause, StringComparer.Ordinal))
            {
                traceResult.AttributeClauses.Add(clause);
            }

            foreach (var span in traceSpans)
            {
                if (span.Proto is null || span.Proto.Length == 0)
                {
                    continue;
                }

                var stored = SpanWithService.Parser.ParseFrom(span.Proto);
                if (stored.Span is not null)
                {
                    traceResult.Spans.Add(stored.Span);
                }
            }

            response.Results.Add(traceResult);

            if (response.Results.Count == limit)
            {
                break;
            }
        }

        var logCounts = logs
            .Where(log => selectedTraceIds.Contains(log.TraceId))
            .Where(log => normalizedLogSearch is null ||
                          (log.RawBody?.Contains(logSearch!, StringComparison.OrdinalIgnoreCase) ?? false))
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

        var spansQuery = context.Spans
            .AsNoTracking()
            .AsQueryable();

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
            .AsNoTracking()
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
                .AsNoTracking()
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
        var snapshot = await context.Snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id);
        return new GetSnapshotResponse
        {
            Model = snapshot is null ? null : TraceLensModel.Parser.ParseFrom(snapshot.Proto)
        };
    }

    public async Task<ListSnapshotsResponse> ListSnapshots()
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var snapshots = await context.Snapshots
            .AsNoTracking()
            .ToListAsync();

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
        var names = await context.Metrics
            .AsNoTracking()
            .Select(m => m.Name)
            .Distinct()
            .ToListAsync();

        return new GetMetricNamesResponse()
        {
            Name = { names }
        };
    }

    public async Task<GetMetricResponse> GetMetric(GetMetricRequest request)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var data = await context.Metrics
            .AsNoTracking()
            .Where(m => m.Name == request.Name)
            .ToListAsync();

        var protos = data.Select(m => Metric.Parser.ParseFrom(m.Proto)).ToList();

        return new GetMetricResponse()
        {
            Metrics = { protos }
        };
    }

    private static void CollectFilterHints(
        TraceFilterExpression? expression,
        ISet<string> serviceNames,
        ISet<string> spanNames)
    {
        if (expression is null)
        {
            return;
        }

        switch (expression.ExpressionCase)
        {
            case TraceFilterExpression.ExpressionOneofCase.Service when
                !string.IsNullOrWhiteSpace(expression.Service.Name):
                serviceNames.Add(expression.Service.Name);
                break;
            case TraceFilterExpression.ExpressionOneofCase.SpanName when
                !string.IsNullOrWhiteSpace(expression.SpanName.Name):
                spanNames.Add(expression.SpanName.Name);
                break;
            case TraceFilterExpression.ExpressionOneofCase.Composite:
                foreach (var child in expression.Composite.Expressions)
                {
                    CollectFilterHints(child, serviceNames, spanNames);
                }

                break;
        }
    }

    private static void CollectRequiredLogAttributeFilters(
        TraceFilterExpression? expression,
        ICollection<AttributeFilter> filters,
        bool isRequired)
    {
        if (expression is null || !isRequired && expression.ExpressionCase != TraceFilterExpression.ExpressionOneofCase.Composite)
        {
            return;
        }

        switch (expression.ExpressionCase)
        {
            case TraceFilterExpression.ExpressionOneofCase.Attribute when
                isRequired &&
                expression.Attribute is { Target: AttributeFilterTarget.Log } attribute &&
                !string.IsNullOrWhiteSpace(attribute.Key):
                filters.Add(attribute);
                break;
            case TraceFilterExpression.ExpressionOneofCase.Composite:
                var composite = expression.Composite;
                if (composite is null || composite.Expressions.Count == 0)
                {
                    return;
                }

                var isOr = composite.Operator == TraceFilterComposite.Types.Operator.Or;
                foreach (var child in composite.Expressions)
                {
                    CollectRequiredLogAttributeFilters(child, filters, isRequired && !isOr);
                }

                break;
        }
    }

    private static IQueryable<LogEntity> ApplyLogAttributeFilter(
        IQueryable<LogEntity> query,
        AttributeFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.Key))
        {
            return query;
        }

        var operation = filter.Operator switch
        {
            AttributeFilterOperator.Equals => AttributeFilterOperator.Equals,
            AttributeFilterOperator.Exists => AttributeFilterOperator.Exists,
            _ => string.IsNullOrEmpty(filter.Value)
                ? AttributeFilterOperator.Exists
                : AttributeFilterOperator.Equals
        };

        return operation == AttributeFilterOperator.Equals
            ? query.Where(log => log.Attributes.Any(attribute =>
                attribute.Key == filter.Key && attribute.Value == filter.Value))
            : query.Where(log => log.Attributes.Any(attribute => attribute.Key == filter.Key));
    }

    private static bool EvaluateTraceFilter(
        TraceFilterExpression? expression,
        TraceContext traceContext,
        IDictionary<string, AttributeClauseMatch> clauseMap)
    {
        if (expression is null)
        {
            return true;
        }

        return expression.ExpressionCase switch
        {
            TraceFilterExpression.ExpressionOneofCase.Attribute =>
                EvaluateAttributeFilter(expression.Attribute, traceContext, clauseMap),
            TraceFilterExpression.ExpressionOneofCase.Service =>
                EvaluateServiceFilter(expression.Service, traceContext),
            TraceFilterExpression.ExpressionOneofCase.SpanName =>
                EvaluateSpanNameFilter(expression.SpanName, traceContext),
            TraceFilterExpression.ExpressionOneofCase.Composite =>
                EvaluateCompositeFilter(expression.Composite, traceContext, clauseMap),
            _ => true
        };
    }

    private static bool EvaluateCompositeFilter(
        TraceFilterComposite composite,
        TraceContext traceContext,
        IDictionary<string, AttributeClauseMatch> clauseMap)
    {
        if (composite is null || composite.Expressions.Count == 0)
        {
            return true;
        }

        var useOr = composite.Operator == TraceFilterComposite.Types.Operator.Or;

        var result = useOr ? false : true;

        foreach (var expression in composite.Expressions)
        {
            var childResult = EvaluateTraceFilter(expression, traceContext, clauseMap);
            if (useOr)
            {
                result |= childResult;
            }
            else
            {
                result &= childResult;
            }
        }

        return result;
    }

    private static bool EvaluateServiceFilter(ServiceFilter filter, TraceContext traceContext)
    {
        if (filter is null || string.IsNullOrWhiteSpace(filter.Name))
        {
            return false;
        }

        return traceContext.Spans.Any(span =>
            string.Equals(span.ServiceName, filter.Name, StringComparison.Ordinal));
    }

    private static bool EvaluateSpanNameFilter(SpanNameFilter filter, TraceContext traceContext)
    {
        if (filter is null || string.IsNullOrWhiteSpace(filter.Name))
        {
            return false;
        }

        return traceContext.Spans.Any(span =>
            string.Equals(span.OperationName, filter.Name, StringComparison.Ordinal));
    }

    private static bool EvaluateAttributeFilter(
        AttributeFilter filter,
        TraceContext traceContext,
        IDictionary<string, AttributeClauseMatch> clauseMap)
    {
        if (filter is null || string.IsNullOrWhiteSpace(filter.Key))
        {
            return false;
        }

        var target = filter.Target == AttributeFilterTarget.Log
            ? AttributeFilterTarget.Log
            : AttributeFilterTarget.Span;

        var operation = filter.Operator switch
        {
            AttributeFilterOperator.Equals => AttributeFilterOperator.Equals,
            AttributeFilterOperator.Exists => AttributeFilterOperator.Exists,
            AttributeFilterOperator.Unspecified => string.IsNullOrEmpty(filter.Value)
                ? AttributeFilterOperator.Exists
                : AttributeFilterOperator.Equals,
            _ => AttributeFilterOperator.Equals
        };

        if (operation == AttributeFilterOperator.Equals && string.IsNullOrEmpty(filter.Value))
        {
            return false;
        }

        var clauseKey = BuildClauseKey(filter.Key!, filter.Value, target, operation);

        if (!clauseMap.TryGetValue(clauseKey, out var clause))
        {
            clause = new AttributeClauseMatch
            {
                Clause = clauseKey
            };
            clauseMap[clauseKey] = clause;
        }

        var matches = target == AttributeFilterTarget.Log
            ? EvaluateLogAttributeMatches(traceContext.Logs, filter.Key!, filter.Value, operation)
            : EvaluateSpanAttributeMatches(traceContext.Spans, filter.Key!, filter.Value, operation);

        if (matches.Count > 0)
        {
            clause.Satisfied = true;
            clause.Matches.AddRange(matches);
            return true;
        }

        return false;
    }

    private static List<AttributeMatch> EvaluateSpanAttributeMatches(
        IReadOnlyList<SpanEntity> spans,
        string key,
        string? value,
        AttributeFilterOperator operation)
    {
        var matches = new List<AttributeMatch>();

        foreach (var span in spans)
        {
            if (span.AttributeMap is not { Length: > 0 })
            {
                continue;
            }

            if (operation == AttributeFilterOperator.Equals)
            {
                var target = $"{key}:{value}";
                if (span.AttributeMap.Contains(target))
                {
                    matches.Add(new AttributeMatch
                    {
                        SpanId = span.SpanId,
                        Key = key,
                        Value = value ?? string.Empty
                    });
                }
            }
            else
            {
                foreach (var attribute in span.AttributeMap)
                {
                    if (!attribute.StartsWith($"{key}:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var matchValue = attribute.Length > key.Length + 1
                        ? attribute[(key.Length + 1)..]
                        : string.Empty;

                    matches.Add(new AttributeMatch
                    {
                        SpanId = span.SpanId,
                        Key = key,
                        Value = matchValue
                    });
                }
            }
        }

        return matches;
    }

    private static List<AttributeMatch> EvaluateLogAttributeMatches(
        IReadOnlyList<LogEntity> logs,
        string key,
        string? value,
        AttributeFilterOperator operation)
    {
        var matches = new List<AttributeMatch>();

        foreach (var log in logs)
        {
            if (log.Attributes is not { Count: > 0 })
            {
                continue;
            }

            if (operation == AttributeFilterOperator.Equals)
            {
                foreach (var attribute in log.Attributes)
                {
                    if (!string.Equals(attribute.Key, key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!string.Equals(attribute.Value, value, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matches.Add(new AttributeMatch
                    {
                        SpanId = log.SpanId,
                        Key = key,
                        Value = value ?? string.Empty
                    });
                }
            }
            else
            {
                foreach (var attribute in log.Attributes)
                {
                    if (!string.Equals(attribute.Key, key, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    matches.Add(new AttributeMatch
                    {
                        SpanId = log.SpanId,
                        Key = key,
                        Value = attribute.Value ?? string.Empty
                    });
                }
            }
        }

        return matches;
    }

    private static string BuildClauseKey(
        string key,
        string? value,
        AttributeFilterTarget target,
        AttributeFilterOperator operation)
    {
        var prefix = target == AttributeFilterTarget.Log ? "log" : "tag";

        if (operation == AttributeFilterOperator.Equals && !string.IsNullOrEmpty(value))
        {
            return $"{prefix}:{key}={value}";
        }

        return $"{prefix}:{key}";
    }

    private readonly record struct TraceContext(
        IReadOnlyList<SpanEntity> Spans,
        IReadOnlyList<LogEntity> Logs);

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
}
