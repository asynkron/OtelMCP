# `Monitoring` Context

Telemetry ingestion metrics are collected and broadcast from this folder.

- [`ReceiverMetricsCollector.cs`](ReceiverMetricsCollector.cs) – exposes the `IReceiverMetricsCollector` interface, `ReceiverMetricsSnapshot` record, and a default implementation backed by `System.Diagnostics.Metrics`. It maintains aggregate counters for received/stored spans, logs, and metrics, and supports async streaming to gRPC clients via per-subscriber channels.

Consumers:
- gRPC services use `Record*` methods after persisting payloads (see [`../Services/context.md`](../Services/context.md)).
- [`ReceiverMetricsConsole`](../ReceiverMetricsConsole.cs) subscribes to `WatchAsync` and renders updates in the terminal.
