# Wiring the .NET Aspire Shop sample into the Asynkron OTLP Receiver

This guide documents how to run the [.NET Aspire Shop sample](https://github.com/dotnet/aspire-samples/tree/main/samples/AspireShop) against the receiver in this repository so you can ingest realistic, multi-service telemetry while exercising the MCP API. The complete sample lives in [`samples/AspireShop`](../samples/AspireShop) so you can launch it without cloning an extra repository.

## Why the Aspire Shop sample?

The sample bootstraps a small retail scenario with four collaborating services defined in the `AppHost.cs` distributed application model:

- `AspireShop.Frontend` – Blazor UI that drives traffic into the backend.
- `AspireShop.CatalogService` – ASP.NET Core API backed by PostgreSQL for catalog data.
- `AspireShop.CatalogDbManager` – provisioning endpoint that initializes and migrates the catalog database.
- `AspireShop.BasketService` – gRPC basket service backed by Redis.

Every project references `AspireShop.ServiceDefaults`, which wires OpenTelemetry logging, metrics, and tracing exporters. Crucially, the exporter becomes active when the `OTEL_EXPORTER_OTLP_ENDPOINT` setting is present:

```csharp
private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
{
    var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    if (useOtlpExporter)
    {
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    return builder;
}
```

Because the exporters honor the standard OTLP environment variables, the sample can point directly at our receiver without code changes. The vendored `AspireShop.AppHost` now references the `src/Asynkron.OtelReceiver` project directly. When you launch the AppHost, it boots the OtelMCP collector with an embedded SQLite database and injects the `OTEL_EXPORTER_OTLP_*` variables into every Aspire service so telemetry automatically flows into the receiver.

## Prerequisites

- .NET 8 SDK (already required for this repository).
- Docker Desktop or a local Docker engine for the sample's Postgres and Redis containers.
- Optional: `docker-compose` (ships with most Docker installs) if you plan to run the receiver container separately.

## Step 1 – Launch the Aspire Shop sample with OtelMCP embedded

1. From the repository root, change into the vendored sample's application host:

   ```bash
   cd samples/AspireShop/AspireShop.AppHost
   ```

   The `samples` directory mirrors the upstream repository structure so you can pick other Aspire scenarios later if needed.

2. Start the distributed application:

   ```bash
   dotnet run
   ```

   The AppHost builds all projects, launches Postgres and Redis containers, boots the OtelMCP receiver with SQLite storage, and wires each service's OTLP exporter to the in-process collector. No manual environment variables are required unless you want to override the defaults.

## Step 2 – Generate traffic and inspect the data

- Browse to the URLs printed by the AppHost (typically `https://localhost:7091` for the frontend) to generate load. Shopping cart interactions exercise both HTTP and gRPC backends, producing diverse telemetry for the receiver.
- To monitor ingestion throughput from another terminal, run:

  ```bash
  dotnet run --project src/Asynkron.OtelReceiver -- --metrics-client --address http://localhost:4317
  ```

  The console streams `ReceiverMetrics` updates from the instance that the AppHost launched.
- Use the MCP streaming endpoint (`POST /mcp/stream`) with commands such as `searchTraces` or `getMetricNames` to explore the ingested data once traffic is flowing.
- If you need to replay the demo, stop the AppHost with `Ctrl+C` and run it again; its initialization routines reset the Postgres catalog through the `/reset-db` command exposed in `AppHost.cs`.

## Running the collector manually

Prefer to operate the collector separately? Launch it with your desired settings and set `OTEL_EXPORTER_OTLP_ENDPOINT`/`OTEL_EXPORTER_OTLP_PROTOCOL` before running the AppHost. The environment hook only applies when those variables are unset, so explicit values will continue to point at your custom endpoint.

## Troubleshooting tips

- **Connection refused** from the Aspire services usually means the embedded receiver failed to start. Check the AppHost logs for OtelMCP startup errors (for example, database file permissions).
- To forward telemetry from containerized workloads, replace `localhost` in `OTEL_EXPORTER_OTLP_ENDPOINT` with the host's IP or use Docker networking aliases. You can set these environment variables before running the AppHost to bypass the embedded collector wiring.

With this setup you gain a realistic telemetry stream—driven by a fully instrumented Aspire application—feeding directly into the MCP-enabled OTLP receiver for end-to-end testing.
