using Microsoft.EntityFrameworkCore;
using Asynkron.OtelReceiver.Data;
using Asynkron.OtelReceiver.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextFactory<OtelReceiverContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

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
