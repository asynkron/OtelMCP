# `TraceLens/Model` Context

Domain objects that represent TraceLens' understanding of OTLP telemetry.

## Key types
- [`TraceLensModel.cs`](TraceLensModel.cs) – roots the span graph, links parent/child relationships, injects diagnostic log entries, and provides timeline helpers (`GetStartPercentD`, `GetWidthPercentD`, etc.). Maintains a concurrent key cache used for deterministic component IDs.
- [`Span.cs`](Span.cs) – wraps OTLP spans with derived properties (duration, error detection, attribute lookups) and delegates classification to extractors.
- [`SpanDescription.cs`](SpanDescription.cs) – describes how a span maps to a component/group/operation/call; used to build UI and component models.
- [`ComponentModel.cs`](ComponentModel.cs) – builds unique `Component`/`Group` sets and inter-component `Call` edges from a `TraceLensModel`, reusing metadata when available.
- [`LogEntry.cs`](LogEntry.cs), [`Metric.cs`](Metric.cs) – representations of logs/metrics tied to spans.
- [`Attributes.cs`](Attributes.cs) – `AttributeString` handles JSON/base64 decoding heuristics for attribute inspection.

## Extractors
Specialised heuristics in [`Extractors/context.md`](Extractors/context.md) populate span descriptions for common technologies (HTTP, databases, queues, Orleans, Proto.Actor, Azure, tests, etc.). The first successful extractor with the smallest depth wins.

## Supporting types
Records such as [`Component.cs`](Component.cs), [`Group.cs`](Group.cs), [`Call.cs`](Call.cs), and enums ([`ComponentKind.cs`](ComponentKind.cs), [`CallKind.cs`](CallKind.cs)) define the relationships assembled by `ComponentModel`.

When adding new span interpretations or metadata sources, document the change here and (if applicable) inside the extractor context.
