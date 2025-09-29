

using OpenTelemetry.Proto.Common.V1;
using TraceLens;
// ReSharper disable once CheckNamespace
using Google.Protobuf.Collections;

namespace OpenTelemetry.Proto.Metrics.V1;

public partial class HistogramDataPoint: IDataPoint
{
    public double Value => Sum;
}

public partial class SummaryDataPoint: IDataPoint
{
    public double Value => Sum;
}

public partial class ExponentialHistogramDataPoint: IDataPoint
{
    public double Value => Sum;
}

public partial class NumberDataPoint : IDataPoint
{
    public double Value => HasAsDouble ? AsDouble : AsInt;
}

public interface IDataPoint
{
    double Value { get; }
    
    public RepeatedField<KeyValue> Attributes { get; }
    
    ulong TimeUnixNano { get; }
}

public class DataPoint : IDataPoint
{
    public double Value { get; set; }
    public ulong TimeUnixNano { get; set; }

    public RepeatedField<KeyValue> Attributes { get; } = new();
}

public static class MetricExtensions
{
    public static string[] GetAttributes(this IEnumerable<IDataPoint> points)
    {
        HashSet<string> keys = [];
        foreach (var p in points)
        {
            foreach (var kvp in p.Attributes)
            {
                keys.Add(kvp.Key);
            }
        }

        return keys.ToArray();
    }
    public static string GetMetricDescription(this IEnumerable<Metric> metrics)
    {
        if (metrics is null)
        {
            return "";
        }
        
        var first = metrics.FirstOrDefault();
        if (first is null)
        {
            return "";
        }
        
        return first.DataCase switch
        {
            Metric.DataOneofCase.None => "",
            Metric.DataOneofCase.Gauge => "Gauge",
            Metric.DataOneofCase.Sum =>  "Sum " + first.Sum.AggregationTemporality,
            Metric.DataOneofCase.Histogram => "Histogram " + first.Histogram.AggregationTemporality,
            Metric.DataOneofCase.ExponentialHistogram => "Exponential Histogram " + first.ExponentialHistogram.AggregationTemporality,
            Metric.DataOneofCase.Summary => "Summary",
            _ => ""
        };
    }

    public static IDataPoint[] ToSumDataPoints(this IEnumerable<IDataPoint> points, TimeSpan bucketSize)
    {
        var groups = points.GroupBy(p => p.TimeUnixNano.UnixNanosToDateTimeOffset().RoundToNearestTimeSpan(bucketSize));
        var res = (
                from g in groups
                select new DataPoint
                {
                    Value = g.Sum(p => p.Value),
                    TimeUnixNano = g.Key.ToUnixTimeNanoseconds(),
                }
            )
            .Cast<IDataPoint>()
            .ToList();

        return res.ToArray();
    }
    
    public static IDataPoint[] ToAverageDataPoints(this IEnumerable<IDataPoint> points, TimeSpan bucketSize)
    {
        var groups = points.GroupBy(p => p.TimeUnixNano.UnixNanosToDateTimeOffset().RoundToNearestTimeSpan(bucketSize));
        var res = (
                from g in groups
                select new DataPoint
                {
                    Value = g.Average(p => p.Value),
                    TimeUnixNano = g.Key.ToUnixTimeNanoseconds(),
                }
            )
            .Cast<IDataPoint>()
            .ToList();

        return res.ToArray();
    }

    public static IDataPoint[] ToDeltaDataPoints(this IEnumerable<IDataPoint> res)
    {
        var all = res.OrderBy(p => p.Value).ToArray();
        for (var i = all.Length - 1; i > 0; i--)
        {
            var p = new DataPoint()
            {
                Value = all[i].Value,
                TimeUnixNano = all[i].TimeUnixNano,
            };
            p.Value -= all[i - 1].Value;
            all[i] = p;
        }

        //the first element has not been reduced by a previous value
        return all.Skip(1).ToArray();
    }

    public static IDataPoint[] ToPercentileDataPoints(this IEnumerable<IDataPoint> points, double percentile, TimeSpan bucketSize)
    {
        var groups = points.GroupBy(p => p.TimeUnixNano.UnixNanosToDateTimeOffset().RoundToNearestTimeSpan(bucketSize));
        var res = new List<IDataPoint>();
        foreach (var group in groups)
        {
            var gp = group.OrderBy(p => p.Value).ToArray();
            var index = (int)(gp.Length * percentile);

            if (index < 0)
            {
                index = 0;
            }

            if (index >= gp.Length)
            {
                index = gp.Length - 1;
            }
            
            var p = gp.Skip(index).ToArray();
            res.AddRange(p);
        }
        
        return res.ToArray();
    }
    
    public static IGrouping<string, IDataPoint>[] GroupByKey(this IEnumerable<IDataPoint> points, IEnumerable<string> groupByAttributes)
    {
        var groupBy = groupByAttributes.ToArray();
        if (groupBy.Length == 0)
        {
            var res = points.GroupBy(p => "").ToArray();
            return res;
        }
        else
        {
            var res = points.GroupBy(p =>
            {
                var fullKey = "";
                foreach (var k in groupBy)
                {
                    var x = k + ":" + (p.Attributes.FirstOrDefault(a => a.Key == k)?.Value?.StringValue ?? "<none>");
                    if (fullKey == "")
                    {
                        fullKey += x;
                    }
                    else
                    {
                        fullKey += "|" + x;
                    }
                }

                return fullKey;
            }).ToArray();
            return res;
        }
    }
    
    public static IDataPoint[] GetSummaryDataPoints(this IEnumerable<Metric> self) =>
        self
            .Select(m => m.Summary)
            .SelectMany(m => m.DataPoints)
            .Cast<IDataPoint>()
            .ToArray();

    public static IDataPoint[] GetExponentialHistogramDataPoints(this IEnumerable<Metric> self) =>
        self
            .Select(m => m.ExponentialHistogram)
            .SelectMany(m => m.DataPoints)
            .Cast<IDataPoint>()
            .ToArray();

    public static IDataPoint[] GetHistogramDataPoints(this IEnumerable<Metric> self) =>
        self
            .Select(m => m.Histogram)
            .SelectMany(m => m.DataPoints)
            .Cast<IDataPoint>()
            .ToArray();

    public static IDataPoint[] GetSumDataPoints(this IEnumerable<Metric> self) =>
        self
            .Select(m => m.Sum)
            .SelectMany(m => m.DataPoints)
            .Cast<IDataPoint>()
            .ToArray();

    public static IDataPoint[] GetGaugeDataPoints(this IEnumerable<Metric> self) =>
        self
            .Select(m => m.Gauge)
            .SelectMany(m => m.DataPoints)
            .Cast<IDataPoint>()
            .ToArray();
}