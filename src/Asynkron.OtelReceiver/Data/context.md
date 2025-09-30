# `Data` Context

The data layer converts OTLP payloads into persisted records and exposes read/write utilities.

## Core files
- [`EfModel.cs`](EfModel.cs) – defines `OtelReceiverContext` and EF Core entity types (`SpanEntity`, `LogEntity`, `SpanAttributeValueEntity`, `MetricEntity`, metadata tables, etc.). Indices mirror common query patterns (trace/span IDs, service names).
- [`ModelRepo.cs`](ModelRepo.cs) – orchestrates persistence of spans, logs, metrics, snapshots, and metadata. It batches OTLP messages, uses provider-specific span inserters, records receiver metrics, and exposes snapshot/metadata APIs consumed by gRPC services.
- [`PrometheusModel.cs`](PrometheusModel.cs) & [`PrometheusRepo.cs`](PrometheusRepo.cs) – currently commented scaffolding for querying Prometheus alongside TraceLens state.

## Providers
- [`Providers/context.md`](Providers/context.md) documents database-specific strategies for inserting spans.

### Usage notes
- `ModelRepo.SaveTrace` writes spans via `ISpanBulkInserter`, updates attribute/name lookup tables with conflict-tolerant raw SQL, persists the normalised `SpanAttributeValues` rows alongside span records, and records ingestion counters.
- `ModelRepo.SearchTraces` now annotates matching attribute clauses (span id, key, value) and can optionally hydrate the original OTLP span protos when callers set `include_span_protos` on the request. Span-attribute predicates are pushed to SQL using the normalised table before trace hydration so limit handling no longer drops matches, and evaluator fallbacks read from the same projection when the denormalised `AttributeMap` is empty.
- `SaveLogs` and `SaveMetrics` transform OTLP structures into relational rows while formatting log bodies and populating attribute indexes for downstream search.
- `LogEntity` normalises both resource and record attributes into the `LogAttributes` table so `ModelRepo.SearchTraces` can push log-body and log-attribute predicates to SQL instead of filtering hydrated entities in memory.
- Snapshot and metadata endpoints support TraceLens visualisation features (see [`../TraceLens/context.md`](../TraceLens/context.md)).
- `ModelRepo.SearchTraces` accepts the TraceLens filter expression tree (see `tracelens.proto`) and translates span-level attribute/service predicates into SQL so composite AND/OR searches are evaluated in the database before results are hydrated. Error and duration leaf filters are recognised as well; duration bounds are pushed down to SQL while error checks run alongside the existing in-memory evaluation.
- Read-focused helpers in `ModelRepo` use `AsNoTracking()` so metadata/service-map lookups and search projections avoid unnecessary EF Core change tracking overhead.

When changing entity shape or persistence semantics, update this context alongside the relevant migration summary in [`../Migrations/context.md`](../Migrations/context.md).
