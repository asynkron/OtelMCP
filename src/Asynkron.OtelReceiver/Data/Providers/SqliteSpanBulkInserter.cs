using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asynkron.OtelReceiver.Data;
using Microsoft.EntityFrameworkCore;

namespace Asynkron.OtelReceiver.Data.Providers;

/// <summary>
/// Uses EF Core's batched change tracker which maps well to SQLite and keeps conversions consistent.
/// </summary>
public class SqliteSpanBulkInserter : ISpanBulkInserter
{
    public async Task InsertAsync(
        OtelReceiverContext context,
        IReadOnlyCollection<SpanEntity> spans,
        IReadOnlyCollection<SpanAttributeValueEntity> attributes,
        CancellationToken cancellationToken = default)
    {
        if (spans.Count > 0)
        {
            // SQLite does not expose a COPY equivalent, but EF Core can still send the inserts as a single transaction.
            await context.Spans.AddRangeAsync(spans, cancellationToken);
        }

        if (attributes.Count > 0)
        {
            await context.SpanAttributeValues.AddRangeAsync(attributes, cancellationToken);
        }
    }
}
