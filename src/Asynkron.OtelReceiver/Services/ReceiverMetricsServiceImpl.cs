using System.Threading.Tasks;
using Asynkron.OtelReceiver.Monitoring;
using Asynkron.OtelReceiver.Monitoring.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Asynkron.OtelReceiver.Services;

/// <summary>
/// Streams live receiver metrics over gRPC so external clients (such as the CLI host) can visualise the data.
/// </summary>
public class ReceiverMetricsServiceImpl(IReceiverMetricsCollector metrics) : ReceiverMetricsService.ReceiverMetricsServiceBase
{
    public override async Task SubscribeMetrics(
        Empty request,
        IServerStreamWriter<ReceiverMetricsUpdate> responseStream,
        ServerCallContext context)
    {
        await foreach (var snapshot in metrics.WatchAsync(context.CancellationToken))
        {
            var update = new ReceiverMetricsUpdate
            {
                SpansReceived = snapshot.SpansReceived,
                SpansStored = snapshot.SpansStored,
                LogsReceived = snapshot.LogsReceived,
                LogsStored = snapshot.LogsStored,
                MetricsReceived = snapshot.MetricsReceived,
                MetricsStored = snapshot.MetricsStored
            };

            await responseStream.WriteAsync(update).ConfigureAwait(false);
        }
    }
}
