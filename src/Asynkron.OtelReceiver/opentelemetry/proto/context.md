# `opentelemetry/proto` Context

Mirrors the OpenTelemetry Protocol (OTLP) directory structure. Each subfolder exposes versioned `.proto` files consumed by `Grpc.Tools`.

Subdirectories:
- [`collector/context.md`](collector/context.md) – OTLP collector service RPC definitions.
- [`common/context.md`](common/context.md) – shared attribute/value messages reused across signal types.
- [`logs/context.md`](logs/context.md) – OTLP log record schemas.
- [`metrics/context.md`](metrics/context.md) – OTLP metric schemas.
- [`resource/context.md`](resource/context.md) – resource descriptors.
- [`trace/context.md`](trace/context.md) – OTLP trace span schemas.

Ensure any upstream updates preserve folder layout so generated namespaces remain stable.
