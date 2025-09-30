using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Asynkron.OtelReceiver.Data;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Tracelens.Proto.V1;

namespace Asynkron.OtelReceiver.Services;

/// <summary>
/// Minimal Model Context Protocol (MCP) handler that mirrors the TraceLens data gRPC surface
/// over a newline-delimited JSON (NDJSON) streaming HTTP endpoint.
/// </summary>
public static class McpStreamingEndpoint
{
    private static readonly JsonParser Parser = new(JsonParser.Settings.Default.WithIgnoreUnknownFields(true));

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private static readonly JsonFormatter Formatter = new(JsonFormatter.Settings.Default
        .WithFormatDefaultValues(false)
        .WithPreserveProtoFieldNames(false));

    private static readonly Dictionary<string, Func<ModelRepo, JsonElement, Task<IMessage>>> CommandHandlers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["getSearchData"] = async (repo, payload) => (IMessage)await repo.GetSearchData(Parse<GetSearchDataRequest>(payload)),
            ["getValuesForTag"] = async (repo, payload) => (IMessage)await repo.GetValuesForTag(Parse<GetValuesForTagRequest>(payload)),
            ["searchTraces"] = async (repo, payload) => (IMessage)await repo.SearchTraces(Parse<SearchTracesRequest>(payload)),
            ["getServiceMapComponents"] = async (repo, payload) => (IMessage)await repo.GetServiceMapComponents(Parse<GetServiceMapComponentsRequest>(payload)),
            ["getComponentMetadata"] = async (repo, payload) => (IMessage)await repo.GetComponentMetadata(Parse<GetComponentMetadataRequest>(payload)),
            ["setComponentMetadata"] = async (repo, payload) => (IMessage)await repo.SetComponentMetadata(Parse<SetComponentMetadataRequest>(payload)),
            ["getMetadataForComponent"] = async (repo, payload) => (IMessage)await repo.GetMetadataForComponent(Parse<GetMetadataForComponentRequest>(payload)),
            ["getSnapshot"] = async (repo, payload) => (IMessage)await repo.GetSnapshot(Parse<GetSnapshotRequest>(payload)),
            ["getMetricNames"] = async (repo, payload) => (IMessage)await repo.GetMetricNames(Parse<GetMetricNamesRequest>(payload)),
            ["getMetric"] = async (repo, payload) => (IMessage)await repo.GetMetric(Parse<GetMetricRequest>(payload))
        };

    public static RouteHandlerBuilder MapMcpStreamingEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/mcp/stream", HandleAsync)
            .WithName("McpStreamingEndpoint");
    }

    private static async Task HandleAsync(HttpContext context, ModelRepo repo, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("McpStreaming");

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/x-ndjson";

        await WriteHandshakeAsync(context.Response, logger, cancellationToken);

        await foreach (var command in ReadCommandsAsync(context.Request.Body, cancellationToken))
        {
            var payloadForLog = FormatPayloadForLog(command.Payload);
            logger.LogInformation("Received MCP command {Command} (Id: {Id}) with payload: {Payload}", command.Command, command.Id, payloadForLog);
            McpResponse envelope;
            try
            {
                var result = await ExecuteCommandAsync(command, repo);
                envelope = McpResponse.ForResult(command.Id, command.Command, result);
                var responsePayload = envelope.Result is JsonElement resultElement
                    ? resultElement.GetRawText()
                    : "{}";
                logger.LogInformation("MCP command {Command} (Id: {Id}) succeeded with response: {Response}", command.Command, command.Id, responsePayload);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to execute MCP command {Command}", command.Command);
                envelope = McpResponse.ForError(command.Id, command.Command, ex.Message);
            }

            await WriteEnvelopeAsync(context.Response, envelope, cancellationToken);
            logger.LogInformation("Sent MCP response for {Command} (Id: {Id})", command.Command, command.Id);
        }
    }

    private static async Task<IMessage> ExecuteCommandAsync(McpCommand command, ModelRepo repo)
    {
        if (!CommandHandlers.TryGetValue(command.Command, out var handler))
        {
            throw new InvalidOperationException($"Unknown MCP command '{command.Command}'.");
        }

        return await handler(repo, command.Payload);
    }

    private static async Task WriteHandshakeAsync(HttpResponse response, ILogger logger, CancellationToken cancellationToken)
    {
        var handshake = new McpHandshake
        {
            Type = "ready",
            Protocol = "mcp",
            Version = "1.0",
            Capabilities = new[] { "commands" },
            Commands = CommandHandlers.Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray()
        };

        await JsonSerializer.SerializeAsync(response.Body, handshake, SerializerOptions, cancellationToken);
        await response.WriteAsync("\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
        logger.LogInformation("Sent MCP handshake advertising commands: {Commands}", string.Join(", ", handshake.Commands));
    }

    private static async Task WriteEnvelopeAsync(HttpResponse response, McpResponse envelope, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(response.Body, envelope, SerializerOptions, cancellationToken);
        await response.WriteAsync("\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }

    private static async IAsyncEnumerable<McpCommand> ReadCommandsAsync(Stream requestBody, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(requestBody, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            McpCommand command;
            try
            {
                command = JsonSerializer.Deserialize<McpCommand>(line, SerializerOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("Invalid MCP command payload.", ex);
            }

            if (string.IsNullOrWhiteSpace(command.Command))
            {
                throw new InvalidOperationException("MCP command must include a command name.");
            }

            yield return command;
        }
    }

    private static string FormatPayloadForLog(JsonElement payload)
    {
        return payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? "<empty>"
            : payload.GetRawText();
    }

    private static T Parse<T>(JsonElement payload)
        where T : IMessage<T>, new()
    {
        if (payload.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new T();
        }

        var json = payload.GetRawText();
        return Parser.Parse<T>(json);
    }

    private readonly record struct McpCommand(string? Id, string Command, JsonElement Payload);

    private sealed record McpHandshake
    {
        public string Type { get; init; } = string.Empty;
        public string Protocol { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string[] Capabilities { get; init; } = Array.Empty<string>();
        public string[] Commands { get; init; } = Array.Empty<string>();
    }

    private sealed record McpResponse
    {
        public string Type { get; init; } = "result";
        public string Command { get; init; } = string.Empty;
        public string? Id { get; init; }
        public JsonElement? Result { get; init; }
        public McpError? Error { get; init; }

        public static McpResponse ForResult(string? id, string command, IMessage result)
        {
            var json = Formatter.Format(result);
            using var document = JsonDocument.Parse(json);
            var normalized = NormalizeToCamelCase(document.RootElement);
            return new McpResponse
            {
                Type = "result",
                Command = command,
                Id = id,
                Result = normalized
            };
        }

        public static McpResponse ForError(string? id, string command, string message)
        {
            return new McpResponse
            {
                Type = "error",
                Command = command,
                Id = id,
                Error = new McpError
                {
                    Code = "command_failed",
                    Message = message
                }
            };
        }
    }

    private sealed record McpError
    {
        public string Code { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    private static JsonElement NormalizeToCamelCase(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WriteCamelCase(writer, element);
        }

        var sequence = new ReadOnlySequence<byte>(buffer.WrittenMemory);
        using var normalized = JsonDocument.Parse(sequence);
        return normalized.RootElement.Clone();
    }

    private static void WriteCamelCase(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    var propertyName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                    writer.WritePropertyName(propertyName);
                    WriteCamelCase(writer, property.Value);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCamelCase(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
