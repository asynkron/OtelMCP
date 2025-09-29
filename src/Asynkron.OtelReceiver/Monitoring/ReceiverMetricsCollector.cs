using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

namespace Asynkron.OtelReceiver.Monitoring;

/// <summary>
/// Collects live statistics from the receiver and exposes them via <see cref="System.Diagnostics.Metrics"/> counters
/// as well as an async stream that can be consumed by gRPC services or other observers.
/// </summary>
public interface IReceiverMetricsCollector
{
    void RecordSpansReceived(long count);
    void RecordSpansStored(long count);
    void RecordLogsReceived(long count);
    void RecordLogsStored(long count);
    void RecordMetricsReceived(long count);
    void RecordMetricsStored(long count);

    IAsyncEnumerable<ReceiverMetricsSnapshot> WatchAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Represents an immutable snapshot of the receiver's throughput counters.
/// </summary>
/// <param name="SpansReceived">Total number of spans received by the OTLP ingestion endpoints.</param>
/// <param name="SpansStored">Total number of spans persisted to the backing store.</param>
/// <param name="LogsReceived">Total number of log records received.</param>
/// <param name="LogsStored">Total number of log records stored.</param>
/// <param name="MetricsReceived">Total number of metrics payloads received.</param>
/// <param name="MetricsStored">Total number of metrics payloads stored.</param>
public readonly record struct ReceiverMetricsSnapshot(
    long SpansReceived,
    long SpansStored,
    long LogsReceived,
    long LogsStored,
    long MetricsReceived,
    long MetricsStored);

/// <summary>
/// Default implementation backed by <see cref="Meter"/> counters and a broadcaster for live updates.
/// </summary>
public class ReceiverMetricsCollector : IReceiverMetricsCollector, IDisposable
{
    private readonly Meter _meter;
    private readonly Counter<long> _spansReceivedCounter;
    private readonly Counter<long> _spansStoredCounter;
    private readonly Counter<long> _logsReceivedCounter;
    private readonly Counter<long> _logsStoredCounter;
    private readonly Counter<long> _metricsReceivedCounter;
    private readonly Counter<long> _metricsStoredCounter;

    private long _spansReceived;
    private long _spansStored;
    private long _logsReceived;
    private long _logsStored;
    private long _metricsReceived;
    private long _metricsStored;

    private readonly ConcurrentDictionary<Guid, Channel<ReceiverMetricsSnapshot>> _subscribers = new();

    public ReceiverMetricsCollector()
    {
        _meter = new Meter("Asynkron.OtelReceiver", "1.0.0");
        _spansReceivedCounter = _meter.CreateCounter<long>("receiver.spans.received");
        _spansStoredCounter = _meter.CreateCounter<long>("receiver.spans.stored");
        _logsReceivedCounter = _meter.CreateCounter<long>("receiver.logs.received");
        _logsStoredCounter = _meter.CreateCounter<long>("receiver.logs.stored");
        _metricsReceivedCounter = _meter.CreateCounter<long>("receiver.metrics.received");
        _metricsStoredCounter = _meter.CreateCounter<long>("receiver.metrics.stored");
    }

    public void Dispose() => _meter.Dispose();

    public void RecordSpansReceived(long count) => Record(ref _spansReceived, count, _spansReceivedCounter);
    public void RecordSpansStored(long count) => Record(ref _spansStored, count, _spansStoredCounter);
    public void RecordLogsReceived(long count) => Record(ref _logsReceived, count, _logsReceivedCounter);
    public void RecordLogsStored(long count) => Record(ref _logsStored, count, _logsStoredCounter);
    public void RecordMetricsReceived(long count) => Record(ref _metricsReceived, count, _metricsReceivedCounter);
    public void RecordMetricsStored(long count) => Record(ref _metricsStored, count, _metricsStoredCounter);

    public async IAsyncEnumerable<ReceiverMetricsSnapshot> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<ReceiverMetricsSnapshot>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = true
        });

        var id = Guid.NewGuid();
        _subscribers[id] = channel;

        // Emit the current snapshot immediately so that new subscribers get an initial view.
        channel.Writer.TryWrite(CreateSnapshot());

        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var snapshot))
                {
                    yield return snapshot;
                }
            }
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
        }
    }

    private void Record(ref long field, long count, Counter<long> counter)
    {
        if (count <= 0)
        {
            return;
        }

        counter.Add(count);
        Interlocked.Add(ref field, count);
        Broadcast();
    }

    private void Broadcast()
    {
        var snapshot = CreateSnapshot();
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(snapshot);
        }
    }

    private ReceiverMetricsSnapshot CreateSnapshot() => new(
        Interlocked.Read(ref _spansReceived),
        Interlocked.Read(ref _spansStored),
        Interlocked.Read(ref _logsReceived),
        Interlocked.Read(ref _logsStored),
        Interlocked.Read(ref _metricsReceived),
        Interlocked.Read(ref _metricsStored));
}
