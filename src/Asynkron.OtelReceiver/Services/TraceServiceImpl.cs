using System.Threading;
using System.Threading.Channels;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Trace.V1;
using TraceLens.Infra;
using Tracelens.Proto.V1;
using Asynkron.OtelReceiver.Data;
using Asynkron.OtelReceiver.Monitoring;

namespace Asynkron.OtelReceiver.Services;

public class TraceServiceImpl : TraceService.TraceServiceBase
{
    private static bool _running;

    private static readonly Channel<ExportTraceServiceRequest> Channel =
        System.Threading.Channels.Channel.CreateUnbounded<ExportTraceServiceRequest>();

    private static long _count;
    private readonly ModelRepo _repo;
    private readonly IReceiverMetricsCollector _metrics;

    public TraceServiceImpl(ModelRepo repo, IReceiverMetricsCollector metrics)
    {
        _repo = repo;
        _metrics = metrics;

        RunConsumer();
    }

    public override async Task<ExportTraceServiceResponse> Export(ExportTraceServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            var spanCount = CountSpans(request);
            if (spanCount > 0)
            {
                _metrics.RecordSpansReceived(spanCount);
            }
            Interlocked.Increment(ref _count);
            await Channel.Writer.WriteAsync(request);
        }
        catch
        {
            Console.WriteLine("Error in trace endpoint");
        }

        return new ExportTraceServiceResponse();
    }

    private void RunConsumer()
    {
        if (_running) return;
        Console.WriteLine("Starting");

        _running = true;

        _ = Task.Run(async () =>
        {
            while (true)
                try
                {
                    if (_count != 0) Console.WriteLine("Current Span delta: " + _count);

                    var requests = await Channel.Reader.ReadBatchAsync(20);
                    Interlocked.Add(ref _count, -requests.Count);

                    var spans = (
                        from request in requests
                        from resourceSpan in request.ResourceSpans
                        let serviceName = resourceSpan.GetServiceName()
                        from scopeSpan in resourceSpan.ScopeSpans
                        from span in scopeSpan.Spans
                        select new SpanWithService
                        {
                            ServiceName = serviceName,
                            Span = span
                        }
                    ).ToList();

                    foreach (var chunk in spans.Chunk(2000))
                        await _repo.SaveTrace(chunk);
                }
                catch (Exception x)
                {
                    Console.WriteLine("Error in trace endpoint: " + x.Message);
                }
        });
    }

    private static long CountSpans(ExportTraceServiceRequest request)
    {
        long count = 0;
        foreach (var resourceSpan in request.ResourceSpans)
        {
            foreach (var scopeSpan in resourceSpan.ScopeSpans)
            {
                count += scopeSpan.Spans.Count;
            }
        }

        return count;
    }
}
