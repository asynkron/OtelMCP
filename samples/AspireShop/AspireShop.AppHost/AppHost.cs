using System;
using System.Net;
using AspireShop.AppHost;
using Microsoft.Extensions.Configuration;

DisableProcessProxies();

var builder = DistributedApplication.CreateBuilder(args);

// Host the OtelMCP receiver alongside the Aspire sample so telemetry flows locally by default.
var otelCollector = builder.AddOtelMcp();

var simpleMode = builder.Configuration.GetValue("OtelMcp:SimpleMode", false);

if (simpleMode)
{
    builder.AddProject<Projects.AspireShop_LoadGenerator>("loadgenerator")
        .WaitFor(otelCollector);

    builder.Build().Run();
    return;
}

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

if (builder.ExecutionContext.IsRunMode)
{
    // Data volumes don't work on ACA for Postgres so only add when running
    postgres.WithDataVolume();
}

var catalogDb = postgres.AddDatabase("catalogdb");

var basketCache = builder.AddRedis("basketcache")
    .WithDataVolume()
    .WithRedisCommander();

var catalogDbManager = builder.AddProject<Projects.AspireShop_CatalogDbManager>("catalogdbmanager")
    .WithReference(catalogDb)
    .WaitFor(catalogDb)
    .WaitFor(otelCollector)
    .WithHttpHealthCheck("/health")
    .WithHttpCommand("/reset-db", "Reset Database", commandOptions: new() { IconName = "DatabaseLightning" });

var catalogService = builder.AddProject<Projects.AspireShop_CatalogService>("catalogservice")
    .WithReference(catalogDb)
    .WaitFor(catalogDbManager)
    .WaitFor(otelCollector)
    .WithHttpHealthCheck("/health");

var basketService = builder.AddProject<Projects.AspireShop_BasketService>("basketservice")
    .WithReference(basketCache)
    .WaitFor(basketCache)
    .WaitFor(otelCollector);

builder.AddProject<Projects.AspireShop_Frontend>("frontend")
    .WithExternalHttpEndpoints()
    .WithUrlForEndpoint("https", url => url.DisplayText = "Online Store (HTTPS)")
    .WithUrlForEndpoint("http", url => url.DisplayText = "Online Store (HTTP)")
    .WithHttpHealthCheck("/health")
    .WithReference(basketService)
    .WithReference(catalogService)
    .WaitFor(catalogService)
    .WaitFor(otelCollector);

builder.Build().Run();

static void DisableProcessProxies()
{
    // Aspire's DCP client talks to local daemons, so proxy interception only causes startup failures
    // in sandboxed environments that inject mandatory HTTP(S) proxies.
    var proxyVariables = new[]
    {
        "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY",
        "http_proxy", "https_proxy", "all_proxy", "no_proxy"
    };

    foreach (var variable in proxyVariables)
    {
        Environment.SetEnvironmentVariable(variable, null);
    }

#pragma warning disable SYSLIB0014 // WebRequest is obsolete but still controls the default proxy used by SocketsHttpHandler.
    WebRequest.DefaultWebProxy = null;
#pragma warning restore SYSLIB0014
}
