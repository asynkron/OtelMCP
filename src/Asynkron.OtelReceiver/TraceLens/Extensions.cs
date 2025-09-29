using System.Globalization;
using TraceLens.Infra;

namespace TraceLens;

public static class Extensions
{
    public static DateTimeOffset RoundToNearestTimeSpan(this DateTimeOffset dateTime, TimeSpan bucketSize)
    {
        var totalSeconds = (int)(dateTime.TimeOfDay.TotalSeconds);
        var roundedSeconds = (int)Math.Round(totalSeconds / bucketSize.TotalSeconds) * bucketSize.TotalSeconds;
        return new DateTimeOffset(dateTime.Date.AddSeconds(roundedSeconds), dateTime.Offset);
    }
    public static DateTimeOffset UnixNanosToDateTimeOffset(this ulong t)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds((long)(t / 1_000_000));
    }

    public static DateTimeOffset UnixMillisToDateTimeOffset(this ulong t)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds((long)(t / 1_000));
    }

    public static DateTimeOffset UnixSecondsToDateTimeOffset(this ulong t)
    {
        return DateTimeOffset.FromUnixTimeSeconds((long)t);
    }

    public static ulong ToUnixTimeNanoseconds(this DateTimeOffset t)
    {
        return (ulong)t.ToUnixTimeMilliseconds() * 1_000_000;
    }

    public static string Humanize(this ulong ts)
    {
        var self = ts.ToTimeSpan();

        if (self.TotalMicroseconds < 3)
            return ((int)self.TotalNanoseconds).ToString(CultureInfo.InvariantCulture) + " ns";
        if (self.TotalMilliseconds < 3)
            return ((int)self.TotalMicroseconds).ToString(CultureInfo.InvariantCulture) + " μs";
        if (self.TotalSeconds < 3) return ((int)self.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
        if (self.TotalMinutes < 2) return ((int)self.TotalSeconds).ToString(CultureInfo.InvariantCulture) + " s";
        if (self.TotalHours < 2) return ((int)self.TotalMinutes).ToString(CultureInfo.InvariantCulture) + " min";
        if (self.TotalDays < 2) return ((int)self.TotalHours).ToString(CultureInfo.InvariantCulture) + " h";
        return ((int)self.TotalDays).ToString(CultureInfo.InvariantCulture) + " d";
    }

    public static string ToInvariantString(this double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}