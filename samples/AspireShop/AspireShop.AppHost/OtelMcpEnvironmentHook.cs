using System;
using System.Linq;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

namespace AspireShop.AppHost;

/// <summary>
/// Applies OTLP exporter environment variables to Aspire project resources so telemetry
/// produced by the sample flows into the bundled OtelMCP collector automatically.
/// </summary>
internal sealed class OtelMcpEnvironmentHook : IDistributedApplicationLifecycleHook
{
    private readonly ILogger<OtelMcpEnvironmentHook> _logger;

    public OtelMcpEnvironmentHook(ILogger<OtelMcpEnvironmentHook> logger)
    {
        _logger = logger;
    }

    public Task AfterEndpointsAllocatedAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken)
    {
        var collectorResource = appModel.Resources
            .OfType<IResourceWithEndpoints>()
            .FirstOrDefault(resource =>
                string.Equals(resource.Name, OtelMcpExtensions.ResourceName, StringComparison.OrdinalIgnoreCase));

        if (collectorResource is null)
        {
            _logger.LogWarning("Unable to locate the OtelMCP resource; telemetry will not be forwarded automatically.");
            return Task.CompletedTask;
        }

        var otlpEndpoint = collectorResource.GetEndpoint(OtelMcpExtensions.OtlpEndpointName);

        foreach (var project in appModel.GetProjectResources())
        {
            if (string.Equals(project.Name, OtelMcpExtensions.ResourceName, StringComparison.OrdinalIgnoreCase))
            {
                // Skip the collector itself – it does not need to export to another endpoint.
                continue;
            }

            project.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                // Always align the Aspire projects with the in-process collector so gRPC telemetry
                // lands in OtelMCP even when developers override variables locally.
                context.EnvironmentVariables["OTEL_EXPORTER_OTLP_ENDPOINT"] = otlpEndpoint.Url;
                context.EnvironmentVariables["OTEL_EXPORTER_OTLP_PROTOCOL"] = "grpc";
            }));
        }

        _logger.LogInformation("Wired {ResourceCount} Aspire project(s) to export telemetry to {Endpoint}.",
            appModel.GetProjectResources().Count(), otlpEndpoint.Url);

        return Task.CompletedTask;
    }
}
