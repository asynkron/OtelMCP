using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asynkron.OtelReceiver.Data;

namespace Asynkron.OtelReceiver.Data.Providers;

/// <summary>
/// Abstraction responsible for inserting a batch of spans into the underlying database.
/// Each database provider can choose the optimal strategy (binary import, batched EF operations, etc.).
/// </summary>
public interface ISpanBulkInserter
{
    Task InsertAsync(
        OtelReceiverContext context,
        IReadOnlyCollection<SpanEntity> spans,
        IReadOnlyCollection<SpanAttributeValueEntity> attributes,
        CancellationToken cancellationToken = default);
}
