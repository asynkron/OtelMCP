using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Asynkron.OtelReceiver.Data.Providers;

/// <summary>
/// Uses EF Core's batched change tracker which maps well to SQLite and keeps conversions consistent.
/// </summary>
public class SqliteSpanBulkInserter : ISpanBulkInserter
{
    public async Task InsertAsync(OtelReceiverContext context, IReadOnlyCollection<SpanEntity> spans, CancellationToken cancellationToken = default)
    {
        if (spans.Count == 0)
        {
            return;
        }

        // SQLite does not expose a COPY equivalent, but EF Core can still send the inserts as a single transaction.
        await context.Spans.AddRangeAsync(spans, cancellationToken);
    }
}
