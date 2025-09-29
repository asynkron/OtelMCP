# TraceLens Search Response Enrichment

`SearchTraces` now returns additional context that helps downstream tooling explain why a trace matched and, when requested, provides the original OTLP span payloads.

## Request flags

- `include_span_protos` (bool): set to `true` to embed the full `opentelemetry.proto.trace.v1.Span` messages for every span in each matched trace. The data is read directly from the persisted `SpanWithService` blobs, so leave the flag unset when you only need the overview to avoid extra payload size.

## Response fields

Each `SearchTraceResult` now contains two extra collections:

- `attribute_clauses`: a list of `AttributeClauseMatch` entries. Each clause records
  - `clause`: a human-readable representation of the filter expression that succeeded (e.g., `tag:http.method=GET`).
  - `satisfied`: `true` if the persisted spans satisfied the clause.
  - `matches`: the individual `AttributeMatch` items (span id, key, value) that satisfied the clause.
- `spans`: present when `include_span_protos` was set on the request and populated with the OTLP `Span` protos for each matched span.

Tooling that previously only inspected the `trace` overview and aggregated log/span counts can now surface:

- Which filter clause(s) caused the trace to be returned (for annotating UI chips, CLI output, etc.).
- The exact span attributes that satisfied each clause (useful for building “why did I see this trace?” explanations).
- The raw span protos without issuing a follow-up call, enabling quick drill-down panes in dashboards.

Update existing consumers to read the new fields opportunistically so older servers remain compatible: treat missing fields as “feature unavailable” and fall back to previous behaviour.
