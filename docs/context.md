# `docs` Directory Context

This folder collects user-facing operational guides, integration notes, and research summaries that don't fit directly into the source tree. Each markdown file explains a workflow (e.g., wiring external systems to the receiver) so future maintainers can reuse the findings without repeating exploratory work.

Current entries:

- [`aspire-sample-integration.md`](aspire-sample-integration.md) – step-by-step instructions for running the vendored .NET Aspire Shop sample against the receiver to generate realistic OTLP traffic.
- [`dotnet-tool.md`](dotnet-tool.md) – installation and usage notes for the `dotnet-otelmcp` global tool, including `--address` and `--metrics-client` guidance.
- [`tracelens-search.md`](tracelens-search.md) – outlines the composable `SearchTraces` filter expression so API consumers can combine service and attribute predicates with AND/OR logic.
- [`tracelens-search-response.md`](tracelens-search-response.md) – documents the enriched `SearchTraces` response payload (attribute clause matches and optional span protos) so UI/CLI clients can adopt the new metadata.

Add a new document whenever you complete an investigation whose results will be referenced again. Keep filenames descriptive and link to them from parent `context.md` files when relevant.
