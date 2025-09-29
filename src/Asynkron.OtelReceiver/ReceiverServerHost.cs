using Asynkron.OtelReceiver.Data;
using Asynkron.OtelReceiver.Data.Providers;
using Asynkron.OtelReceiver.Monitoring;
using Asynkron.OtelReceiver.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Asynkron.OtelReceiver;

internal static class ReceiverServerHost
{
    /// <summary>
    /// Bootstraps the OTLP receiver ASP.NET Core host.
    /// </summary>
    public static async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder(args);

        const string defaultProvider = "Postgres";
        var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? defaultProvider;

        if (string.Equals(databaseProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddDbContextFactory<OtelReceiverContext>(options =>
            {
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
            });

            builder.Services.AddScoped<ISpanBulkInserter, SqliteSpanBulkInserter>();
        }
        else
        {
            builder.Services.AddDbContextFactory<OtelReceiverContext>(options =>
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
            });

            builder.Services.AddScoped<ISpanBulkInserter, PostgresSpanBulkInserter>();
        }

        builder.Services.AddSingleton<IReceiverMetricsCollector, ReceiverMetricsCollector>();
        builder.Services.AddGrpc();
        builder.Services.AddScoped<ModelRepo>();

        var app = builder.Build();

        app.MapGrpcService<TraceServiceImpl>();
        app.MapGrpcService<LogsServiceImpl>();
        app.MapGrpcService<MetricsServiceImpl>();
        app.MapGrpcService<ReceiverMetricsServiceImpl>();
        app.MapGet("/", () => "Asynkron Otel Receiver");

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OtelReceiverContext>>();
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
        }

        await app.RunAsync(cancellationToken);
    }
}
