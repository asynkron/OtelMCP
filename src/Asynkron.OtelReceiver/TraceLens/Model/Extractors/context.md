# `TraceLens/Model/Extractors` Context

Extractors translate raw spans into `SpanDescription` instances. Each implements [`IExtractor`](IExtractor.cs) and can return a match along with a "depth" score (lower wins). The ordered list in [`../Span.cs`](../Span.cs) determines evaluation priority.

## Implemented extractors
- [`RootExtractor`](RootExtractor.cs) – identifies the synthetic root span.
- [`TemporalExtractor`](TemporalExtractor.cs) – detects workflows scheduled by Temporal.io.
- [`OrleansExtractor`](OrleansExtractor.cs) – maps Orleans grains and calls.
- [`RpcExtractor`](RpcExtractor.cs) – generic RPC spans based on `rpc.*` attributes.
- [`ProtoActorExtractor`](ProtoActorExtractor.cs) & [`ProtoActorChildExtractor`](ProtoActorChildExtractor.cs) – recognise Proto.Actor messaging patterns.
- [`ProtoActorEventExtractor`](ProtoActorEventExtractor.cs) – surfaces Proto.Actor event spans.
- [`DbExtractor`](DbExtractor.cs)`/`[`DbStatementExtractor`](DbStatementExtractor.cs)`/`[`ExternalHttpEndpointExtractor`](ExternalHttpEndpointExtractor.cs)`/`[`HttpEndpointExtractor`](HttpEndpointExtractor.cs)`/`[`HttpRequestExtractor`](HttpRequestExtractor.cs)` – handle databases and HTTP client/server spans.
- [`QueueExtractor`](QueueExtractor.cs)`/`[`QueueConsumerExtractor`](QueueConsumerExtractor.cs)` – classify messaging interactions.
- [`AzureExtractor`](AzureExtractor.cs)` – Azure-specific spans (Functions, Service Bus, etc.).
- [`TestExtractor`](TestExtractor.cs)` – special cases used in tests/fixtures.
- [`AkkaExtractor`](AkkaExtractor.cs)` – actor system mapping.

If you add a new extractor:
1. Implement `IExtractor` and return a meaningful depth.
2. Insert it into `Span.Extractors` in the desired priority.
3. Update this list with a brief description and link any cross-dependencies.
