# `Asynkron.OtelReceiver.Tests` Context

xUnit project covering repository and SQLite bulk insert behaviour.

- [`SqliteSpanBulkInserterTests.cs`](SqliteSpanBulkInserterTests.cs) exercises:
  - `SqliteSpanBulkInserter.InsertAsync` to ensure batches are persisted correctly when `SaveChangesAsync` is invoked.
  - `ModelRepo.SaveTrace` attribute/span-name persistence using an in-memory SQLite database and `ReceiverMetricsCollector`.
  - `ModelRepo.SaveLogs`/`SaveMetrics` to verify log and metric entities are stored.

Test infrastructure: `SqliteTestDatabase` creates a shared in-memory SQLite connection, builds `OtelReceiverContext`, and exposes an `IDbContextFactory` for repository construction.

Add new tests here when changing data access patterns or metrics recording logic, and update this context with any new fixtures/utilities.
