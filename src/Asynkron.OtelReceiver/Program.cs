using Asynkron.OtelReceiver;
using Asynkron.OtelReceiver.Data;
using Asynkron.OtelReceiver.Data.Providers;
using Asynkron.OtelReceiver.Monitoring;
using Asynkron.OtelReceiver.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// The application can either run the OTLP receiver ASP.NET Core host or attach to an existing
// instance and print its metrics. We use a simple flag so the tool remains easy to run.
if (args.Any(a => string.Equals(a, "--metrics-client", StringComparison.OrdinalIgnoreCase)))
{
    var filteredArgs = args.Where(a => !string.Equals(a, "--metrics-client", StringComparison.OrdinalIgnoreCase)).ToArray();
    await ReceiverMetricsConsole.RunAsync(filteredArgs);
    return;
}

var (applicationArgs, bindingAddress) = ExtractAddressArguments(args);

var builder = WebApplication.CreateBuilder(applicationArgs);

if (!string.IsNullOrWhiteSpace(bindingAddress))
{
    builder.WebHost.UseUrls(bindingAddress);
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listenOptions =>
        listenOptions.Protocols = HttpProtocols.Http2);
});

var sqliteConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                             ?? "Data Source=otel.db";

builder.Services.AddDbContextFactory<OtelReceiverContext>(options =>
{
    options.UseSqlite(sqliteConnectionString);
});

builder.Services.AddScoped<ISpanBulkInserter, SqliteSpanBulkInserter>();

builder.Services.AddSingleton<IReceiverMetricsCollector, ReceiverMetricsCollector>();
builder.Services.AddGrpc();
builder.Services.AddScoped<ModelRepo>();

var app = builder.Build();

app.MapGrpcService<TraceServiceImpl>();
app.MapGrpcService<LogsServiceImpl>();
app.MapGrpcService<MetricsServiceImpl>();
app.MapGrpcService<ReceiverMetricsServiceImpl>();
app.MapGrpcService<DataServiceImpl>();
app.MapMcpStreamingEndpoint();
app.MapGet("/", () => "Asynkron Otel Receiver");

await using (var scope = app.Services.CreateAsyncScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OtelReceiverContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await context.Database.MigrateAsync();
}

await app.RunAsync();

static (string[] RemainingArgs, string? Address) ExtractAddressArguments(string[] sourceArgs)
{
    var remainingArgs = new List<string>(sourceArgs.Length);
    string? address = null;

    for (var i = 0; i < sourceArgs.Length; i++)
    {
        var argument = sourceArgs[i];

        if (argument.StartsWith("--address=", StringComparison.OrdinalIgnoreCase))
        {
            address = argument[10..];
        }
        else if (string.Equals(argument, "--address", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= sourceArgs.Length)
            {
                throw new ArgumentException("The --address option requires a value.");
            }

            address = sourceArgs[i + 1];
            i++; // Skip the value.
        }
        else
        {
            remainingArgs.Add(argument);
        }
    }

    if (address is not null && string.IsNullOrWhiteSpace(address))
    {
        throw new ArgumentException("The --address option requires a non-empty value.");
    }

    return (remainingArgs.ToArray(), address);
}
