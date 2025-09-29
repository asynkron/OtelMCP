using Asynkron.OtelReceiver.Data;
using Asynkron.OtelReceiver.Services;
using Asynkron.OtelReceiver.Data.Providers;
using Microsoft.EntityFrameworkCore;

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

builder.Services.AddGrpc();

builder.Services.AddScoped<ModelRepo>();

var app = builder.Build();

app.MapGrpcService<TraceServiceImpl>();
app.MapGrpcService<LogsServiceImpl>();
app.MapGrpcService<MetricsServiceImpl>();

app.MapGet("/", () => "Asynkron Otel Receiver");

using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<OtelReceiverContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await context.Database.MigrateAsync();
}

app.Run();
