# `trace/v1` Context

- `trace.proto` defines OTLP span messages, events, links, and status fields. These types feed directly into span persistence (`ModelRepo.SaveTrace`) and TraceLens translation logic (`Translator.cs`).
