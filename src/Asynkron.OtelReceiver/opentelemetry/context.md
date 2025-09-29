# `opentelemetry` Context

Vendored OpenTelemetry proto definitions used to generate gRPC contracts for traces, logs, metrics, resources, and collector services. These files come from the official OTLP specification and are organised by namespace under [`proto`](proto/context.md).

Regenerate the corresponding C# code (via `Grpc.Tools` or equivalent) if you update these protos. Because they mirror upstream specs, avoid modifying them directly unless syncing with a specific OTLP release. Document any version bumps here.
