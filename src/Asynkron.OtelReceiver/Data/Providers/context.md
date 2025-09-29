# `Data/Providers` Context

This folder abstracts span bulk-insert strategies so each database can optimise writes:

- [`ISpanBulkInserter.cs`](ISpanBulkInserter.cs) – contract invoked by `ModelRepo.SaveTrace` with the ambient `OtelReceiverContext` and a batch of spans.
- [`PostgresSpanBulkInserter.cs`](PostgresSpanBulkInserter.cs) – uses Npgsql's binary `COPY Spans` pipeline within a transaction for high-throughput persistence. Opens the connection and commits only after writing the batch; logs and rethrows on failure.
- [`SqliteSpanBulkInserter.cs`](SqliteSpanBulkInserter.cs) – relies on EF Core's change tracker to enqueue the batch (later flushed by `SaveChangesAsync`). Suitable for SQLite where bulk APIs are absent.

If you introduce a new provider (e.g., SQL Server) or change batching behaviour, mention it here and ensure `Program.cs` selects the correct implementation.
