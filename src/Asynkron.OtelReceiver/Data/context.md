# `Data` Context

The data layer converts OTLP payloads into persisted records and exposes read/write utilities.

## Core files
- [`EfModel.cs`](EfModel.cs) – defines `OtelReceiverContext` and EF Core entity types (`SpanEntity`, `LogEntity`, `LogAttributeEntity`, `MetricEntity`, metadata tables, etc.). Indices mirror common query patterns (trace/span IDs, service names, log attribute lookups).
- [`ModelRepo.cs`](ModelRepo.cs) – orchestrates persistence of spans, logs, metrics, snapshots, and metadata. It batches OTLP messages, uses provider-specific span inserters, records receiver metrics, and exposes snapshot/metadata APIs consumed by gRPC services. Log persistence now emits both record and resource attributes into `LogAttributeEntity` rows so database queries can filter by structured log metadata.
- [`PrometheusModel.cs`](PrometheusModel.cs) & [`PrometheusRepo.cs`](PrometheusRepo.cs) – currently commented scaffolding for querying Prometheus alongside TraceLens state.

## Providers
- [`Providers/context.md`](Providers/context.md) documents database-specific strategies for inserting spans.

### Usage notes
- `ModelRepo.SaveTrace` writes spans via `ISpanBulkInserter`, updates attribute/name lookup tables with conflict-tolerant raw SQL, and records ingestion counters.
- `SaveLogs` and `SaveMetrics` transform OTLP structures into relational rows while computing derived attributes (formatting log bodies, normalising log attributes into `LogAttributeEntity`).
- Snapshot and metadata endpoints support TraceLens visualisation features (see [`../TraceLens/context.md`](../TraceLens/context.md)).

When changing entity shape or persistence semantics, update this context alongside the relevant migration summary in [`../Migrations/context.md`](../Migrations/context.md).
