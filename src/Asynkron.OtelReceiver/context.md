# `Asynkron.OtelReceiver` Project Context

This ASP.NET Core gRPC service ingests OTLP telemetry and persists it using Entity Framework Core. Key entry points:

- [`Program.cs`](Program.cs) bootstraps the web host, selects the configured database provider (PostgreSQL or SQLite), wires up EF Core factories, registers ingestion services, and ensures migrations are applied at startup.
- [`ReceiverMetricsConsole.cs`](ReceiverMetricsConsole.cs) provides a Spectre.Console CLI that connects to the running receiver (via `--metrics-client`) and streams live ingestion counters.
- [`appsettings.json`](appsettings.json) holds default connection strings and provider selection.
- Proto definitions (`tracelens.proto`, `receiver_metrics.proto`) generate gRPC contracts for TraceLens models and receiver metrics streaming. `tracelens.proto` now exposes a composable TraceLens search filter tree so clients can combine service, span, attribute, error, and duration predicates.

Subsystems are organised into dedicated folders:

- [`Data/context.md`](Data/context.md) – EF Core entities, repositories, and database-specific span bulk inserters.
- [`Monitoring/context.md`](Monitoring/context.md) – instrumentation that tracks receiver throughput and exposes async snapshots.
- [`Services/context.md`](Services/context.md) – gRPC service implementations for OTLP traces/logs/metrics, TraceLens data queries, custom metrics streaming, and the streaming HTTP MCP bridge.
- [`TraceLens/context.md`](TraceLens/context.md) – TraceLens domain logic, extractors, and helper utilities used to interpret telemetry payloads.
- [`Migrations/context.md`](Migrations/context.md) – EF Core migration history describing the storage schema.
- [`opentelemetry/context.md`](opentelemetry/context.md) – vendored OpenTelemetry proto files consumed for code generation.

When modifying this project, update the nearest `context.md` to capture new dependencies, configuration flags, or noteworthy behaviour so future agents can navigate quickly.
