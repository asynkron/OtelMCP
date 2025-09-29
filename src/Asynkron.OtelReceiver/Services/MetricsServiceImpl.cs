using Google.Protobuf;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Metrics.V1;
using OpenTelemetry.Proto.Metrics.V1;
using TraceLens.Infra;
using Asynkron.OtelReceiver.Data;

namespace Asynkron.OtelReceiver.Services;

public class MetricsServiceImpl : MetricsService.MetricsServiceBase
{
    private readonly ModelRepo _repo;

    public MetricsServiceImpl(ModelRepo repo)
    {
        _repo = repo;
    }
    public override async Task<ExportMetricsServiceResponse> Export(ExportMetricsServiceRequest request, ServerCallContext context)
    {

        var metrics = new List<MetricEntity>();
        foreach (var r in request.ResourceMetrics)
        {
            var resourceAttribs = r.Resource.Attributes;
            foreach (var s in r.ScopeMetrics)
            {
                var scopeAttribs = s.Scope.Attributes;

                var allAttribs = scopeAttribs.Concat(resourceAttribs).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                foreach (var m in s.Metrics)
                {
                    ulong start = 0;
                    ulong end = 0;
                    switch (m.DataCase)
                    {
                        case Metric.DataOneofCase.None:
                            break;
                        case Metric.DataOneofCase.Gauge:
                            start = m.Gauge.DataPoints.Min(d => d.TimeUnixNano);
                            end = m.Gauge.DataPoints.Min(d => d.TimeUnixNano);
                            break;
                        case Metric.DataOneofCase.Sum:
                            start = m.Sum.DataPoints.Min(d => d.TimeUnixNano);
                            end = m.Sum.DataPoints.Min(d => d.TimeUnixNano);
                            break;
                        case Metric.DataOneofCase.Histogram:
                            start = m.Histogram.DataPoints.Min(d => d.TimeUnixNano);
                            end = m.Histogram.DataPoints.Min(d => d.TimeUnixNano);
                            break;
                        case Metric.DataOneofCase.ExponentialHistogram:
                            start = m.ExponentialHistogram.DataPoints.Min(d => d.TimeUnixNano);
                            end = m.ExponentialHistogram.DataPoints.Min(d => d.TimeUnixNano);
                            break;
                        case Metric.DataOneofCase.Summary:
                            start = m.Summary.DataPoints.Min(d => d.TimeUnixNano);
                            end = m.Summary.DataPoints.Min(d => d.TimeUnixNano);
                            break;
                        default:
                            break;
                    }

                    var me = new MetricEntity
                    {
                        Proto = m.ToByteArray(),
                        Description = m.Description,
                        Name = m.Name,
                        Unit = m.Unit,
                        StartTimestamp = start,
                        EndTimestamp = end,
                        AttributeMap = allAttribs.Select(kvp => $"{kvp.Key}:{kvp.Value.ToStringValue()}").ToArray(),
                    };
                    metrics.Add(me);
                }
            }

            foreach (var chunk in metrics.Chunk(2000))
                await _repo.SaveMetrics(chunk);
        }
        return new ExportMetricsServiceResponse();
    }
}
