# `TraceLens/ExtendedProtos` Context

Partial class extensions that enrich generated OpenTelemetry metric protobufs:

- [`Metric.cs`](Metric.cs) implements the `IDataPoint` abstraction across histogram, summary, exponential histogram, and number data points. It also adds helper extension methods for grouping, aggregating (sum/average/delta/percentile), and extracting attribute keys.

These helpers are used by TraceLens tooling to normalise metric time series before presentation. Keep this summary in sync when adding new aggregations or helper interfaces.
