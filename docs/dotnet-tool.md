# `dotnet-otelmcp` Global Tool

The receiver project can be distributed as a .NET global tool named `dotnet-otelmcp`. Installing the tool provides the `dotnet otelmcp` command that boots the OTLP receiver, applies EF Core migrations, and exposes the optional metrics console client.

## Installation

Install from NuGet once the package is published:

```bash
# Installs the published package from nuget.org
dotnet tool install --global dotnet-otelmcp
```

During local development you can install directly from a packed `.nupkg` folder:

```bash
# Replace ./nupkg with the folder produced by `dotnet pack`
dotnet tool install --global --add-source ./nupkg dotnet-otelmcp
```

Ensure that the global tool path is on your `PATH` (typically `~/.dotnet/tools`).

## Running the Receiver

Starting the server uses the default `appsettings.json` connection strings bundled with the tool. On first launch the Entity Framework Core migrations execute automatically to create or upgrade the backing database.

```bash
# Launch the OTLP receiver (the global tool shim is also exposed as `otelmcp`)
dotnet otelmcp
```

> **Note:** The `dotnet` driver resolves to the same shim the global tool installs. If your shell cannot locate the command via `dotnet otelmcp`, invoke `otelmcp` directly.

### Selecting a Bind Address

Use `--address` to specify the HTTP/gRPC listener (defaults to `http://localhost:5000`).

```bash
# Bind to all interfaces on port 7171
dotnet otelmcp --address http://0.0.0.0:7171
```

## Metrics Console

The tool also includes a live metrics console implemented with Spectre.Console.

```bash
# Connect the metrics console to a running receiver instance
dotnet otelmcp --metrics-client --address http://localhost:5000
```

When `--metrics-client` is provided the process connects to the server instead of hosting it, streaming ingestion counters in real time.
