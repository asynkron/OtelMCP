# `samples/AspireShop` Context

Vendored copy of the .NET Aspire Shop sample application (upstream commit `f2b267b9` from `dotnet/aspire-samples`).
It contains the distributed application host plus the basket, catalog, frontend, and shared defaults projects used to
produce telemetry during manual receiver testing.

Key subdirectories:

- [`AspireShop.AppHost`](AspireShop.AppHost) – Aspire distributed application configuration that orchestrates the workload. In
  this repository it additionally hosts the `src/Asynkron.OtelReceiver` project and injects OTLP exporter settings so telemetry
  automatically flows into OtelMCP during development.
- [`AspireShop.BasketService`](AspireShop.BasketService) – gRPC basket microservice and Redis interactions.
- [`AspireShop.CatalogService`](AspireShop.CatalogService) – HTTP API that surfaces catalog data from PostgreSQL.
- [`AspireShop.CatalogDbManager`](AspireShop.CatalogDbManager) – provisioning utility for the catalog database.
- [`AspireShop.Frontend`](AspireShop.Frontend) – Blazor WASM front-end that drives traffic into the backend services.
- [`AspireShop.ServiceDefaults`](AspireShop.ServiceDefaults) – shared service wiring (logging, OpenTelemetry defaults, etc.).
- [`AspireShop.CatalogDb`](AspireShop.CatalogDb) – schema and data snapshots used by the catalog components.

The upstream repository ships additional documentation in [`README.md`](README.md) and reference imagery in [`images`](images/).
Keep this copy aligned with upstream licensing when updating the sample.
