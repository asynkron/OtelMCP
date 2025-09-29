using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource.AddService(serviceName: "AspireShop.LoadGenerator", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource(TelemetryWorker.ActivitySourceName)
        .SetSampler(new AlwaysOnSampler())
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(TelemetryWorker.MeterName)
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.ParseStateValues = true;
    logging.AddOtlpExporter();
});

builder.Services.AddHostedService<TelemetryWorker>();

var app = builder.Build();
await app.RunAsync();

internal sealed class TelemetryWorker(ILogger<TelemetryWorker> logger) : BackgroundService
{
    internal const string ActivitySourceName = "AspireShop.LoadGenerator";
    internal const string MeterName = "AspireShop.LoadGenerator";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> PurchaseCounter = Meter.CreateCounter<long>("aspire_shop_purchases");
    private static readonly Histogram<double> CheckoutDuration = Meter.CreateHistogram<double>(
        "aspire_shop_checkout_duration", unit: "s");

    private readonly ILogger<TelemetryWorker> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var random = new Random();

        while (!stoppingToken.IsCancellationRequested)
        {
            var userId = $"user-{random.Next(1000, 9999)}";
            var itemCount = random.Next(1, 6);
            var checkoutDelay = TimeSpan.FromMilliseconds(random.Next(250, 1250));

            using var checkout = ActivitySource.StartActivity("basket.checkout", ActivityKind.Server);
            checkout?.SetTag("app.user.id", userId);
            checkout?.SetTag("app.basket.item_count", itemCount);

            try
            {
                await Task.Delay(checkoutDelay, stoppingToken);

                using (ActivitySource.StartActivity("catalog.lookup", ActivityKind.Client))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(random.Next(100, 400)), stoppingToken);
                }

                using (ActivitySource.StartActivity("basket.persist", ActivityKind.Internal))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(random.Next(50, 150)), stoppingToken);
                }

                PurchaseCounter.Add(itemCount, new KeyValuePair<string, object?>("app.user.id", userId));
                CheckoutDuration.Record(checkoutDelay.TotalSeconds, new KeyValuePair<string, object?>("app.user.id", userId));

                checkout?.SetStatus(ActivityStatusCode.Ok);
                _logger.LogInformation("Simulated checkout for {UserId} containing {ItemCount} items in {Duration} ms.",
                    userId, itemCount, checkoutDelay.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                checkout?.SetStatus(ActivityStatusCode.Unset);
                break;
            }
            catch (Exception ex)
            {
                checkout?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Failed to simulate checkout for {UserId}.", userId);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        ActivitySource.Dispose();
        Meter.Dispose();
    }
}
