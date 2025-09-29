using System.Collections.Concurrent;

namespace TraceLens.Model;

public record TraceLensModel
{
    private TraceLensModel(IList<Span> entries, bool flatten, bool diagnostics, bool multiRoot)
    {
        Flatten = flatten;

        entries = entries.OrderBy(x => x.StartTimeUnixNano).ToList();
        var spansById = entries.ToLookup(e => e.SpanId);
        var spansByParentId = entries.ToLookup(e => e.ParentSpanId);
        
        //first pass
        foreach (var e in entries)
        {
            e.Children = spansByParentId[e.SpanId].ToArray();
            e.Parent = spansById[e.ParentSpanId].FirstOrDefault();
            //add the span log entry
        }

        //second pass. calculate depth, add diagnostics
        foreach (var e in entries)
        {
            if (diagnostics)
            {
                e.Logs.Add(new LogEntry(e.StartTimeUnixNano, "tag",
                    e.ServiceName + " " + e.OperationName + " " + e.SpanId,
                    e.Attributes, 0));
            }

            e.Depth = e.Parent?.Depth + 1 ?? 0;

            foreach (var c in e.Children)
            {
                if (diagnostics)
                {
                    e.Logs.Add(new LogEntry(c.StartTimeUnixNano, "Diagnostics", "Child span {SpanId} starts",
                        new Dictionary<string, object?>
                        {
                            { "SpanId", c.SpanId }
                        }, 0));

                    e.Logs.Add(new LogEntry(c.EndTimeUnixNano, "Diagnostics", "Child span {SpanId} ends",
                        new Dictionary<string, object?>
                        {
                            { "SpanId", c.SpanId }
                        }, 4));
                }
            }

            if (diagnostics)
            {
                e.Logs.Add(new LogEntry(e.StartTimeUnixNano, "Diagnostics", "Span {SpanId} starts",
                    new Dictionary<string, object?>
                    {
                        { "SpanId", e.SpanId }
                    }, 1));

                e.Logs.Add(new LogEntry(e.EndTimeUnixNano, "Diagnostics", "Span {SpanId} ends",
                    new Dictionary<string, object?>
                    {
                        { "SpanId", e.SpanId }
                    }, 3));
            }

            e.Logs = e.Logs.OrderBy(l => l.TimeUnixNano).ThenBy(l => l.SortOrder).ToList();

            foreach (var l in e.Logs)
            {
                l.Span = e;
            }
        }


        var allSpanIds = entries.Select(e => e.SpanId).ToHashSet();
        var rootSpanIds = entries.Where(e => e.ParentSpanId == "" || !allSpanIds.Contains(e.ParentSpanId))
            .Select(e => e.SpanId).ToHashSet();

        var rootSpans = entries.Where(e => rootSpanIds.Contains(e.SpanId)).ToArray();

        Root = new Span("root", "root", "root", "Start", "d", 0, 0, new Dictionary<string, object?>(),
            [],OpenTelemetry.Proto.Trace.V1.Span.Types.SpanKind.Internal)
        {
            Children = rootSpans
        };
        
        foreach (var s in rootSpans) s.Parent = Root;

        All = [Root, ..entries];

        //This touches .Description...
        StartSpan = All.Skip(1).MinBy(s => s.StartTimeUnixNano)!;
        EndSpan = All.Skip(1).MaxBy(s => s.EndTimeUnixNano)!;
        WidestChildDuration = GetWidestChildDuration();
    }
    public bool Flatten { get; }
    public Span StartSpan { get; }
    public Span EndSpan { get; }
    public ulong WidestChildDuration { get; }
    
    public Span Root { get; }

    public IReadOnlyList<Span> All { get; }
    public ConcurrentDictionary<string, string>  Keys { get; } = new();

    private ulong GetWidestChildDuration()
    {
        return Root.GetTotalSpanDurationRecursive();
    }
    
    public static TraceLensModel Create(IList<Span> entries, bool flatten, bool diagnostics, bool multiRoot)
    {
        return new TraceLensModel(entries, flatten, diagnostics, multiRoot);
    }

    public double GetStartPercentD(Span span)
    {
        var s = StartSpan.StartTimeUnixNano;
        var e = EndSpan.EndTimeUnixNano;
        var d = (long)e - (long)s;

        var s1 = (long)span.StartTimeUnixNano - (long)s;
        var s2 = s1 / (double)d;

        return s2 * 100;
    }

    public double GetWidthPercentD(Span span)
    {
        var s = StartSpan.StartTimeUnixNano;
        var e = EndSpan.EndTimeUnixNano;
        var d = (long)e - (long)s;

        var s1 = (long)span.StartTimeUnixNano - (long)s;
        var e1 = (long)span.EndTimeUnixNano - (long)s;

        var s2 = s1 / (double)d;
        var e2 = e1 / (double)d;
        var d2 = e2 - s2;

        return d2 * 100;
    }
}