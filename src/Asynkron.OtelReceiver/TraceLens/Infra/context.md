# `TraceLens/Infra` Context

Infrastructure helpers used across TraceLens components.

- [`ChannelExtensions.cs`](ChannelExtensions.cs) – adds `ReadBatchAsync` for backpressure-friendly channel consumption used by gRPC services.
- [`Extensions.cs`](Extensions.cs) – OTLP-focused helpers (service-name extraction, attribute formatting, hex conversions, nanos→`DateTimeOffset`, etc.).
- [`JsonFlattener.cs`](JsonFlattener.cs) – converts nested JSON into path/value pairs, enabling virtual attributes when TraceLens encounters JSON payloads inside span attributes.
- [`PlantUmlBuiltInStyles.cs`](PlantUmlBuiltInStyles.cs) – enumerates known PlantUML themes; useful when generating architecture diagrams.
- [`Translator.cs`](Translator.cs) – `OtelTranslator` builds TraceLens models from OTLP spans/logs. It deduplicates spans, links logs/events, runs JSON flattening, and produces `TraceLensModel` instances with optional diagnostics/multi-root support.

These helpers are tightly coupled to the domain classes described in [`../Model/context.md`](../Model/context.md). Update both contexts when extending translation logic or adding infrastructure utilities.
