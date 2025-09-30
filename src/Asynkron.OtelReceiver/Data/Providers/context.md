# `Data/Providers` Context

This folder abstracts the span bulk-insert strategy used by the SQLite-backed receiver:

- [`ISpanBulkInserter.cs`](ISpanBulkInserter.cs) – contract invoked by `ModelRepo.SaveTrace` with the ambient `OtelReceiverContext`, a batch of spans, and the normalised span-attribute rows that need to land alongside them.
- [`SqliteSpanBulkInserter.cs`](SqliteSpanBulkInserter.cs) – relies on EF Core's change tracker to enqueue the spans and corresponding `SpanAttributeValues` (later flushed by `SaveChangesAsync`). Suitable for SQLite where bulk APIs are absent.

If you introduce another database provider, document it here and ensure `Program.cs` selects the correct implementation.
