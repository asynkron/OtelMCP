# `opentelemetry/proto/collector` Context

Top-level namespace for OTLP collector service definitions. Subfolders split by signal type:
- [`logs/context.md`](logs/context.md)
- [`metrics/context.md`](metrics/context.md)
- [`trace/context.md`](trace/context.md)

Each contains versioned RPC service definitions used by the receiver to implement OTLP ingestion endpoints.
