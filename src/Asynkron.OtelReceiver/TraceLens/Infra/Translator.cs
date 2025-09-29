using System.Diagnostics;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using TraceLens.Model;
using Tracelens.Proto.V1;
using TraceLensModelDomain = TraceLens.Model.TraceLensModel;
using Span = OpenTelemetry.Proto.Trace.V1.Span;

namespace TraceLens.Infra;

public static class OtelTranslator
{
    public static IList<LogEntry> GetLogModel(IReadOnlyCollection<LogRecord> logRecords)
    {
        var logs = GetLogs(logRecords);
        return logs;
    }

    public static TraceLensModelDomain GetModel(IReadOnlyCollection<SpanWithService> traces,
        IReadOnlyCollection<LogRecord> logRecords, bool flatten, bool diagnostics=true, bool multiRoot=false)
    {
        var sw = Stopwatch.StartNew();
        var logLookup = (
                from log in logRecords
                select log)
            .ToLookup(l => l.SpanId);

        var spans = (
                from span in traces
                where span.Span != null
                select span)
            .DistinctBy(s => s.Span.SpanId)
            .ToList();

        Console.WriteLine("start translation");
        var entries = (
                from spanX in spans
                let span = spanX.Span
                let tags = GetTags(span)
                let logs = GetLogs(logLookup, span)
                select new Model.Span(span.TraceId.ToHex(), span.SpanId.ToHex(), span.ParentSpanId.ToHex(),
                    spanX.ServiceName,
                    span.Name, span.StartTimeUnixNano, span.EndTimeUnixNano, tags, logs, span.Kind)
            )
            .ToList();
        Console.WriteLine("end translation");

        var model = TraceLensModelDomain.Create(entries, flatten, diagnostics, multiRoot);

        Console.WriteLine("Model translation {0}",sw.Elapsed);
        return model;
    }

    private static List<LogEntry> GetLogs(ILookup<ByteString, LogRecord> logLookup, Span span)
    {
        var l = logLookup[span.SpanId]
            .Select(l => new LogEntry(l.TimeUnixNano, l.SeverityText, l?.Body?.StringValue ?? "null",
                GetAttributes(l!.Attributes)))
            .ToList();

        var l2 = span
            .Events
            .Select(e => new LogEntry(e.TimeUnixNano, "Event", e.Name, GetAttributes(e.Attributes)))
            .ToList();
        
        return l.Concat(l2).OrderBy(l => l.TimeUnixNano).ToList();
    }
    
    private static List<LogEntry> GetLogs(IEnumerable<LogRecord> logs)
    {
        var l = logs
            .Select(l => new LogEntry(l.TimeUnixNano, l.SeverityText, l?.Body?.StringValue ?? "null",
                GetAttributes(l!.Attributes)))
            .ToList();

        
        return l.ToList();
    }

    private static Dictionary<string, object?> GetAttributes(RepeatedField<KeyValue> attributes)
    {
        var res = attributes.DistinctBy(k => k.Key).ToDictionary(x => x.Key, x => x.Value.ToValue());

        ExtractVirtualAttributes(res);

        return res!;
    }

    private static void ExtractVirtualAttributes(Dictionary<string, object> res)
    {
        //extract virtual attributes
        foreach (var (key, value) in res.ToArray())
        {
            if (value is not AttributeString { IsJson: true } a) continue;
            var more = JsonFlattener.Flatten(a.Json!);
            {
                foreach (var m in more)
                {
                    var attrValue = m.Value;
                    if (attrValue is string attrStr) attrValue = new AttributeString(attrStr, true);

                    res.Add(key + "." + m.Path, attrValue!);
                }
            }
        }
    }
    
    private static Dictionary<string, object?> GetTags(Span span)
    {
        var distinct = span.Attributes
            .DistinctBy(k => k.Key);

        var dict = distinct.ToDictionary<KeyValue, string, object?>(x => x.Key, x => x.Value.StringValue);

        return dict;
    }
}