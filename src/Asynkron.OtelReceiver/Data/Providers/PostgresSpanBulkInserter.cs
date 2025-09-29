using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Asynkron.OtelReceiver.Data.Providers;

/// <summary>
/// Uses PostgreSQL's binary <c>COPY</c> support to quickly persist spans.
/// </summary>
public class PostgresSpanBulkInserter(ILogger<PostgresSpanBulkInserter> logger) : ISpanBulkInserter
{
    private readonly ILogger<PostgresSpanBulkInserter> _logger = logger;

    public async Task InsertAsync(OtelReceiverContext context, IReadOnlyCollection<SpanEntity> spans, CancellationToken cancellationToken = default)
    {
        if (spans.Count == 0)
        {
            return;
        }

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var writer = await connection.BeginBinaryImportAsync(
            """
            COPY "Spans" (
                    "SpanId",
                    "StartTimestamp",
                    "EndTimestamp",
                    "TraceId",
                    "ParentSpanId",
                    "ServiceName",
                    "OperationName",
                    "AttributeMap",
                    "Events",
                    "Proto") FROM STDIN (FORMAT BINARY)
            """);

        try
        {
            foreach (var span in spans)
            {
                await writer.StartRowAsync(cancellationToken);
                await writer.WriteAsync(span.SpanId, NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(span.StartTimestamp, NpgsqlDbType.Bigint, cancellationToken);
                await writer.WriteAsync(span.EndTimestamp, NpgsqlDbType.Bigint, cancellationToken);
                await writer.WriteAsync(span.TraceId, NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(span.ParentSpanId, NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(span.ServiceName, NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(span.OperationName, NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(span.AttributeMap, NpgsqlDbType.Array | NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(span.Events, NpgsqlDbType.Array | NpgsqlDbType.Text, cancellationToken);
                await writer.WriteAsync(span.Proto, NpgsqlDbType.Bytea, cancellationToken);
            }

            await writer.CompleteAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk insert spans using PostgreSQL COPY");
            throw;
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
