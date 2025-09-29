# `src` Context

Source projects compiled by the solution live here. The primary project is [`Asynkron.OtelReceiver`](Asynkron.OtelReceiver/context.md), which hosts the OTLP gRPC server, storage layer, TraceLens tooling, and generated protobuf definitions.

Other notable items:
- [`Asynkron.OtelReceiver.sln`](../Asynkron.OtelReceiver.sln) references this folder exclusively.
- Subdirectories mirror bounded concerns; follow each linked `context.md` to dive deeper.
  - [`Asynkron.OtelReceiver/context.md`](Asynkron.OtelReceiver/context.md)
