# `Asynkron.OtelReceiver.Tests` Context

xUnit project covering repository persistence logic, SQLite bulk insert behaviour, and in-process gRPC ingestion tests.

- [`SqliteSpanBulkInserterTests.cs`](SqliteSpanBulkInserterTests.cs) exercises:
  - `SqliteSpanBulkInserter.InsertAsync` to ensure batches are persisted correctly when `SaveChangesAsync` is invoked.
  - `ModelRepo.SaveTrace` attribute/span-name persistence using an in-memory SQLite database and `ReceiverMetricsCollector`.
  - `ModelRepo.SaveLogs`/`SaveMetrics` to verify log and metric entities are stored.
- [`OtelGrpcIngestionTests.cs`](OtelGrpcIngestionTests.cs) spins up the full ASP.NET Core host with `WebApplicationFactory`, pushes fake OTLP spans/logs/metrics through the gRPC endpoints, and asserts the database captures the payloads.

Test infrastructure: `SqliteTestDatabase` creates a shared in-memory SQLite connection for data-layer unit tests, while `OtelReceiverApplicationFactory` provisions a temporary SQLite file and HTTP/2 gRPC channel for end-to-end ingestion scenarios.

Add new tests here when changing data access patterns, metrics recording logic, or gRPC ingestion behaviour, and update this context with any new fixtures/utilities.
