# `Services` Context

This directory contains gRPC service implementations wired into the ASP.NET Core host.

- [`TraceServiceImpl.cs`](TraceServiceImpl.cs) – handles OTLP trace exports. Requests are queued on a channel, counted for metrics, and consumed by a background task that batches spans (`ReadBatchAsync`) before handing them to `ModelRepo.SaveTrace` for persistence.
- [`LogsServiceImpl.cs`](LogsServiceImpl.cs) – mirrors the trace workflow for log records, chunking payloads and invoking `ModelRepo.SaveLogs` while updating metrics counters.
- [`MetricsServiceImpl.cs`](MetricsServiceImpl.cs) – processes OTLP metrics synchronously: combines resource/scope attributes, normalises timestamps per metric type, persists via `ModelRepo.SaveMetrics`, and records ingestion totals.
- [`ReceiverMetricsServiceImpl.cs`](ReceiverMetricsServiceImpl.cs) – exposes a server-streaming endpoint backed by `IReceiverMetricsCollector.WatchAsync`, allowing external tooling (e.g., `ReceiverMetricsConsole`) to observe live counts.

Supporting infrastructure:
- Channel batching is implemented in [`../TraceLens/Infra/ChannelExtensions.cs`](../TraceLens/Infra/ChannelExtensions.cs).
- Persisted entities and repositories live under [`../Data/context.md`](../Data/context.md).

If you adjust concurrency, batching thresholds, or add new OTLP services, update this summary and cross-reference any new dependencies.
