using Asynkron.OtelReceiver;

// The application can either run the OTLP receiver server or attach to an existing instance
// and print its metrics. We use a simple flag so the tool remains easy to run.
if (args.Any(a => string.Equals(a, "--metrics-client", StringComparison.OrdinalIgnoreCase)))
{
    var filteredArgs = args.Where(a => !string.Equals(a, "--metrics-client", StringComparison.OrdinalIgnoreCase)).ToArray();
    await ReceiverMetricsConsole.RunAsync(filteredArgs);
}
else
{
    await ReceiverServerHost.RunAsync(args);
}
