# Repository Context

This repository hosts the **Asynkron OTLP Receiver**, a .NET 8 solution for ingesting OpenTelemetry traces, logs, and metrics, persisting them to a relational store, and exposing TraceLens tooling for analysing telemetry.

## Solution layout
- [`Asynkron.OtelReceiver.sln`](Asynkron.OtelReceiver.sln) – entry point for building the receiver and associated tests.
- [`src/context.md`](src/context.md) – code for the ASP.NET Core receiver, TraceLens utilities, database access, and generated OTLP protobuf definitions.
- [`tests/context.md`](tests/context.md) – unit test projects that exercise the receiver infrastructure.
- [`docker-compose.yml`](docker-compose.yml) – container recipe for the receiver with SQLite-backed storage.
- [`docs/context.md`](docs/context.md) – repository guides and research notes, including integration walkthroughs.
- [`samples/context.md`](samples/context.md) – vendored telemetry generators (e.g., the .NET Aspire Shop sample) for producing OTLP traffic during development.
- [`AGENTS.md`](AGENTS.md) – workflow guidance for AI maintainers. Please read it together with this `context.md` and update both when altering repository conventions.

## Key capabilities
- gRPC endpoints implementing OTLP trace, log, metric, and custom receiver-metrics services.
- Entity Framework Core models and migrations backing SQLite storage.
- TraceLens domain model (component/group extraction, timeline calculations, metrics helpers) used to interpret OTLP payloads.
- Generated OpenTelemetry protocol buffers that define the gRPC surface area consumed by the receiver.

## Where to look next
- Need to understand the application host? See [`src/Asynkron.OtelReceiver/context.md`](src/Asynkron.OtelReceiver/context.md).
- Investigating telemetry ingestion or storage? Explore [`src/Asynkron.OtelReceiver/Services/context.md`](src/Asynkron.OtelReceiver/Services/context.md) and [`src/Asynkron.OtelReceiver/Data/context.md`](src/Asynkron.OtelReceiver/Data/context.md).
- Working on TraceLens modelling utilities? Start with [`src/Asynkron.OtelReceiver/TraceLens/context.md`](src/Asynkron.OtelReceiver/TraceLens/context.md).
- Updating OpenTelemetry protobufs? Begin at [`src/Asynkron.OtelReceiver/opentelemetry/context.md`](src/Asynkron.OtelReceiver/opentelemetry/context.md).

Keep the directory-level `context.md` files aligned with the actual code: whenever you move or significantly change behaviour, update the closest context file (and link to deeper ones when helpful).
