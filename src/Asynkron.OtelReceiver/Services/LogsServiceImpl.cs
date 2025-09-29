using System.Threading;
using System.Threading.Channels;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Logs.V1;
using TraceLens.Infra;
using Asynkron.OtelReceiver.Data;
using Asynkron.OtelReceiver.Monitoring;

namespace Asynkron.OtelReceiver.Services;

public class LogsServiceImpl : LogsService.LogsServiceBase
{
    private static bool _running;

    private static readonly Channel<ExportLogsServiceRequest> Channel =
        System.Threading.Channels.Channel.CreateUnbounded<ExportLogsServiceRequest>();

    private static long _count;
    private readonly ModelRepo _repo;
    private readonly IReceiverMetricsCollector _metrics;


    public LogsServiceImpl(ModelRepo repo, IReceiverMetricsCollector metrics)
    {
        _repo = repo;
        _metrics = metrics;
        RunConsumer();
    }

    private void RunConsumer()
    {
        if (_running) return;
        _running = true;
        _ = Task.Run(async () =>
        {
            while (true)
                try
                {
                    if (_count != 0) Console.WriteLine("Current Log delta: " + _count);

                    var requests = await Channel.Reader.ReadBatchAsync(20);
                    Interlocked.Add(ref _count, -requests.Count);

                    var payloads =
                        (
                            from request in requests
                            from resourceLog in request.ResourceLogs
                            from scopeLog in resourceLog.ScopeLogs
                            from log in scopeLog.LogRecords
                            select (log, resourceLog)
                        )
                        .ToList();

                    if (payloads.Count > 0)
                    {
                        _metrics.RecordLogsReceived(payloads.Count);
                    }


                    foreach (var chunk in payloads.Chunk(2000)) await _repo.SaveLogs(chunk);
                }
                catch (Exception x)
                {
                    Console.WriteLine("Error in log endpoint: " + x.Message);
                }
        });
    }


    public override async Task<ExportLogsServiceResponse> Export(ExportLogsServiceRequest request,
        ServerCallContext context)
    {
        try
        {
            Interlocked.Increment(ref _count);
            await Channel.Writer.WriteAsync(request);
        }
        catch
        {
            Console.WriteLine("Error in logs endpoint");
        }

        return new ExportLogsServiceResponse();
    }
}
