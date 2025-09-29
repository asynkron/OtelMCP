using System.Runtime.CompilerServices;
using Asynkron.OtelReceiver.Monitoring.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Spectre.Console;

namespace Asynkron.OtelReceiver;

internal static class ReceiverMetricsConsole
{
    /// <summary>
    /// Connects to a running receiver and continuously prints metrics to the console.
    /// </summary>
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var address = ResolveAddress(args);

        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            // Allow plaintext HTTP/2 for local development scenarios.
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        }

        using var channel = GrpcChannel.ForAddress(address);
        var client = new ReceiverMetricsService.ReceiverMetricsServiceClient(channel);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var table = BuildMetricsTable(new ReceiverMetricsUpdate());

        await AnsiConsole.Live(table)
            .AutoClear(false)
            .Overflow(VerticalOverflow.Visible)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async ctx =>
            {
                try
                {
                    await foreach (var update in SubscribeAsync(client, cts.Token))
                    {
                        UpdateTable(table, update);
                        ctx.Refresh();
                    }
                }
                catch (RpcException rpc) when (rpc.StatusCode == StatusCode.Cancelled && cts.IsCancellationRequested)
                {
                    // Graceful shutdown triggered by Ctrl+C.
                }
            });
    }

    private static string ResolveAddress(string[] commandLineArgs)
    {
        var address = "http://localhost:5000";

        for (var i = 0; i < commandLineArgs.Length; i++)
        {
            var argument = commandLineArgs[i];
            if (argument.StartsWith("--address="))
            {
                address = argument[10..];
            }
            else if (argument == "--address" && i + 1 < commandLineArgs.Length)
            {
                address = commandLineArgs[i + 1];
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A receiver address must be provided via --address.");
        }

        return address;
    }

    private static Table BuildMetricsTable(ReceiverMetricsUpdate snapshot)
    {
        var table = new Table().Centered();
        table.Title = new TableTitle("[bold yellow]OTLP Receiver Metrics[/]");
        table.AddColumn("Metric");
        table.AddColumn("Received");
        table.AddColumn("Stored");
        table.AddRow("Spans", Format(snapshot.SpansReceived), Format(snapshot.SpansStored));
        table.AddRow("Logs", Format(snapshot.LogsReceived), Format(snapshot.LogsStored));
        table.AddRow("Metrics", Format(snapshot.MetricsReceived), Format(snapshot.MetricsStored));
        return table;
    }

    private static void UpdateTable(Table table, ReceiverMetricsUpdate snapshot)
    {
        table.Rows.Clear();
        table.AddRow("Spans", Format(snapshot.SpansReceived), Format(snapshot.SpansStored));
        table.AddRow("Logs", Format(snapshot.LogsReceived), Format(snapshot.LogsStored));
        table.AddRow("Metrics", Format(snapshot.MetricsReceived), Format(snapshot.MetricsStored));
    }

    private static string Format(long value) => $"[bold cyan]{value:N0}[/]";

    private static async IAsyncEnumerable<ReceiverMetricsUpdate> SubscribeAsync(
        ReceiverMetricsService.ReceiverMetricsServiceClient client,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var call = client.SubscribeMetrics(new Empty(), cancellationToken: cancellationToken);
        await foreach (var update in call.ResponseStream.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }
    }
}
