# `Services` Context

This directory contains gRPC service implementations wired into the ASP.NET Core host.

- [`TraceServiceImpl.cs`](TraceServiceImpl.cs) – handles OTLP trace exports. Requests are queued on a channel, counted for metrics, and consumed by a background task that batches spans (`ReadBatchAsync`) before handing them to `ModelRepo.SaveTrace` for persistence.
- [`LogsServiceImpl.cs`](LogsServiceImpl.cs) – mirrors the trace workflow for log records, chunking payloads and invoking `ModelRepo.SaveLogs` while updating metrics counters.
- [`MetricsServiceImpl.cs`](MetricsServiceImpl.cs) – processes OTLP metrics synchronously: combines resource/scope attributes, normalises timestamps per metric type, persists via `ModelRepo.SaveMetrics` once per request to avoid duplicating rows, and records ingestion totals.
- [`ReceiverMetricsServiceImpl.cs`](ReceiverMetricsServiceImpl.cs) – exposes a server-streaming endpoint backed by `IReceiverMetricsCollector.WatchAsync`, allowing external tooling (e.g., `ReceiverMetricsConsole`) to observe live counts.
- [`DataServiceImpl.cs`](DataServiceImpl.cs) – surfaces TraceLens search, metadata, and metrics queries so clients can explore persisted telemetry, including the enriched search responses (attribute clause matches and optional span protos).
- [`McpStreamingEndpoint.cs`](McpStreamingEndpoint.cs) – provides a newline-delimited JSON streaming HTTP endpoint that mirrors the TraceLens DataService gRPC commands for Model Context Protocol clients and now logs handshake, request, and response payloads at information level for easier diagnostics.

Supporting infrastructure:
- Channel batching is implemented in [`../TraceLens/Infra/ChannelExtensions.cs`](../TraceLens/Infra/ChannelExtensions.cs).
- Persisted entities and repositories live under [`../Data/context.md`](../Data/context.md).

If you adjust concurrency, batching thresholds, or add new OTLP services, update this summary and cross-reference any new dependencies.
