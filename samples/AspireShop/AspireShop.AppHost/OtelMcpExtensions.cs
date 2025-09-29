using System;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace AspireShop.AppHost;

/// <summary>
/// Helpers for embedding the OtelMCP receiver into the Aspire Shop distributed application.
/// </summary>
internal static class OtelMcpExtensions
{
    internal const string ResourceName = "otelmcp";
    internal const string OtlpEndpointName = "otel";

    /// <summary>
    /// Registers the OtelMCP receiver project and exposes its OTLP gRPC endpoint so other
    /// Aspire resources can emit telemetry without additional configuration.
    /// </summary>
    public static IResourceBuilder<ProjectResource> AddOtelMcp(this IDistributedApplicationBuilder builder)
    {
        // Ensure the lifecycle hook that injects OTLP environment variables is present once.
        builder.Services.TryAddLifecycleHook<OtelMcpEnvironmentHook>();

        var otelCollector = builder
            .AddProject<Projects.Asynkron_OtelReceiver>(ResourceName)
            .WithEndpoint(OtlpEndpointName, endpoint =>
            {
                endpoint.Port = 4317;
                endpoint.UriScheme = "http";
                endpoint.Protocol = EndpointProtocol.Http2;
            })
            // Force Kestrel to listen on the canonical OTLP gRPC port so Aspire services
            // can discover it via the generated endpoint reference above.
            .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:4317")
            .WithEnvironment("ASPNETCORE_Kestrel__EndpointDefaults__Protocols", "Http2")
            // Use the lightweight SQLite provider by default so the collector does not
            // require any additional infrastructure when launched from the AppHost.
            .WithEnvironment("DatabaseProvider", "Sqlite")
            .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=otelmcp.db");

        return otelCollector;
    }
}
