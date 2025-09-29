# `samples/AspireShop` Context

Vendored copy of the .NET Aspire Shop sample application (upstream commit `f2b267b9` from `dotnet/aspire-samples`).
It contains the distributed application host plus the basket, catalog, frontend, and shared defaults projects used to
produce telemetry during manual receiver testing.

Key subdirectories:

- [`AspireShop.AppHost`](AspireShop.AppHost) – Aspire distributed application configuration that orchestrates the workload. In
  this repository it additionally hosts the `src/Asynkron.OtelReceiver` project and injects OTLP exporter settings so telemetry
  automatically flows into OtelMCP during development. The AppHost also disables process-wide HTTP(S) proxies to keep Aspire's
  DCP sidecars from being intercepted in locked-down environments (otherwise the sandbox's mandatory proxy causes startup
  failures even in `OtelMcp:SimpleMode`).
- [`AspireShop.BasketService`](AspireShop.BasketService) – gRPC basket microservice and Redis interactions.
- [`AspireShop.CatalogService`](AspireShop.CatalogService) – HTTP API that surfaces catalog data from PostgreSQL.
- [`AspireShop.CatalogDbManager`](AspireShop.CatalogDbManager) – provisioning utility for the catalog database.
- [`AspireShop.Frontend`](AspireShop.Frontend) – Blazor WASM front-end that drives traffic into the backend services.
- [`AspireShop.ServiceDefaults`](AspireShop.ServiceDefaults) – shared service wiring (logging, OpenTelemetry defaults, etc.).
- [`AspireShop.CatalogDb`](AspireShop.CatalogDb) – schema and data snapshots used by the catalog components.
- [`AspireShop.LoadGenerator`](AspireShop.LoadGenerator) – simplified worker that produces synthetic traces, metrics, and logs for
  environments where the containerised dependencies cannot be launched.

Set the `OtelMcp:SimpleMode` configuration value (environment variable `OtelMcp__SimpleMode=true`) to run only the OtelMCP
collector alongside the load generator. This bypasses the Postgres and Redis containers so telemetry can still be exercised in
restricted environments such as CI or the hosted evaluation sandbox.

The upstream repository ships additional documentation in [`README.md`](README.md) and reference imagery in [`images`](images/).
Keep this copy aligned with upstream licensing when updating the sample.
